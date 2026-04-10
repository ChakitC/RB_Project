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
    [Opsive.Shared.Utility.Description("Converts a float to a Vector3 by setting X, Y, Z, or all components to the float value.")]
    public class ConvertFloatToVector3 : Action
    {
        /// <summary>
        /// Specifies which component(s) to set in the Vector3.
        /// </summary>
        public enum SetComponent
        {
            SetX,       // Set only the X component (Y and Z remain 0).
            SetY,       // Set only the Y component (X and Z remain 0).
            SetZ,       // Set only the Z component (X and Y remain 0).
            SetAll      // Set all components (X, Y, Z) to the float value.
        }

        [Tooltip("The float to convert.")]
        [SerializeField] protected SharedVariable<float> m_InputValue;
        [Tooltip("Specifies which component(s) to set.")]
        [SerializeField] protected SetComponent m_SetComponent = SetComponent.SetX;
        [Tooltip("The resulting Vector3 value.")]
        [SerializeField] [RequireShared] protected SharedVariable<Vector3> m_OutputVector;

        /// <summary>
        /// Converts the float to a Vector3.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            switch (m_SetComponent) {
                case SetComponent.SetX:
                    m_OutputVector.Value = new Vector3(m_InputValue.Value, 0f, 0f);
                    break;
                case SetComponent.SetY:
                    m_OutputVector.Value = new Vector3(0f, m_InputValue.Value, 0f);
                    break;
                case SetComponent.SetZ:
                    m_OutputVector.Value = new Vector3(0f, 0f, m_InputValue.Value);
                    break;
                case SetComponent.SetAll:
                    m_OutputVector.Value = new Vector3(m_InputValue.Value, m_InputValue.Value, m_InputValue.Value);
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