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
    [Opsive.Shared.Utility.Description("Converts a Quaternion to a float. Can extract X, Y, Z, W, or angle around an axis.")]
    public class ConvertQuaternionToFloat : Action
    {
        /// <summary>
        /// Specifies how to convert the Quaternion to a float.
        /// </summary>
        public enum ConversionType
        {
            X,          // The X component.
            Y,          // The Y component.
            Z,          // The Z component.
            W,          // The W component.
            Angle       // The angle (in degrees) around the specified axis.
        }

        [Tooltip("The Quaternion to convert.")]
        [SerializeField] protected SharedVariable<Quaternion> m_InputQuaternion;
        [Tooltip("Specifies how to convert the Quaternion to a float.")]
        [SerializeField] protected ConversionType m_ConversionType = ConversionType.Angle;
        [Tooltip("The axis to use when extracting the angle. Only used when ConversionType is Angle.")]
        [SerializeField] protected SharedVariable<Vector3> m_Axis = Vector3.forward;
        [Tooltip("The resulting float value.")]
        [SerializeField] [RequireShared] protected SharedVariable<float> m_OutputValue;

        /// <summary>
        /// Converts the Quaternion to a float.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            switch (m_ConversionType) {
                case ConversionType.X:
                    m_OutputValue.Value = m_InputQuaternion.Value.x;
                    break;
                case ConversionType.Y:
                    m_OutputValue.Value = m_InputQuaternion.Value.y;
                    break;
                case ConversionType.Z:
                    m_OutputValue.Value = m_InputQuaternion.Value.z;
                    break;
                case ConversionType.W:
                    m_OutputValue.Value = m_InputQuaternion.Value.w;
                    break;
                case ConversionType.Angle:
                    var axis = m_Axis.Value.normalized;
                    var identity = Quaternion.identity;
                    m_OutputValue.Value = Quaternion.Angle(identity, m_InputQuaternion.Value);
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
            m_InputQuaternion = null;
            m_ConversionType = ConversionType.Angle;
            m_Axis = Vector3.forward;
            m_OutputValue = null;
        }
    }
}
#endif