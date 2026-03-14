/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Samples
{
    using Opsive.BehaviorDesigner.Runtime.Tasks;
    using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    /// <summary>
    /// Uses the NavMeshAgent to patrol through a set of waypoints.
    /// This task is basic intended for demo purposes. For a more complete task see the Movement Pack:
    /// https://assetstore.unity.com/packages/slug/310243
    /// </summary>
    [Shared.Utility.Category("Behavior Designer Samples")]
    public class RotateTowards : Action
    {
        [Tooltip("The patrol waypoints.")]
        [SerializeField] protected SharedVariable<GameObject> m_Target;
        [Tooltip("The speed the agent should rotate.")]
        [SerializeField] protected SharedVariable<float> m_MaxLookAtRotationDelta = 1;
        [Tooltip("Should the rotation only affect the Y axis?")]
        [UnityEngine.Serialization.FormerlySerializedAs("m_IgnoreY")]
        [SerializeField] protected SharedVariable<bool> m_OnlyY;

        /// <summary>
        /// Rotates towards the target.
        /// </summary>
        /// <returns>A status of running.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_Target.Value == null) {
                return TaskStatus.Failure;
            }

            var direction = m_Target.Value.transform.position - m_Transform.position;
            if (direction.magnitude < 0.001f) {
                return TaskStatus.Success;
            }

            var targetRotation = Quaternion.LookRotation(direction.normalized);
            
            if (m_OnlyY.Value) {
                var currentRotation = m_Transform.eulerAngles;
                var targetY = targetRotation.eulerAngles.y;
                var deltaAngle = Mathf.DeltaAngle(currentRotation.y, targetY);
                
                if (Mathf.Abs(deltaAngle) < m_MaxLookAtRotationDelta.Value) {
                    return TaskStatus.Success;
                }

                // Move towards the target angle using the shortest path
                currentRotation.y += Mathf.MoveTowards(0, deltaAngle, m_MaxLookAtRotationDelta.Value);
                m_Transform.rotation = Quaternion.Euler(currentRotation);
            } else {
                if (Quaternion.Angle(m_Transform.rotation, targetRotation) < m_MaxLookAtRotationDelta.Value) {
                    return TaskStatus.Success;
                }

                m_Transform.rotation = Quaternion.RotateTowards(m_Transform.rotation, targetRotation, m_MaxLookAtRotationDelta.Value);
            }

            return TaskStatus.Running;
        }
    }
}