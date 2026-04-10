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
    [Opsive.Shared.Utility.Description("Returns the normalized (unit length) version of a Vector2.")]
    public class Vector2Normalized : Action
    {
        [Tooltip("The Vector2 to normalize.")]
        [SerializeField] protected SharedVariable<Vector2> m_InputVector;
        [Tooltip("The resulting normalized Vector2.")]
        [SerializeField] [RequireShared] protected SharedVariable<Vector2> m_OutputVector;

        /// <summary>
        /// Normalizes the Vector2.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            m_OutputVector.Value = m_InputVector.Value.normalized;

            return TaskStatus.Success;
        }
    }
}
#endif