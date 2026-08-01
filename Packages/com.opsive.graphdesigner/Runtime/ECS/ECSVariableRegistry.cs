#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Graph Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.GraphDesigner.Runtime.Variables
{
    using System;
    using System.Collections.Generic;
    using Opsive.GraphDesigner.Runtime.ECS.Core;
    using Unity.Entities;
    using UnityEngine;

    /// <summary>
    /// Managed registry created once per entity during graph initialization. Collects SharedVariable registrations from all ECS tasks, deduplicates by name+scope,
    /// bakes initial values into DynamicBuffer<SharedVariableElement>, and supports syncing runtime values between the ECS buffer and managed SharedVariables.
    /// </summary>
    public class ECSVariableRegistry
    {
        private readonly ECSVariableRegistryCore m_Core = new();
        private readonly List<Action<DynamicBuffer<SharedVariableElement>>> m_SyncToManagedActions = new();
        private readonly List<Action<DynamicBuffer<SharedVariableElement>>> m_SyncToECSActions = new();

        /// <summary>
        /// Gets the number of variables registered so far.
        /// </summary>
        public int Count => m_Core.Count;

        /// <summary>
        /// Returns authoring metadata for a registered SharedVariable.
        /// </summary>
        /// <param name="index">The SharedVariableElement buffer index.</param>
        /// <param name="name">The registered variable name.</param>
        /// <param name="elementType">The registered variable value type.</param>
        /// <returns>True if the index maps to a tracked SharedVariable.</returns>
        public bool TryGetVariableMetadata(int index, out string name, out Type elementType)
        {
            return m_Core.TryGetVariableMetadata(index, out name, out elementType);
        }

        /// <summary>
        /// Registers a SharedVariable with the registry and returns its buffer index.
        /// </summary>
        /// <param name="sharedVariable">The SharedVariable that should be registered.</param>
        /// <returns>The buffer index of the registered SharedVariable, or -1 if the SharedVariable is null.</returns>
        public int Register<T>(SharedVariable<T> sharedVariable) where T : unmanaged
        {
            if (sharedVariable == null) {
                return -1;
            }

            var size = SharedVariableBufferCore.SizeOf<T>();
            Debug.Assert(size <= SharedVariableBufferCore.MaxValueSize, $"SharedVariable<{typeof(T).Name}> value size ({size} bytes) exceeds the {SharedVariableBufferCore.MaxValueSize}-byte SharedVariableElement limit. Use a smaller unmanaged type.");

            if (m_Core.TryGetExistingIndex(sharedVariable, out var existingIndex)) {
                return existingIndex;
            }

            var index = m_Core.Register(sharedVariable);
            if (index < 0) {
                return index;
            }

            // Capture sync delegates so managed and ECS tasks stay in sync while the tree is running.
            var capturedVar = sharedVariable;
            var capturedIndex = index;
            m_SyncToManagedActions.Add((buffer) => {
                capturedVar.Value = buffer.Get<T>(capturedIndex);
            });
            m_SyncToECSActions.Add((buffer) => {
                buffer.Set(capturedIndex, capturedVar.Value);
            });

            return index;
        }

        /// <summary>
        /// Creates the DynamicBuffer<SharedVariableElement> on the entity and writes all registered initial values. Call once after all tasks have registered their variables.
        /// </summary>
        /// <param name="world">The world that owns the entity.</param>
        /// <param name="entity">The entity that should receive the shared variable buffer.</param>
        public void Bake(World world, Entity entity)
        {
            if (m_Core.Count == 0) {
                return;
            }

            DynamicBuffer<SharedVariableElement> buffer;
            if (world.EntityManager.HasBuffer<SharedVariableElement>(entity)) {
                buffer = world.EntityManager.GetBuffer<SharedVariableElement>(entity);
                buffer.Clear();
            } else {
                buffer = world.EntityManager.AddBuffer<SharedVariableElement>(entity);
            }
            for (int i = 0; i < m_Core.Count; ++i) {
                buffer.Add(new SharedVariableElement { Value = m_Core.GetInitialValue(i) });
            }
        }

        /// <summary>
        /// Writes the current managed SharedVariable values into the ECS buffer.
        /// </summary>
        /// <param name="world">The world that owns the entity.</param>
        /// <param name="entity">The entity whose shared variable buffer should be synced.</param>
        public void SyncToECS(World world, Entity entity)
        {
            if (m_SyncToECSActions.Count == 0 || world == null || entity == Entity.Null) {
                return;
            }

            if (!world.EntityManager.HasBuffer<SharedVariableElement>(entity)) {
                return;
            }

            var buffer = world.EntityManager.GetBuffer<SharedVariableElement>(entity);
            for (int i = 0; i < m_SyncToECSActions.Count; ++i) {
                if (!m_Core.IsManagedValueDirty(i)) {
                    continue;
                }
                m_SyncToECSActions[i](buffer);
                m_Core.MarkSyncedToECS(i, buffer[i].Value);
            }
        }

        /// <summary>
        /// Reads current ECS buffer values back into the managed SharedVariable instances.
        /// </summary>
        /// <param name="world">The world that owns the entity.</param>
        /// <param name="entity">The entity whose shared variable buffer should be synced.</param>
        public void SyncToManaged(World world, Entity entity)
        {
            if (m_SyncToManagedActions.Count == 0 || world == null || entity == Entity.Null) {
                return;
            }

            if (!world.EntityManager.HasBuffer<SharedVariableElement>(entity)) {
                return;
            }

            var buffer = world.EntityManager.GetBuffer<SharedVariableElement>(entity);
            m_Core.SuppressManagedValueTracking(() => {
                for (int i = 0; i < m_SyncToManagedActions.Count; ++i) {
                    var bufferValue = buffer[i].Value;
                    if (!m_Core.ShouldSyncToManaged(i, bufferValue)) {
                        continue;
                    }

                    m_SyncToManagedActions[i](buffer);
                    m_Core.MarkSyncedToManaged(i, bufferValue);
                }
            });
        }

        /// <summary>
        /// Removes any managed SharedVariable change listeners registered by the registry.
        /// </summary>
        public void Dispose()
        {
            m_Core.Dispose();
        }
    }
}
#endif