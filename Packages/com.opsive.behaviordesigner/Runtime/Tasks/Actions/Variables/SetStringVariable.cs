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

    [Opsive.Shared.Utility.Description("Sets the value of a string SharedVariable by name.")]
    [Shared.Utility.Category("Variables")]
    public class SetStringVariable : SetVariableBase
    {
        [Tooltip("The name of the string SharedVariable.")]
        [SerializeField] protected SharedVariable<string> m_VariableName;
        [Tooltip("The string value to set.")]
        [SerializeField] protected SharedVariable<string> m_Value;

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
            m_Value = "";
        }
    }

    /// <summary>
    /// Gets the value of a string SharedVariable by name.
    /// </summary>
    [Opsive.Shared.Utility.Description("Gets the value of a string SharedVariable by name.")]
    [Shared.Utility.Category("Variables")]
    public class GetStringVariable : GetVariableBase
    {
        [Tooltip("The name of the string SharedVariable.")]
        [SerializeField] protected SharedVariable<string> m_VariableName;
        [Tooltip("The string variable to store the retrieved value.")]
        [RequireShared] [SerializeField] protected SharedVariable<string> m_Destination;

        /// <summary>
        /// Executes the task.
        /// </summary>
        /// <returns>The execution status of the task.</returns>
        public override TaskStatus OnUpdate()
        {
            var source = GetVariable<string>(m_VariableName);
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