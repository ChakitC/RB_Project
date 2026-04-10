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
    [Opsive.Shared.Utility.Description("Converts a Quaternion to a Vector3. Can extract Euler angles or direction vectors (Forward, Right, Up).")]
    public class ConvertQuaternionToVector3 : Action
    {
        /// <summary>
        /// Specifies how to convert the Quaternion to a Vector3.
        /// </summary>
        public enum ConversionType
        {
            EulerAngles,    // Extract Euler angles (X, Y, Z in degrees).
            Forward,        // Extract the forward direction vector.
            Right,          // Extract the right direction vector.
            Up              // Extract the up direction vector.
        }

        [Tooltip("The Quaternion to convert.")]
        [SerializeField] protected SharedVariable<Quaternion> m_InputQuaternion;
        [Tooltip("Specifies how to convert the Quaternion to a Vector3.")]
        [SerializeField] protected ConversionType m_ConversionType = ConversionType.EulerAngles;
        [Tooltip("The resulting Vector3 value.")]
        [SerializeField] [RequireShared] protected SharedVariable<Vector3> m_OutputVector;

        /// <summary>
        /// Converts the Quaternion to a Vector3.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            switch (m_ConversionType) {
                case ConversionType.EulerAngles:
                    m_OutputVector.Value = m_InputQuaternion.Value.eulerAngles;
                    break;
                case ConversionType.Forward:
                    m_OutputVector.Value = m_InputQuaternion.Value * Vector3.forward;
                    break;
                case ConversionType.Right:
                    m_OutputVector.Value = m_InputQuaternion.Value * Vector3.right;
                    break;
                case ConversionType.Up:
                    m_OutputVector.Value = m_InputQuaternion.Value * Vector3.up;
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
            m_ConversionType = ConversionType.EulerAngles;
            m_OutputVector = null;
        }
    }
}
#endif