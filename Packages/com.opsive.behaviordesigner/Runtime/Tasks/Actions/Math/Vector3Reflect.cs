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
    [Opsive.Shared.Utility.Description("Reflects a Vector3 off a normal vector.")]
    public class Vector3Reflect : Action
    {
        [Tooltip("The Vector3 direction to reflect.")]
        [SerializeField] protected SharedVariable<Vector3> m_Direction;
        [Tooltip("The normal vector to reflect off of.")]
        [SerializeField] protected SharedVariable<Vector3> m_Normal;
        [Tooltip("The resulting reflected Vector3.")]
        [SerializeField] [RequireShared] protected SharedVariable<Vector3> m_Result;

        /// <summary>
        /// Reflects the Vector3 off the normal.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            m_Result.Value = Vector3.Reflect(m_Direction.Value, m_Normal.Value);

            return TaskStatus.Success;
        }
    }
}
#endif