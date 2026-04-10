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
    [Opsive.Shared.Utility.Description("Rotates a Vector3 around an axis by an angle in degrees.")]
    public class Vector3Rotate : Action
    {
        [Tooltip("The Vector3 to rotate.")]
        [SerializeField] protected SharedVariable<Vector3> m_Vector;
        [Tooltip("The axis to rotate around.")]
        [SerializeField] protected SharedVariable<Vector3> m_Axis = Vector3.up;
        [Tooltip("The angle in degrees to rotate by.")]
        [SerializeField] protected SharedVariable<float> m_Angle;
        [Tooltip("The resulting rotated Vector3.")]
        [SerializeField] [RequireShared] protected SharedVariable<Vector3> m_Result;

        /// <summary>
        /// Rotates the Vector3.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            var axis = m_Axis.Value.normalized;

            m_Result.Value = Quaternion.AngleAxis(m_Angle.Value, axis) * m_Vector.Value;

            return TaskStatus.Success;
        }
    }
}
#endif