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
    [Opsive.Shared.Utility.Description("Rotates a Vector2 by an angle in degrees.")]
    public class Vector2Rotate : Action
    {
        [Tooltip("The Vector2 to rotate.")]
        [SerializeField] protected SharedVariable<Vector2> m_Vector;
        [Tooltip("The angle in degrees to rotate by.")]
        [SerializeField] protected SharedVariable<float> m_Angle;
        [Tooltip("The resulting rotated Vector2.")]
        [SerializeField] [RequireShared] protected SharedVariable<Vector2> m_Result;

        /// <summary>
        /// Rotates the Vector2.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            var radians = m_Angle.Value * Mathf.Deg2Rad;
            var cos = Mathf.Cos(radians);
            var sin = Mathf.Sin(radians);

            m_Result.Value = new Vector2(
                m_Vector.Value.x * cos - m_Vector.Value.y * sin,
                m_Vector.Value.x * sin + m_Vector.Value.y * cos
            );

            return TaskStatus.Success;
        }
    }
}
#endif