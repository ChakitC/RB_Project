#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.Conversions
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("Conversions")]
    [Opsive.Shared.Utility.Description("Converts a Vector2 to a Vector3. The Z component can be specified.")]
    public class ConvertVector2ToVector3 : Action
    {
        [Tooltip("The Vector2 to convert.")]
        [SerializeField] protected SharedVariable<Vector2> m_InputVector;
        [Tooltip("The Z value to use for the Vector3. Default is 0.")]
        [SerializeField] protected SharedVariable<float> m_ZValue = 0f;
        [Tooltip("The resulting Vector3 value.")]
        [SerializeField] [RequireShared] protected SharedVariable<Vector3> m_OutputVector;

        /// <summary>
        /// Converts the Vector2 to a Vector3.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            m_OutputVector.Value = new Vector3(m_InputVector.Value.x, m_InputVector.Value.y, m_ZValue.Value);

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_InputVector = null;
            m_ZValue = 0f;
            m_OutputVector = null;
        }
    }
}
#endif