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

    [Opsive.Shared.Utility.Description("Sets the value of a Transform SharedVariable by name.")]
    [Shared.Utility.Category("Variables")]
    public class SetTransformVariable : SetVariableBase
    {
        [Tooltip("The name of the Transform SharedVariable.")]
        [SerializeField] protected SharedVariable<string> m_VariableName;
        [Tooltip("The Transform value to set.")]
        [SerializeField] protected SharedVariable<Transform> m_Value;

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
            m_Value = null;
        }
    }

    /// <summary>
    /// Gets the value of a Transform SharedVariable by name.
    /// </summary>
    [Opsive.Shared.Utility.Description("Gets the value of a Transform SharedVariable by name.")]
    [Shared.Utility.Category("Variables")]
    public class GetTransformVariable : GetVariableBase
    {
        [Tooltip("The name of the Transform SharedVariable.")]
        [SerializeField] protected SharedVariable<string> m_VariableName;
        [Tooltip("The Transform variable to store the retrieved value.")]
        [RequireShared] [SerializeField] protected SharedVariable<Transform> m_Destination;

        /// <summary>
        /// Executes the task.
        /// </summary>
        /// <returns>The execution status of the task.</returns>
        public override TaskStatus OnUpdate()
        {
            var source = GetVariable<Transform>(m_VariableName);
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