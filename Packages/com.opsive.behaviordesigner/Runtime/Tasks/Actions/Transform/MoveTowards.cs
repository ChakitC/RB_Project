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
    [Opsive.Shared.Utility.Description("Move towards the target position using Vector3.MoveTowards. The object can pass through walls with this action. " +
                     "The position can either be specified by a transform or position. If the transform is specified then the position will not be used.")]
    public class MoveTowards : TargetGameObjectAction
    {
        [Tooltip("The speed of the agent")]
        [SerializeField] protected SharedVariable<float> m_Speed = 5;
        [Tooltip("The agent has arrived when the magnitude is less than this value.")]
        [SerializeField] protected SharedVariable<float> m_ArrivedDistance = 0.1f;
        [Tooltip("The GameObject that the agent should move towards.")]
        [SerializeField] protected SharedVariable<GameObject> m_Target;
        [Tooltip("If target is null then use the target position")]
        [SerializeField] protected SharedVariable<Vector3> m_TargetPosition;
        [Tooltip("The easing curve type for movement.")]
        [SerializeField] protected SmoothMoveTo.EasingType m_EasingType = SmoothMoveTo.EasingType.Linear;

        private float m_InitialDistance;

        /// <summary>
        /// Called when the action starts.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();
            var targetPosition = GetTargetDestination();
            m_InitialDistance = Vector3.Distance(transform.position, targetPosition);
        }

        /// <summary>
        /// Moves the Transform towards the target position with optional easing.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            var targetPosition = GetTargetDestination();
            var direction = targetPosition - transform.position;
            var currentDistance = Vector3.Magnitude(direction);

            var progress = Mathf.Clamp01(1f - (currentDistance / m_InitialDistance));
            var easedProgress = ApplyEasing(progress);
            float speedMultiplier;
            switch (m_EasingType) {
                case SmoothMoveTo.EasingType.Linear:
                    speedMultiplier = 1f;
                    break;
                case SmoothMoveTo.EasingType.EaseIn:
                    speedMultiplier = Mathf.Lerp(0.2f, 1f, easedProgress);
                    break;
                case SmoothMoveTo.EasingType.EaseOut:
                    speedMultiplier = Mathf.Lerp(1f, 0.2f, easedProgress);
                    break;
                case SmoothMoveTo.EasingType.EaseInOut:
                    if (progress < 0.5f) {
                        var firstHalfProgress = progress * 2f;
                        speedMultiplier = Mathf.Lerp(0.2f, 1f, ApplyEasing(firstHalfProgress));
                    } else {
                        var secondHalfProgress = (progress - 0.5f) * 2f;
                        speedMultiplier = Mathf.Lerp(1f, 0.2f, ApplyEasing(secondHalfProgress));
                    }
                    break;
                default:
                    speedMultiplier = 1f;
                    break;
            }
            var moveDistance = m_Speed.Value * speedMultiplier * Time.deltaTime;

            transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveDistance);
            var remainingDistance = Vector3.Distance(transform.position, targetPosition);
            var snapThreshold = Mathf.Min(0.001f, m_ArrivedDistance.Value);
            if (remainingDistance < snapThreshold) {
                transform.position = targetPosition;
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
            return m_Target.Value != null ? m_Target.Value.transform.position : m_TargetPosition.Value;
        }

        /// <summary>
        /// Applies easing to the progress value based on the selected easing type.
        /// </summary>
        /// <param name="t">The normalized progress value (0 to 1).</param>
        /// <returns>The eased value.</returns>
        private float ApplyEasing(float t)
        {
            t = Mathf.Clamp01(t);
            switch (m_EasingType) {
                case SmoothMoveTo.EasingType.Linear:
                    return t;
                case SmoothMoveTo.EasingType.EaseIn:
                    return t * t;
                case SmoothMoveTo.EasingType.EaseOut:
                    return 1f - (1f - t) * (1f - t);
                case SmoothMoveTo.EasingType.EaseInOut:
                    return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f;
                default:
                    return t;
            }
        }

        /// <summary>
        /// Resets the node values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();

            m_Speed = 5;
            m_ArrivedDistance = 0.1f;
            m_Target = null;
            m_TargetPosition = Vector3.zero;
            m_EasingType = SmoothMoveTo.EasingType.Linear;
        }
    }
}
#endif