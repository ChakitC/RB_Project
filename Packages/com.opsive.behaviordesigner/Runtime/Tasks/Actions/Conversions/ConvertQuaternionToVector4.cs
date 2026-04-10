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
    [Opsive.Shared.Utility.Description("Converts a Quaternion to a Vector4 (X, Y, Z, W components).")]
    public class ConvertQuaternionToVector4 : Action
    {
        [Tooltip("The Quaternion to convert.")]
        [SerializeField] protected SharedVariable<Quaternion> m_InputQuaternion;
        [Tooltip("The resulting Vector4 value.")]
        [SerializeField] [RequireShared] protected SharedVariable<Vector4> m_OutputVector;

        /// <summary>
        /// Converts the Quaternion to a Vector4.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            m_OutputVector.Value = new Vector4(m_InputQuaternion.Value.x, m_InputQuaternion.Value.y, m_InputQuaternion.Value.z, m_InputQuaternion.Value.w);

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_InputQuaternion = null;
            m_OutputVector = null;
        }
    }
}
#endif