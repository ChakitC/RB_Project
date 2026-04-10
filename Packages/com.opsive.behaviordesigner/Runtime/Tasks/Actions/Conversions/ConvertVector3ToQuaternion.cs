#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.Conversions
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("Conversions")]
    [Opsive.Shared.Utility.Description("Converts a Vector3 to a Quaternion. Can use LookRotation (direction to rotation) or Euler angles.")]
    public class ConvertVector3ToQuaternion : Action
    {
        /// <summary>
        /// Specifies how to convert the Vector3 to a Quaternion.
        /// </summary>
        public enum ConversionType
        {
            LookRotation,   // Treats Vector3 as a direction and creates a rotation looking in that direction.
            EulerAngles     // Treats Vector3 as Euler angles (X, Y, Z in degrees).
        }

        [Tooltip("The Vector3 to convert.")]
        [SerializeField] protected SharedVariable<Vector3> m_InputVector;
        [Tooltip("Specifies how to convert the Vector3 to a Quaternion.")]
        [SerializeField] protected ConversionType m_ConversionType = ConversionType.EulerAngles;
        [Tooltip("The up vector for LookRotation. Only used when ConversionType is LookRotation.")]
        [SerializeField] protected SharedVariable<Vector3> m_UpVector = Vector3.up;
        [Tooltip("The resulting Quaternion value.")]
        [SerializeField] [RequireShared] protected SharedVariable<Quaternion> m_OutputQuaternion;

        /// <summary>
        /// Converts the Vector3 to a Quaternion.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            switch (m_ConversionType) {
                case ConversionType.LookRotation:
                    m_OutputQuaternion.Value = Quaternion.LookRotation(m_InputVector.Value, m_UpVector.Value);
                    break;
                case ConversionType.EulerAngles:
                    m_OutputQuaternion.Value = Quaternion.Euler(m_InputVector.Value);
                    break;
            }

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_InputVector = null;
            m_ConversionType = ConversionType.EulerAngles;
            m_UpVector = Vector3.up;
            m_OutputQuaternion = null;
        }
    }
}
#endif