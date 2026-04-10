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
    [Opsive.Shared.Utility.Description("Returns the normalized (unit length) version of a Vector3.")]
    public class Vector3Normalized : Action
    {
        [Tooltip("The Vector3 to normalize.")]
        [SerializeField] protected SharedVariable<Vector3> m_InputVector;
        [Tooltip("The resulting normalized Vector3.")]
        [SerializeField] [RequireShared] protected SharedVariable<Vector3> m_OutputVector;

        /// <summary>
        /// Normalizes the Vector3.
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