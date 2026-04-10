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
    [Opsive.Shared.Utility.Description("Calculates the signed angle (in degrees) between two Vector3 values. Returns negative for clockwise, positive for counter-clockwise.")]
    public class SignedAngle : Action
    {
        [Tooltip("The first Vector3 value.")]
        [SerializeField] protected SharedVariable<Vector3> m_From;
        [Tooltip("The second Vector3 value.")]
        [SerializeField] protected SharedVariable<Vector3> m_To;
        [Tooltip("The axis to measure the angle around.")]
        [SerializeField] protected SharedVariable<Vector3> m_Axis = Vector3.up;
        [Tooltip("The resulting signed angle in degrees.")]
        [SerializeField] [RequireShared] protected SharedVariable<float> m_Result;

        /// <summary>
        /// Calculates the signed angle.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            m_Result.Value = Vector3.SignedAngle(m_From.Value, m_To.Value, m_Axis.Value);

            return TaskStatus.Success;
        }
    }
}
#endif