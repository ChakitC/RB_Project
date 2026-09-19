#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.Math
{
    using Opsive.GraphDesigner.Runtime;
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    /// <summary>
    /// Performs a component-wise arithmetic operation on two Vector2Int values.
    /// </summary>
    [Opsive.Shared.Utility.Description("Performs a component-wise arithmetic operation on two Vector2Int values.")]
    [Shared.Utility.Category("Math")]
    public class Vector2IntOperator : Action
    {
        /// <summary>
        /// Specifies the arithmetic operation to perform.
        /// </summary>
        protected enum Operation
        {
            Add,        // Add the two values.
            Subtract,   // Subtract the second value from the first.
            Multiply,   // Multiply the values component-wise.
            Divide      // Divide the first value by the second component-wise.
        }

        [Tooltip("The operation to perform.")]
        [SerializeField] protected SharedVariable<Operation> m_Operation;
        [Tooltip("The first Vector2Int value.")]
        [SerializeField] protected SharedVariable<Vector2Int> m_Vector1;
        [Tooltip("The second Vector2Int value.")]
        [SerializeField] protected SharedVariable<Vector2Int> m_Vector2;
        [Tooltip("The variable to store the result.")]
        [RequireShared] [SerializeField] protected SharedVariable<Vector2Int> m_StoreResult;

        /// <summary>
        /// Executes the task.
        /// </summary>
        /// <returns>The execution status of the task.</returns>
        public override TaskStatus OnUpdate()
        {
            var vector1 = m_Vector1.Value;
            var vector2 = m_Vector2.Value;

            switch (m_Operation.Value) {
                case Operation.Add:
                    m_StoreResult.Value = vector1 + vector2;
                    break;
                case Operation.Subtract:
                    m_StoreResult.Value = vector1 - vector2;
                    break;
                case Operation.Multiply:
                    m_StoreResult.Value = Vector2Int.Scale(vector1, vector2);
                    break;
                case Operation.Divide:
                    m_StoreResult.Value = new Vector2Int(
                        vector2.x != 0 ? vector1.x / vector2.x : 0,
                        vector2.y != 0 ? vector1.y / vector2.y : 0);
                    break;
            }

            return TaskStatus.Success;
        }
    }
}
#endif