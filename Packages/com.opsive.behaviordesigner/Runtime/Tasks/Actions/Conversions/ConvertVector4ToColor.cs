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
    [Opsive.Shared.Utility.Description("Converts a Vector4 to a Color (X=R, Y=G, Z=B, W=A).")]
    public class ConvertVector4ToColor : Action
    {
        [Tooltip("The Vector4 to convert.")]
        [SerializeField] protected SharedVariable<Vector4> m_InputVector;
        [Tooltip("The resulting Color value.")]
        [SerializeField] [RequireShared] protected SharedVariable<Color> m_OutputColor;

        /// <summary>
        /// Converts the Vector4 to a Color.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            m_OutputColor.Value = new Color(m_InputVector.Value.x, m_InputVector.Value.y, m_InputVector.Value.z, m_InputVector.Value.w);

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_InputVector = null;
            m_OutputColor = null;
        }
    }
}
#endif