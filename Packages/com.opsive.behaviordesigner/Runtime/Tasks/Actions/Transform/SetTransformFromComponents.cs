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
    [Opsive.Shared.Utility.Description("Sets the Transform's position, rotation, and/or scale from separate component values. Only updates specified components.")]
    public class SetTransformFromComponents : TargetGameObjectAction
    {
        /// <summary>
        /// Specifies the rotation input type.
        /// </summary>
        public enum RotationType
        {
            Euler,      // Use Euler angles (Vector3).
            Quaternion  // Use Quaternion.
        }

        [Tooltip("The position to set. Only used if Set Position is enabled.")]
        [SerializeField] protected SharedVariable<Vector3> m_Position;
        [Tooltip("Should the position be set?")]
        [SerializeField] protected SharedVariable<bool> m_SetPosition = false;
        [Tooltip("The rotation type.")]
        [SerializeField] protected RotationType m_RotationType = RotationType.Euler;
        [Tooltip("The rotation as Euler angles. Only used if Set Rotation is enabled and Rotation Type is Euler.")]
        [SerializeField] protected SharedVariable<Vector3> m_RotationEuler;
        [Tooltip("The rotation as Quaternion. Only used if Set Rotation is enabled and Rotation Type is Quaternion.")]
        [SerializeField] protected SharedVariable<Quaternion> m_RotationQuaternion;
        [Tooltip("Should the rotation be set?")]
        [SerializeField] protected SharedVariable<bool> m_SetRotation = false;
        [Tooltip("The scale to set. Only used if Set Scale is enabled.")]
        [SerializeField] protected SharedVariable<Vector3> m_Scale;
        [Tooltip("Should the scale be set?")]
        [SerializeField] protected SharedVariable<bool> m_SetScale = false;

        /// <summary>
        /// Sets the transform components.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_SetPosition.Value) {
                transform.position = m_Position.Value;
            }

            if (m_SetRotation.Value) {
                if (m_RotationType == RotationType.Euler) {
                    transform.rotation = Quaternion.Euler(m_RotationEuler.Value);
                } else if (m_RotationType == RotationType.Quaternion) {
                    transform.rotation = m_RotationQuaternion.Value;
                }
            }

            if (m_SetScale.Value) {
                transform.localScale = m_Scale.Value;
            }

            return TaskStatus.Success;
        }
    }
}
#endif