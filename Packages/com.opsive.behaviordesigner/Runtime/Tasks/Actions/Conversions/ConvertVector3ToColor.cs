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
    [Opsive.Shared.Utility.Description("Converts a Vector3 to a Color (X=R, Y=G, Z=B, Alpha=1).")]
    public class ConvertVector3ToColor : Action
    {
        [Tooltip("The Vector3 to convert.")]
        [SerializeField] protected SharedVariable<Vector3> m_InputVector;
        [Tooltip("The alpha value to use for the Color. Default is 1.")]
        [SerializeField] protected SharedVariable<float> m_Alpha = 1f;
        [Tooltip("The resulting Color value.")]
        [SerializeField] [RequireShared] protected SharedVariable<Color> m_OutputColor;

        /// <summary>
        /// Converts the Vector3 to a Color.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            m_OutputColor.Value = new Color(m_InputVector.Value.x, m_InputVector.Value.y, m_InputVector.Value.z, m_Alpha.Value);

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_InputVector = null;
            m_Alpha = 1f;
            m_OutputColor = null;
        }
    }
}
#endif