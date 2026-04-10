#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.TransformTasks
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("Transform")]
    [Opsive.Shared.Utility.Description("Bounces the Transform between two points. Returns Finished when bounce count is reached (or runs continuously if infinite).")]
    public class BounceBetweenPoints : TargetGameObjectAction
    {
        [Tooltip("Point A position.")]
        [SerializeField] protected SharedVariable<Vector3> m_PointA;
        [Tooltip("Point B position.")]
        [SerializeField] protected SharedVariable<Vector3> m_PointB;
        [Tooltip("The bounce speed.")]
        [SerializeField] protected SharedVariable<float> m_BounceSpeed = 5f;
        [Tooltip("The bounce damping (0 = no damping, 1 = full damping).")]
        [SerializeField] protected SharedVariable<float> m_BounceDamping = 0f;
        [Tooltip("The number of bounces. Set to 0 for infinite.")]
        [SerializeField] protected SharedVariable<int> m_BounceCount = 0;
        [Tooltip("The arrival distance threshold for each point.")]
        [SerializeField] protected SharedVariable<float> m_ArrivedDistance = 0.1f;

        private int m_CurrentBounceCount;
        private bool m_MovingToB;
        private float m_CurrentSpeed;

        /// <summary>
        /// Called when the state machine starts.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();
            m_CurrentBounceCount = 0;
            m_MovingToB = true;
            m_CurrentSpeed = m_BounceSpeed.Value;
        }

        /// <summary>
        /// Bounces between the two points.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_BounceCount.Value > 0 && m_CurrentBounceCount >= m_BounceCount.Value) {
                return TaskStatus.Success;
            }

            var currentPosition = transform.position;
            var targetPosition = m_MovingToB ? m_PointB.Value : m_PointA.Value;
            var direction = (targetPosition - currentPosition).normalized;
            var distance = Vector3.Distance(currentPosition, targetPosition);

            // Check if arrived at point.
            if (distance < m_ArrivedDistance.Value) {
                transform.position = targetPosition;
                m_MovingToB = !m_MovingToB;
                m_CurrentBounceCount++;
                m_CurrentSpeed = m_BounceSpeed.Value * (1f - m_BounceDamping.Value);
            }

            // Move towards target.
            var moveDistance = m_CurrentSpeed * Time.deltaTime;
            transform.position = Vector3.MoveTowards(currentPosition, targetPosition, moveDistance);

            return TaskStatus.Running;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_PointA = null;
            m_PointB = null;
            m_BounceSpeed = 5f;
            m_BounceDamping = 0f;
            m_BounceCount = 0;
            m_ArrivedDistance = 0.1f;
        }
    }
}
#endif