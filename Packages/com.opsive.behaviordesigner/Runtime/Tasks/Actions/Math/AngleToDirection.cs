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
    [Opsive.Shared.Utility.Description("Converts an angle (in degrees) to a Vector2 direction.")]
    public class AngleToDirection : Action
    {
        [Tooltip("The angle in degrees.")]
        [SerializeField] protected SharedVariable<float> m_Angle;
        [Tooltip("The resulting Vector2 direction.")]
        [SerializeField] [RequireShared] protected SharedVariable<Vector2> m_Result;

        /// <summary>
        /// Converts the angle to a direction.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
var radians = m_Angle.Value * Mathf.Deg2Rad;
            m_Result.Value = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));

            return TaskStatus.Success;
        }
    }
}
#endif