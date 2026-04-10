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
    [Opsive.Shared.Utility.Description("Scales a Vector3 by another Vector3 (component-wise multiplication).")]
    public class Vector3Scale : Action
    {
        [Tooltip("The Vector3 to scale.")]
        [SerializeField] protected SharedVariable<Vector3> m_Vector;
        [Tooltip("The Vector3 to scale by (component-wise).")]
        [SerializeField] protected SharedVariable<Vector3> m_Scale;
        [Tooltip("The resulting scaled Vector3.")]
        [SerializeField] [RequireShared] protected SharedVariable<Vector3> m_Result;

        /// <summary>
        /// Scales the Vector3.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            m_Result.Value = Vector3.Scale(m_Vector.Value, m_Scale.Value);

            return TaskStatus.Success;
        }
    }
}
#endif