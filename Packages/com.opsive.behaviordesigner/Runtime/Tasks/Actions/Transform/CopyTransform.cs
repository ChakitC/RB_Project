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
    [Opsive.Shared.Utility.Description("Copies transform values (position, rotation, scale) from another Transform. Can be instant or smooth.")]
    public class CopyTransform : TargetGameObjectAction
    {
        [Tooltip("The source Transform to copy from.")]
        [SerializeField] protected SharedVariable<Transform> m_SourceTransform;
        [Tooltip("Should the position be copied?")]
        [SerializeField] protected SharedVariable<bool> m_CopyPosition = true;
        [Tooltip("Should the rotation be copied?")]
        [SerializeField] protected SharedVariable<bool> m_CopyRotation = true;
        [Tooltip("Should the scale be copied?")]
        [SerializeField] protected SharedVariable<bool> m_CopyScale = false;
        [Tooltip("Should the copy be smooth?")]
        [SerializeField] protected SharedVariable<bool> m_Smooth = false;
        [Tooltip("The position copy speed. Only used if Smooth is enabled.")]
        [SerializeField] protected SharedVariable<float> m_PositionSpeed = 5f;
        [Tooltip("The rotation copy speed (degrees per second). Only used if Smooth is enabled.")]
        [SerializeField] protected SharedVariable<float> m_RotationSpeed = 90f;
        [Tooltip("The scale copy speed. Only used if Smooth is enabled.")]
        [SerializeField] protected SharedVariable<float> m_ScaleSpeed = 5f;

        /// <summary>
        /// Copies the transform values.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_SourceTransform.Value == null) {
                return TaskStatus.Failure;
            }

            if (m_Smooth.Value) {
                // Smooth copy.
                if (m_CopyPosition.Value) {
                    transform.position = Vector3.MoveTowards(transform.position, m_SourceTransform.Value.position, m_PositionSpeed.Value * Time.deltaTime);
                }
                if (m_CopyRotation.Value) {
                    var maxDegreesDelta = m_RotationSpeed.Value * Time.deltaTime;
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, m_SourceTransform.Value.rotation, maxDegreesDelta);
                }
                if (m_CopyScale.Value) {
                    transform.localScale = Vector3.MoveTowards(transform.localScale, m_SourceTransform.Value.localScale, m_ScaleSpeed.Value * Time.deltaTime);
                }
            } else {
                // Instant copy.
                if (m_CopyPosition.Value) {
                    transform.position = m_SourceTransform.Value.position;
                }
                if (m_CopyRotation.Value) {
                    transform.rotation = m_SourceTransform.Value.rotation;
                }
                if (m_CopyScale.Value) {
                    transform.localScale = m_SourceTransform.Value.localScale;
                }
            }

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_SourceTransform = null;
            m_CopyPosition = true;
            m_CopyRotation = true;
            m_CopyScale = false;
            m_Smooth = false;
            m_PositionSpeed = 5f;
            m_RotationSpeed = 90f;
            m_ScaleSpeed = 5f;
        }
    }
}
#endif