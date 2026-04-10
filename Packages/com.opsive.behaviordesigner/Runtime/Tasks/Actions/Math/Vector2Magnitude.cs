#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.Math
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("Math")]
    [Opsive.Shared.Utility.Description("Calculates the magnitude (length) of a Vector2.")]
    public class Vector2Magnitude : Action
    {
        [Tooltip("The Vector2 to calculate the magnitude of.")]
        [SerializeField] protected SharedVariable<Vector2> m_InputVector;
        [Tooltip("The resulting magnitude value.")]
        [SerializeField] [RequireShared] protected SharedVariable<float> m_OutputValue;

        /// <summary>
        /// Calculates the magnitude of the Vector2.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            m_OutputValue.Value = m_InputVector.Value.magnitude;

            return TaskStatus.Success;
        }
    }
}
#endif