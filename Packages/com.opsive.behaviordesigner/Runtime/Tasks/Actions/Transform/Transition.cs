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
    [Opsive.Shared.Utility.Description("Transitions between positions/rotations with duration and easing.")]
    public class Transition : TargetGameObjectAction
    {
        [Tooltip("The target GameObject whose transform to use. If null, uses Target Position and Target Rotation.")]
        [SerializeField] protected SharedVariable<GameObject> m_Target;
        [Tooltip("The target position. Only used if Target is null.")]
        [SerializeField] protected SharedVariable<Vector3> m_TargetPosition;
        [Tooltip("The target rotation (Euler angles). Only used if Target is null.")]
        [SerializeField] protected SharedVariable<Vector3> m_TargetRotation;
        [Tooltip("The transition duration.")]
        [SerializeField] protected SharedVariable<float> m_Duration = 1.0f;
        [Tooltip("The easing curve type.")]
        [SerializeField] protected SmoothMoveTo.EasingType m_EasingType = SmoothMoveTo.EasingType.Linear;

        private Vector3 m_StartPosition;
        private Quaternion m_StartRotation;
        private float m_ElapsedTime;

        /// <summary>
        /// Called when the action starts.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();
            m_ElapsedTime = 0.0f;
            m_StartPosition = transform.position;
            m_StartRotation = transform.rotation;
        }

        /// <summary>
        /// Updates the camera transition.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            m_ElapsedTime += Time.deltaTime;
            var progress = Mathf.Clamp01(m_ElapsedTime / m_Duration.Value);
            var easedProgress = ApplyEasing(progress);

            transform.position = Vector3.Lerp(m_StartPosition, m_Target.Value != null ? m_Target.Value.transform.position : m_TargetPosition.Value, easedProgress);
            transform.rotation = Quaternion.Slerp(m_StartRotation, m_Target.Value != null ? m_Target.Value.transform.rotation : Quaternion.Euler(m_TargetRotation.Value), easedProgress);

            if (progress >= 1.0f) {
                return TaskStatus.Success;
            }

            return TaskStatus.Running;
        }

        /// <summary>
        /// Applies easing to the value.
        /// </summary>
        private float ApplyEasing(float t)
        {
            switch (m_EasingType) {
                case SmoothMoveTo.EasingType.EaseIn:
                    return t * t;
                case SmoothMoveTo.EasingType.EaseOut:
                    return 1.0f - (1.0f - t) * (1.0f - t);
                case SmoothMoveTo.EasingType.EaseInOut:
                    return t < 0.5f ? 2.0f * t * t : 1.0f - Mathf.Pow(-2.0f * t + 2.0f, 2.0f) / 2.0f;
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
            m_TargetRotation = null;
            m_Duration = 1.0f;
            m_EasingType = SmoothMoveTo.EasingType.Linear;
        }
    }
}
#endif