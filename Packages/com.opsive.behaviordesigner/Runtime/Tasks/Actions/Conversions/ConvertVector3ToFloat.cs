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
    [Opsive.Shared.Utility.Description("Converts a Vector3 to a float value. Can extract magnitude, squared magnitude, X, Y, or Z component.")]
    public class ConvertVector3ToFloat : Action
    {
        /// <summary>
        /// Specifies how to convert the Vector3 to a float.
        /// </summary>
        public enum ConversionType
        {
            Magnitude,      // The magnitude (length) of the vector.
            SqrMagnitude,   // The squared magnitude of the vector.
            X,              // The X component.
            Y,              // The Y component.
            Z               // The Z component.
        }

        [Tooltip("The Vector3 to convert.")]
        [SerializeField] protected SharedVariable<Vector3> m_InputVector;
        [Tooltip("Specifies how to convert the Vector3 to a float.")]
        [SerializeField] protected ConversionType m_ConversionType = ConversionType.Magnitude;
        [Tooltip("The resulting float value.")]
        [SerializeField] [RequireShared] protected SharedVariable<float> m_OutputValue;

        /// <summary>
        /// Converts the Vector3 to a float based on the conversion type.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            switch (m_ConversionType) {
                case ConversionType.Magnitude:
                    m_OutputValue.Value = m_InputVector.Value.magnitude;
                    break;
                case ConversionType.SqrMagnitude:
                    m_OutputValue.Value = m_InputVector.Value.sqrMagnitude;
                    break;
                case ConversionType.X:
                    m_OutputValue.Value = m_InputVector.Value.x;
                    break;
                case ConversionType.Y:
                    m_OutputValue.Value = m_InputVector.Value.y;
                    break;
                case ConversionType.Z:
                    m_OutputValue.Value = m_InputVector.Value.z;
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
            m_ConversionType = ConversionType.Magnitude;
            m_OutputValue = null;
        }
    }
}
#endif