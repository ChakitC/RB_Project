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
    [Opsive.Shared.Utility.Description("Projects a Vector2 onto another Vector2.")]
    public class Vector2Project : Action
    {
        [Tooltip("The Vector2 to project.")]
        [SerializeField] protected SharedVariable<Vector2> m_Vector;
        [Tooltip("The Vector2 to project onto.")]
        [SerializeField] protected SharedVariable<Vector2> m_OnNormal;
        [Tooltip("The resulting projected Vector2.")]
        [SerializeField] [RequireShared] protected SharedVariable<Vector2> m_Result;

        /// <summary>
        /// Projects a Vector2 onto another Vector2.
        /// </summary>
        private Vector2 Project(Vector2 vector, Vector2 onNormal)
        {
            var sqrMagnitude = onNormal.sqrMagnitude;
            if (sqrMagnitude < Mathf.Epsilon) {
                return Vector2.zero;
            }
            return onNormal * (Vector2.Dot(vector, onNormal) / sqrMagnitude);
        }

        /// <summary>
        /// Projects the Vector2 onto the other Vector2.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            m_Result.Value = Project(m_Vector.Value, m_OnNormal.Value);

            return TaskStatus.Success;
        }
    }
}
#endif