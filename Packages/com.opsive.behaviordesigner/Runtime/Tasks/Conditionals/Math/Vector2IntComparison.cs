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

    /// <summary>
    /// Compares two Vector2Int values.
    /// </summary>
    [Opsive.Shared.Utility.Description("Compares two Vector2Int values.")]
    [Shared.Utility.Category("Math")]
    public class Vector2IntComparison : Conditional
    {
        /// <summary>
        /// Specifies the type of comparison that should be performed.
        /// </summary>
        protected enum Operation
        {
            LessThan,           // Less than (magnitude comparison).
            LessThanOrEqualTo,  // Less than or equal to (magnitude comparison).
            EqualTo,            // Equal to (component-wise).
            NotEqualTo,         // Not equal to (component-wise).
            GreaterThanOrEqualTo, // Greater than or equal to (magnitude comparison).
            GreaterThan         // Greater than (magnitude comparison).
        }

        [Tooltip("The operation that should be performed.")]
        [SerializeField] protected SharedVariable<Operation> m_Operation;
        [Tooltip("The first Vector2Int.")]
        [SerializeField] protected SharedVariable<Vector2Int> m_Vector1;
        [Tooltip("The second Vector2Int.")]
        [SerializeField] protected SharedVariable<Vector2Int> m_Vector2;

        /// <summary>
        /// Executes the task.
        /// </summary>
        /// <returns>The execution status of the task.</returns>
        public override TaskStatus OnUpdate()
        {
            var magnitude1 = m_Vector1.Value.magnitude;
            var magnitude2 = m_Vector2.Value.magnitude;

            switch (m_Operation.Value) {
                case Operation.LessThan:
                    return magnitude1 < magnitude2 ? TaskStatus.Success : TaskStatus.Failure;
                case Operation.LessThanOrEqualTo:
                    return magnitude1 <= magnitude2 ? TaskStatus.Success : TaskStatus.Failure;
                case Operation.EqualTo:
                    return m_Vector1.Value == m_Vector2.Value ? TaskStatus.Success : TaskStatus.Failure;
                case Operation.NotEqualTo:
                    return m_Vector1.Value != m_Vector2.Value ? TaskStatus.Success : TaskStatus.Failure;
                case Operation.GreaterThanOrEqualTo:
                    return magnitude1 >= magnitude2 ? TaskStatus.Success : TaskStatus.Failure;
                case Operation.GreaterThan:
                    return magnitude1 > magnitude2 ? TaskStatus.Success : TaskStatus.Failure;
            }

            return TaskStatus.Failure;
        }
    }
}
#endif