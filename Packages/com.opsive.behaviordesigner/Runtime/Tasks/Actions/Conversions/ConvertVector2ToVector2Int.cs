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
    [Opsive.Shared.Utility.Description("Converts a Vector2 to a Vector2Int by rounding each component to the nearest integer.")]
    public class ConvertVector2ToVector2Int : Action
    {
        [Tooltip("The Vector2 to convert.")]
        [SerializeField] protected SharedVariable<Vector2> m_InputVector;
        [Tooltip("The resulting Vector2Int value.")]
        [SerializeField] [RequireShared] protected SharedVariable<Vector2Int> m_OutputVector;

        /// <summary>
        /// Converts the Vector2 to a Vector2Int.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            m_OutputVector.Value = Vector2Int.RoundToInt(m_InputVector.Value);

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_InputVector = null;
            m_OutputVector = null;
        }
    }
}
#endif