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
    [Opsive.Shared.Utility.Description("Converts an int to a Vector2 by setting X, Y, or both components to the int value.")]
    public class ConvertIntToVector2 : Action
    {
        /// <summary>
        /// Specifies which component(s) to set in the Vector2.
        /// </summary>
        public enum SetComponent
        {
            SetX,       // Set only the X component (Y remains 0).
            SetY,       // Set only the Y component (X remains 0).
            SetBoth     // Set both X and Y to the int value.
        }

        [Tooltip("The int to convert.")]
        [SerializeField] protected SharedVariable<int> m_InputValue;
        [Tooltip("Specifies which component(s) to set.")]
        [SerializeField] protected SetComponent m_SetComponent = SetComponent.SetX;
        [Tooltip("The resulting Vector2 value.")]
        [SerializeField] [RequireShared] protected SharedVariable<Vector2> m_OutputVector;

        /// <summary>
        /// Converts the int to a Vector2.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            switch (m_SetComponent) {
                case SetComponent.SetX:
                    m_OutputVector.Value = new Vector2(m_InputValue.Value, 0f);
                    break;
                case SetComponent.SetY:
                    m_OutputVector.Value = new Vector2(0f, m_InputValue.Value);
                    break;
                case SetComponent.SetBoth:
                    m_OutputVector.Value = new Vector2(m_InputValue.Value, m_InputValue.Value);
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
            m_InputValue = null;
            m_SetComponent = SetComponent.SetX;
            m_OutputVector = null;
        }
    }
}
#endif