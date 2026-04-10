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
    [Opsive.Shared.Utility.Description("Smoothly matches another Transform's position, rotation, and/or scale. Returns Finished when matched.")]
    public class MatchTransform : TargetGameObjectAction
    {
        [Tooltip("The Transform to match.")]
        [SerializeField] protected SharedVariable<Transform> m_TargetTransform;
        [Tooltip("Should the position be matched?")]
        [SerializeField] protected SharedVariable<bool> m_MatchPosition = true;
        [Tooltip("Should the rotation be matched?")]
        [SerializeField] protected SharedVariable<bool> m_MatchRotation = true;
        [Tooltip("Should the scale be matched?")]
        [SerializeField] protected SharedVariable<bool> m_MatchScale = false;
        [Tooltip("The position matching speed.")]
        [SerializeField] protected SharedVariable<float> m_PositionSpeed = 5f;
        [Tooltip("The rotation matching speed (degrees per second).")]
        [SerializeField] protected SharedVariable<float> m_RotationSpeed = 90f;
        [Tooltip("The scale matching speed.")]
        [SerializeField] protected SharedVariable<float> m_ScaleSpeed = 5f;
        [Tooltip("The arrival distance threshold for position.")]
        [SerializeField] protected SharedVariable<float> m_PositionArrivedDistance = 0.1f;
        [Tooltip("The arrival angle threshold for rotation (in degrees).")]
        [SerializeField] protected SharedVariable<float> m_RotationArrivedAngle = 1f;
        [Tooltip("The arrival distance threshold for scale.")]
        [SerializeField] protected SharedVariable<float> m_ScaleArrivedDistance = 0.01f;

        /// <summary>
        /// Matches the target transform.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_TargetTransform.Value == null) {
                return TaskStatus.Running;
            }

            var allMatched = true;

            // Match position.
            if (m_MatchPosition.Value) {
                var targetPos = m_TargetTransform.Value.position;
                var distance = Vector3.Distance(transform.position, targetPos);
                if (distance > m_PositionArrivedDistance.Value) {
                    transform.position = Vector3.MoveTowards(transform.position, targetPos, m_PositionSpeed.Value * Time.deltaTime);
                    allMatched = false;
                } else {
                    transform.position = targetPos;
                }
            }

            // Match rotation.
            if (m_MatchRotation.Value) {
                var targetRot = m_TargetTransform.Value.rotation;
                var angle = Quaternion.Angle(transform.rotation, targetRot);
                if (angle > m_RotationArrivedAngle.Value) {
                    var maxDegreesDelta = m_RotationSpeed.Value * Time.deltaTime;
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, maxDegreesDelta);
                    allMatched = false;
                } else {
                    transform.rotation = targetRot;
                }
            }

            // Match scale.
            if (m_MatchScale.Value) {
                var targetScale = m_TargetTransform.Value.localScale;
                var distance = Vector3.Distance(transform.localScale, targetScale);
                if (distance > m_ScaleArrivedDistance.Value) {
                    transform.localScale = Vector3.MoveTowards(transform.localScale, targetScale, m_ScaleSpeed.Value * Time.deltaTime);
                    allMatched = false;
                } else {
                    transform.localScale = targetScale;
                }
            }

            return allMatched ? TaskStatus.Success : TaskStatus.Running;
        }
    }
}
#endif