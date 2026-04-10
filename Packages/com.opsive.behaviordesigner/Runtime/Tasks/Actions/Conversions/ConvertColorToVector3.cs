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
    [Opsive.Shared.Utility.Description("Converts a Color to a Vector3 (R, G, B components, alpha is dropped).")]
    public class ConvertColorToVector3 : Action
    {
        [Tooltip("The Color to convert.")]
        [SerializeField] protected SharedVariable<Color> m_InputColor;
        [Tooltip("The resulting Vector3 value (R, G, B).")]
        [SerializeField] [RequireShared] protected SharedVariable<Vector3> m_OutputVector;

        /// <summary>
        /// Converts the Color to a Vector3.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            m_OutputVector.Value = new Vector3(m_InputColor.Value.r, m_InputColor.Value.g, m_InputColor.Value.b);

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_InputColor = null;
            m_OutputVector = null;
        }
    }
}
#endif