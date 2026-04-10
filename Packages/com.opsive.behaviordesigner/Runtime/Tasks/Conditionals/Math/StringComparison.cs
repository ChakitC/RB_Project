#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Conditionals.Math
{
    using Opsive.GraphDesigner.Runtime;
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("Math")]
    [Opsive.Shared.Utility.Description("Compares two string values.")]
    public class StringComparison : Conditional
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

        [Tooltip("The operation that should be performed.")]
        [SerializeField] protected Operation m_Operation = Operation.EqualTo;
        [Tooltip("The first string.")]
        [SerializeField] protected SharedVariable<string> m_String1;
        [Tooltip("The second string.")]
        [SerializeField] protected SharedVariable<string> m_String2;
        [Tooltip("Should the comparison be case-sensitive?")]
        [SerializeField] protected bool m_CaseSensitive = true;

        /// <summary>
        /// Executes the conditional.
        /// </summary>
        public override TaskStatus OnUpdate()
        {
            var comparisonType = m_CaseSensitive ? System.StringComparison.Ordinal : System.StringComparison.OrdinalIgnoreCase;

            bool valid;
            switch (m_Operation) {
                case Operation.EqualTo:
                    if (m_String1.Value == null && m_String2.Value == null) {
                        valid = true;
                    } else if (m_String1.Value == null || m_String2.Value == null) {
                        valid = false;
                    } else {
                        valid = m_String1.Value.Equals(m_String2.Value, comparisonType);
                    }
                    break;
                case Operation.NotEqualTo:
                    if (m_String1.Value == null && m_String2.Value == null) {
                        valid = false;
                    } else if (m_String1.Value == null || m_String2.Value == null) {
                        valid = true;
                    } else {
                        valid = !m_String1.Value.Equals(m_String2.Value, comparisonType);
                    }
                    break;
                case Operation.Contains:
                    valid = !string.IsNullOrEmpty(m_String1.Value) && !string.IsNullOrEmpty(m_String2.Value) && m_String1.Value.IndexOf(m_String2.Value, comparisonType) >= 0;
                    break;
                case Operation.StartsWith:
                    valid = !string.IsNullOrEmpty(m_String1.Value) && !string.IsNullOrEmpty(m_String2.Value) && m_String1.Value.StartsWith(m_String2.Value, comparisonType);
                    break;
                case Operation.EndsWith:
                    valid = !string.IsNullOrEmpty(m_String1.Value) && !string.IsNullOrEmpty(m_String2.Value) && m_String1.Value.EndsWith(m_String2.Value, comparisonType);
                    break;
                case Operation.IsNullOrEmpty:
                    valid = string.IsNullOrEmpty(m_String1.Value);
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