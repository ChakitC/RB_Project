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
    [Opsive.Shared.Utility.Description("Generates a random Vector3 inside a unit sphere.")]
    public class RandomInsideUnitSphere : Action
    {
        [Tooltip("The resulting random Vector3 inside a unit sphere.")]
        [SerializeField] [RequireShared] protected SharedVariable<Vector3> m_Result;

        /// <summary>
        /// Generates a random Vector3 inside a unit sphere.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            m_Result.Value = Random.insideUnitSphere;

            return TaskStatus.Success;
        }
    }
}
#endif