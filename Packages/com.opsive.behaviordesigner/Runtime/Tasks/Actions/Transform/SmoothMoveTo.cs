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
    [Opsive.Shared.Utility.Description("Smoothly moves the Transform to a target position with acceleration and deceleration. Returns Finished when arrived.")]
    public class SmoothMoveTo : TargetGameObjectAction
    {
        /// <summary>
        /// Specifies the easing curve type for movement.
        /// </summary>
        public enum EasingType
        {
            Linear,     // Linear movement.
            EaseIn,     // Slow start, fast end.
            EaseOut,    // Fast start, slow end.
            EaseInOut   // Slow start and end, fast middle.
        }

        [Tooltip("The GameObject that the agent should move towards. If null, uses Target Position.")]
        [SerializeField] protected SharedVariable<GameObject> m_Target;
        [Tooltip("The target position. Only used if Target is null.")]
        [SerializeField] protected SharedVariable<Vector3> m_TargetPosition;
        [Tooltip("The maximum movement speed.")]
        [SerializeField] protected SharedVariable<float> m_MaxSpeed = 5f;
        [Tooltip("The acceleration rate (units per second squared).")]
        [SerializeField] protected SharedVariable<float> m_Acceleration = 10f;
        [Tooltip("The deceleration rate (units per second squared).")]
        [SerializeField] protected SharedVariable<float> m_Deceleration = 10f;
        [Tooltip("The agent has arrived when the distance is less than this value.")]
        [SerializeField] protected SharedVariable<float> m_ArrivedDistance = 0.1f;
        [Tooltip("The easing curve type for movement.")]
        [SerializeField] protected EasingType m_EasingType = EasingType.Linear;
        [Tooltip("Should the movement be relative to the Transform's local space?")]
        [SerializeField] protected SharedVariable<bool> m_UseLocalSpace = false;

        private float m_CurrentSpeed;

        /// <summary>
        /// Called when the state machine starts.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();
            m_CurrentSpeed = 0f;
        }

        /// <summary>
        /// Smoothly moves the Transform to the target position.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            var targetPosition = GetTargetDestination();
            var currentPosition = m_UseLocalSpace.Value ? transform.localPosition : transform.position;
            var direction = targetPosition - currentPosition;
            var distance = direction.magnitude;

            // Calculate deceleration distance.
            var decelerationDistance = (m_CurrentSpeed * m_CurrentSpeed) / (2f * m_Deceleration.Value);

            // Accelerate or decelerate based on distance to target.
            if (distance > decelerationDistance) {
                m_CurrentSpeed = Mathf.Min(m_CurrentSpeed + m_Acceleration.Value * Time.deltaTime, m_MaxSpeed.Value);
            } else {
                m_CurrentSpeed = Mathf.Max(m_CurrentSpeed - m_Deceleration.Value * Time.deltaTime, 0f);
            }

            // Apply easing.
            var easedSpeed = ApplyEasing(m_CurrentSpeed / m_MaxSpeed.Value) * m_MaxSpeed.Value;
            var moveDistance = easedSpeed * Time.deltaTime;

            // Move towards target. Clamp to avoid overshoot so we can finish with a negligible snap.
            var newPosition = Vector3.MoveTowards(currentPosition, targetPosition, moveDistance);
            if (m_UseLocalSpace.Value) {
                transform.localPosition = newPosition;
            } else {
                transform.position = newPosition;
            }

            var remainingDistance = Vector3.Distance(newPosition, targetPosition);
            var snapThreshold = Mathf.Min(0.001f, m_ArrivedDistance.Value);
            if (remainingDistance < snapThreshold) {
                if (m_UseLocalSpace.Value) {
                    transform.localPosition = targetPosition;
                } else {
                    transform.position = targetPosition;
                }
                return TaskStatus.Success;
            }
            return TaskStatus.Running;
        }

        /// <summary>
        /// Returns the target destination.
        /// </summary>
        /// <returns>The target destination.</returns>
        private Vector3 GetTargetDestination()
        {
            if (m_Target.Value != null) {
                return m_UseLocalSpace.Value ? m_Target.Value.transform.localPosition : m_Target.Value.transform.position;
            }
            return m_TargetPosition.Value;
        }

        /// <summary>
        /// Applies easing to the speed value.
        /// </summary>
        /// <param name="t">The normalized time value (0 to 1).</param>
        /// <returns>The eased value.</returns>
        private float ApplyEasing(float t)
        {
            t = Mathf.Clamp01(t);
            switch (m_EasingType) {
                case EasingType.Linear:
                    return t;
                case EasingType.EaseIn:
                    return t * t;
                case EasingType.EaseOut:
                    return 1f - (1f - t) * (1f - t);
                case EasingType.EaseInOut:
                    return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
                default:
                    return t;
            }
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_Target = null;
            m_TargetPosition = null;
            m_MaxSpeed = 5f;
            m_Acceleration = 10f;
            m_Deceleration = 10f;
            m_ArrivedDistance = 0.1f;
            m_EasingType = EasingType.Linear;
            m_UseLocalSpace = false;
        }
    }
}
#endif