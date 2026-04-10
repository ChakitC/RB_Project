#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Conditionals.SaveLoadTasks
{
    using Opsive.GraphDesigner.Runtime;
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    /// <summary>
    /// Compares a PlayerPrefs float value with another value.
    /// </summary>
    [Opsive.Shared.Utility.Category("Save//Load")]
    [Opsive.Shared.Utility.Description("Compares a PlayerPrefs float value with another value.")]
    public class PlayerPrefsFloatComparison : Conditional
    {
        /// <summary>
        /// Specifies the type of comparison that should be performed.
        /// </summary>
        public enum Operation
        {
            LessThan,           // Less than.
            LessThanOrEqualTo,  // Less than or equal to.
            EqualTo,            // Equal to.
            NotEqualTo,         // Not equal to.
            GreaterThanOrEqualTo, // Greater than or equal to.
            GreaterThan         // Greater than.
        }

        [Tooltip("The PlayerPrefs key to read.")]
        [SerializeField] protected SharedVariable<string> m_Key;
        [Tooltip("The operation that should be performed.")]
        [SerializeField] protected Operation m_Operation = Operation.EqualTo;
        [Tooltip("The value to compare against.")]
        [SerializeField] protected SharedVariable<float> m_CompareValue;
        [Tooltip("The default value to use if the key doesn't exist.")]
        [SerializeField] protected SharedVariable<float> m_DefaultValue;

        /// <summary>
        /// Executes the conditional.
        /// </summary>
        public override TaskStatus OnUpdate()
        {
            if (string.IsNullOrEmpty(m_Key.Value)) {
                return TaskStatus.Failure;
            }

            var playerPrefsValue = PlayerPrefs.GetFloat(m_Key.Value, m_DefaultValue.Value);

            switch (m_Operation) {
                case Operation.LessThan:
                    return playerPrefsValue < m_CompareValue.Value ? TaskStatus.Success : TaskStatus.Failure;
                case Operation.LessThanOrEqualTo:
                    return playerPrefsValue <= m_CompareValue.Value ? TaskStatus.Success : TaskStatus.Failure;
                case Operation.EqualTo:
                    return playerPrefsValue == m_CompareValue.Value ? TaskStatus.Success : TaskStatus.Failure;
                case Operation.NotEqualTo:
                    return playerPrefsValue != m_CompareValue.Value ? TaskStatus.Success : TaskStatus.Failure;
                case Operation.GreaterThanOrEqualTo:
                    return playerPrefsValue >= m_CompareValue.Value ? TaskStatus.Success : TaskStatus.Failure;
                case Operation.GreaterThan:
                    return playerPrefsValue > m_CompareValue.Value ? TaskStatus.Success : TaskStatus.Failure;
            }

            return TaskStatus.Failure;
        }
    }
}
#endif