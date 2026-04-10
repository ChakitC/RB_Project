#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.GameObjectTasks
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("GameObject")]
    [Opsive.Shared.Utility.Description("Spawns a GameObject prefab at a specified position with full configuration. Can optionally auto-destroy after a duration.")]
    public class SpawnAtPosition : Action
    {
        [Tooltip("The prefab to spawn.")]
        [SerializeField] protected GameObject m_Prefab;
        [Tooltip("The spawn position. If null, uses Vector3.zero.")]
        [SerializeField] protected SharedVariable<Vector3> m_SpawnPosition;
        [Tooltip("The Transform to spawn at. If assigned, overrides Spawn Position.")]
        [SerializeField] protected SharedVariable<Transform> m_SpawnTransform;
        [Tooltip("The spawn rotation. If null, uses Quaternion.identity.")]
        [SerializeField] protected SharedVariable<Quaternion> m_SpawnRotation;
        [Tooltip("The parent Transform to assign. If null, no parent is assigned.")]
        [SerializeField] protected SharedVariable<Transform> m_Parent;
        [Tooltip("Should the GameObject be auto-destroyed after a duration?")]
        [SerializeField] protected SharedVariable<bool> m_AutoDestroy = false;
        [Tooltip("The duration before auto-destroy. Only used if Auto Destroy is enabled.")]
        [SerializeField] protected SharedVariable<float> m_DestroyDuration = 5f;
        [Tooltip("The spawned GameObject.")]
        [SerializeField] [RequireShared] protected SharedVariable<GameObject> m_SpawnedObject;

        /// <summary>
        /// Spawns the GameObject at the specified position.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_Prefab == null) {
                Debug.LogWarning("SpawnAtPosition: Prefab is not assigned.");
                return TaskStatus.Success;
            }

            // Determine spawn position.
            Vector3 position;
            Quaternion rotation;

            if (m_SpawnTransform.Value != null) {
                position = m_SpawnTransform.Value.position;
                rotation = m_SpawnTransform.Value.rotation;
            } else {
                position = m_SpawnPosition.Value;
                rotation = m_SpawnRotation.Value;
            }

            // Spawn the GameObject.
            var spawned = m_Parent.Value != null ? UnityEngine.Object.Instantiate(m_Prefab, position, rotation, m_Parent.Value) : UnityEngine.Object.Instantiate(m_Prefab, position, rotation);
            m_SpawnedObject.Value = spawned;

            // Setup auto-destroy if enabled.
            if (m_AutoDestroy.Value) {
                UnityEngine.Object.Destroy(spawned, m_DestroyDuration.Value);
            }

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_Prefab = null;
            m_SpawnPosition = null;
            m_SpawnTransform = null;
            m_SpawnRotation = null;
            m_Parent = null;
            m_AutoDestroy = false;
            m_DestroyDuration = 5f;
            m_SpawnedObject = null;
        }
    }
}
#endif