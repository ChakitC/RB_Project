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
    [Opsive.Shared.Utility.Description("Linearly interpolates the Transform between two Transforms based on a t value (0 to 1). Returns Finished when t >= 1.")]
    public class LerpTransform : TargetGameObjectAction
    {
        [Tooltip("The start Transform. If null, uses current Transform values.")]
        [SerializeField] protected SharedVariable<Transform> m_StartTransform;
        [Tooltip("The end Transform.")]
        [SerializeField] protected SharedVariable<Transform> m_EndTransform;
        [Tooltip("The interpolation value (0 = start, 1 = end).")]
        [SerializeField] protected SharedVariable<float> m_T = 0f;
        [Tooltip("Should the position be lerped?")]
        [SerializeField] protected SharedVariable<bool> m_LerpPosition = true;
        [Tooltip("Should the rotation be lerped?")]
        [SerializeField] protected SharedVariable<bool> m_LerpRotation = true;
        [Tooltip("Should the scale be lerped?")]
        [SerializeField] protected SharedVariable<bool> m_LerpScale = false;

        private Vector3 m_StartPosition;
        private Quaternion m_StartRotation;
        private Vector3 m_StartScale;

        /// <summary>
        /// Called when the state machine starts.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();
            if (m_StartTransform.Value != null) {
                m_StartPosition = m_StartTransform.Value.position;
                m_StartRotation = m_StartTransform.Value.rotation;
                m_StartScale = m_StartTransform.Value.localScale;
            } else {
                m_StartPosition = transform.position;
                m_StartRotation = transform.rotation;
                m_StartScale = transform.localScale;
            }
        }

        /// <summary>
        /// Lerps the transform.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_EndTransform.Value == null) {
                return TaskStatus.Running;
            }

            var t = Mathf.Clamp01(m_T.Value);

            if (m_LerpPosition.Value) {
                transform.position = Vector3.Lerp(m_StartPosition, m_EndTransform.Value.position, t);
            }

            if (m_LerpRotation.Value) {
                transform.rotation = Quaternion.Lerp(m_StartRotation, m_EndTransform.Value.rotation, t);
            }

            if (m_LerpScale.Value) {
                transform.localScale = Vector3.Lerp(m_StartScale, m_EndTransform.Value.localScale, t);
            }

            if (t >= 1f) {
                return TaskStatus.Success;
            }

            return TaskStatus.Running;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_StartTransform = null;
            m_EndTransform = null;
            m_T = 0f;
            m_LerpPosition = true;
            m_LerpRotation = true;
            m_LerpScale = false;
        }
    }
}
#endif