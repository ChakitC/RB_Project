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
    /// Compares a PlayerPrefs string value with another value.
    /// </summary>
    [Opsive.Shared.Utility.Category("Save//Load")]
    [Opsive.Shared.Utility.Description("Compares a PlayerPrefs string value with another value.")]
    public class PlayerPrefsStringComparison : Conditional
    {
        /// <summary>
        /// Specifies the type of comparison that should be performed.
        /// </summary>
        public enum Operation
        {
            EqualTo,        // Equal to.
            NotEqualTo,     // Not equal to.
            Contains,       // Contains substring.
            StartsWith,     // Starts with substring.
            EndsWith,       // Ends with substring.
            IsNullOrEmpty   // Is null or empty.
        }

        [Tooltip("The PlayerPrefs key to read.")]
        [SerializeField] protected SharedVariable<string> m_Key;
        [Tooltip("The operation that should be performed.")]
        [SerializeField] protected Operation m_Operation = Operation.EqualTo;
        [Tooltip("The value to compare against.")]
        [SerializeField] protected SharedVariable<string> m_CompareValue;
        [Tooltip("The default value to use if the key doesn't exist.")]
        [SerializeField] protected SharedVariable<string> m_DefaultValue;
        [Tooltip("Should the comparison be case-sensitive?")]
        [SerializeField] protected bool m_CaseSensitive = true;

        /// <summary>
        /// Executes the conditional.
        /// </summary>
        public override TaskStatus OnUpdate()
        {
            if (string.IsNullOrEmpty(m_Key.Value)) {
                return TaskStatus.Failure;
            }

            var playerPrefsValue = PlayerPrefs.GetString(m_Key.Value, m_DefaultValue.Value);
            var comparisonType = m_CaseSensitive ? System.StringComparison.Ordinal : System.StringComparison.OrdinalIgnoreCase;

            bool valid;
            switch (m_Operation) {
                case Operation.EqualTo:
                    if (playerPrefsValue == null && m_CompareValue.Value == null) {
                        valid = true;
                    } else if (playerPrefsValue == null || m_CompareValue.Value == null) {
                        valid = false;
                    } else {
                        valid = playerPrefsValue.Equals(m_CompareValue.Value, comparisonType);
                    }
                    break;
                case Operation.NotEqualTo:
                    if (playerPrefsValue == null && m_CompareValue.Value == null) {
                        valid = false;
                    } else if (playerPrefsValue == null || m_CompareValue.Value == null) {
                        valid = true;
                    } else {
                        valid = !playerPrefsValue.Equals(m_CompareValue.Value, comparisonType);
                    }
                    break;
                case Operation.Contains:
                    valid = !string.IsNullOrEmpty(playerPrefsValue) && !string.IsNullOrEmpty(m_CompareValue.Value) && playerPrefsValue.IndexOf(m_CompareValue.Value, comparisonType) >= 0;
                    break;
                case Operation.StartsWith:
                    valid = !string.IsNullOrEmpty(playerPrefsValue) && !string.IsNullOrEmpty(m_CompareValue.Value) && playerPrefsValue.StartsWith(m_CompareValue.Value, comparisonType);
                    break;
                case Operation.EndsWith:
                    valid = !string.IsNullOrEmpty(playerPrefsValue) && !string.IsNullOrEmpty(m_CompareValue.Value) && playerPrefsValue.EndsWith(m_CompareValue.Value, comparisonType);
                    break;
                case Operation.IsNullOrEmpty:
                    valid = string.IsNullOrEmpty(playerPrefsValue);
                    break;
                default:
                    valid = false;
                    break;
            }

            return valid ? TaskStatus.Success : TaskStatus.Failure;
        }
    }
}
#endif