#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.Variables
{
    using Opsive.GraphDesigner.Runtime;
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    [Opsive.Shared.Utility.Description("Sets the value of a Vector3 SharedVariable by name.")]
    [Shared.Utility.Category("Variables")]
    public class SetVector3Variable : SetVariableBase
    {
        [Tooltip("The name of the Vector3 SharedVariable.")]
        [SerializeField] protected SharedVariable<string> m_VariableName;
        [Tooltip("The Vector3 value to set.")]
        [SerializeField] protected SharedVariable<Vector3> m_Value;

        /// <summary>
        /// Executes the task.
        /// </summary>
        /// <returns>The execution status of the task.</returns>
        public override TaskStatus OnUpdate()
        {
            SetVariableValue(m_VariableName, m_Value.Value);
            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the task values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_VariableName = "";
            m_Value = Vector3.zero;
        }
    }

    /// <summary>
    /// Gets the value of a Vector3 SharedVariable by name.
    /// </summary>
    [Opsive.Shared.Utility.Description("Gets the value of a Vector3 SharedVariable by name.")]
    [Shared.Utility.Category("Variables")]
    public class GetVector3Variable : GetVariableBase
    {
        [Tooltip("The name of the Vector3 SharedVariable.")]
        [SerializeField] protected SharedVariable<string> m_VariableName;
        [Tooltip("The Vector3 variable to store the retrieved value.")]
        [RequireShared] [SerializeField] protected SharedVariable<Vector3> m_Destination;

        /// <summary>
        /// Executes the task.
        /// </summary>
        /// <returns>The execution status of the task.</returns>
        public override TaskStatus OnUpdate()
        {
            var source = GetVariable<Vector3>(m_VariableName);
            if (source != null) {
                m_Destination.Value = source.Value;
            }

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the task values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_VariableName = "";
            m_Destination = null;
        }
    }
}
#endif