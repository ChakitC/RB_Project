#if GRAPH_DESIGNER && UNITY_EDITOR
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Systems
{
    using Opsive.BehaviorDesigner.Runtime.Components;
    using Opsive.BehaviorDesigner.Runtime.Groups;
    using Opsive.GraphDesigner.Runtime;
    using Opsive.GraphDesigner.Runtime.Variables;
    using System.Collections.Generic;
    using Unity.Entities;

    /// <summary>
    /// Mirrors ECS-backed SharedVariables into managed authoring SharedVariables for editor runtime inspection.
    /// </summary>
    [UpdateInGroup(typeof(TraversalSystemGroup))]
    [UpdateAfter(typeof(TraversalTaskSystemGroup))]
    [UpdateBefore(typeof(EvaluationSystem))]
    public partial class EditorECSSharedVariableSyncSystem : SystemBase
    {
        private readonly Dictionary<Entity, ECSVariableRegistry> m_RegistrySyncByEntity = new Dictionary<Entity, ECSVariableRegistry>();
        private readonly List<EditorGraphSharedVariableInspectionTracker.EntityInspectionKey> m_InspectedEntities = new List<EditorGraphSharedVariableInspectionTracker.EntityInspectionKey>();
        private readonly HashSet<Entity> m_ActivePureECSEntities = new HashSet<Entity>();
        private double m_NextSyncTime;
        private int m_LastInspectionVersion = -1;

        /// <summary>
        /// Syncs ECS-backed SharedVariables into the managed authoring variables after traversal completes.
        /// </summary>
        protected override void OnUpdate()
        {
            if (!EditorGraphSharedVariableInspectionTracker.HasInspectedEntities) {
                CleanupStaleRegistries(null);
                return;
            }

            var inspectionVersion = EditorGraphSharedVariableInspectionTracker.InspectionVersion;
            var forceSync = inspectionVersion != m_LastInspectionVersion;
            var time = UnityEngine.Time.realtimeSinceStartupAsDouble;
            if (!forceSync && time < m_NextSyncTime) {
                return;
            }

            m_LastInspectionVersion = inspectionVersion;
            m_NextSyncTime = time + EditorGraphSharedVariableInspectionTracker.DefaultSyncIntervalSeconds;

            EditorGraphSharedVariableInspectionTracker.GetInspectedEntities(m_InspectedEntities);
            if (m_InspectedEntities.Count == 0) {
                CleanupStaleRegistries(null);
                return;
            }

            m_ActivePureECSEntities.Clear();
            for (int i = 0; i < m_InspectedEntities.Count; ++i) {
                var inspectedEntity = m_InspectedEntities[i];
                if (!ReferenceEquals(inspectedEntity.World, World)) {
                    continue;
                }

                var entity = inspectedEntity.Entity;
                if (entity == Entity.Null || !EntityManager.Exists(entity)) {
                    RemovePureECSSyncRegistry(entity);
                    continue;
                }

                // Managed tree objects synchronize their own editor SharedVariable state during execution.
                if (BehaviorTree.GetBehaviorTree(entity) != null) {
                    RemovePureECSSyncRegistry(entity);
                    continue;
                }

                if (!EntityManager.HasComponent<EditorBehaviorTreeGraphReference>(entity) || !EntityManager.HasBuffer<SharedVariableElement>(entity)) {
                    RemovePureECSSyncRegistry(entity);
                    continue;
                }

                m_ActivePureECSEntities.Add(entity);

                var registry = GetOrCreateRegistry(entity);
                if (registry == null) {
                    continue;
                }

                registry.SyncToManaged(World, entity);
            }

            CleanupStaleRegistries(m_ActivePureECSEntities);
        }

        /// <summary>
        /// Returns the cached sync context for the entity, creating it on demand when the authoring behavior tree can be resolved.
        /// </summary>
        private ECSVariableRegistry GetOrCreateRegistry(Entity entity)
        {
            if (m_RegistrySyncByEntity.TryGetValue(entity, out var registry)) {
                return registry;
            }

            var graphReference = EntityManager.GetComponentObject<EditorBehaviorTreeGraphReference>(entity);
            if (graphReference == null || string.IsNullOrEmpty(graphReference.AuthoringBehaviorTreeGlobalObjectId)) {
                return null;
            }

            var behaviorTree = BehaviorTreeEditorUtility.ResolveBehaviorTreeFromGlobalObjectId(graphReference.AuthoringBehaviorTreeGlobalObjectId, graphReference.DesignGraphUniqueID);
            if (behaviorTree == null) {
                return null;
            }

            m_RegistrySyncByEntity[entity] = behaviorTree.CreateECSVariableSyncRegistry(World);
            return registry;
        }

        /// <summary>
        /// Removes cached contexts for entities that are no longer valid pure ECS sync targets.
        /// </summary>
        private void CleanupStaleRegistries(HashSet<Entity> activePureECSEntities)
        {
            if (m_RegistrySyncByEntity.Count == 0) {
                return;
            }

            List<Entity> staleEntities = null;
            foreach (var entry in m_RegistrySyncByEntity) {
                if (activePureECSEntities != null && activePureECSEntities.Contains(entry.Key)) {
                    continue;
                }

                if (staleEntities == null) { staleEntities = new List<Entity>(); }
                staleEntities.Add(entry.Key);
            }

            if (staleEntities == null) {
                return;
            }

            for (int i = 0; i < staleEntities.Count; ++i) {
                RemovePureECSSyncRegistry(staleEntities[i]);
            }
        }

        /// <summary>
        /// Removes and disposes the cached sync context for the specified entity.
        /// </summary>
        private void RemovePureECSSyncRegistry(Entity entity)
        {
            if (!m_RegistrySyncByEntity.TryGetValue(entity, out var registry)) {
                return;
            }

            registry?.Dispose();
            m_RegistrySyncByEntity.Remove(entity);
        }

        /// <summary>
        /// Disposes the cached registries.
        /// </summary>
        protected override void OnDestroy()
        {
            foreach (var entry in m_RegistrySyncByEntity) {
                entry.Value.Dispose();
            }

            m_RegistrySyncByEntity.Clear();
        }
    }
}
#endif