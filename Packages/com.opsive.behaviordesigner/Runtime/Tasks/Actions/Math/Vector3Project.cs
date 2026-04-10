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
    [Opsive.Shared.Utility.Description("Projects a Vector3 onto another Vector3.")]
    public class Vector3Project : Action
    {
        [Tooltip("The Vector3 to project.")]
        [SerializeField] protected SharedVariable<Vector3> m_Vector;
        [Tooltip("The Vector3 to project onto.")]
        [SerializeField] protected SharedVariable<Vector3> m_OnNormal;
        [Tooltip("The resulting projected Vector3.")]
        [SerializeField] [RequireShared] protected SharedVariable<Vector3> m_Result;

        /// <summary>
        /// Projects the Vector3 onto the other Vector3.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            m_Result.Value = Vector3.Project(m_Vector.Value, m_OnNormal.Value);

            return TaskStatus.Success;
        }
    }
}
#endif