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
    [Opsive.Shared.Utility.Description("Compares the distance between two positions or GameObjects.")]
    public class DistanceComparison : Conditional
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

        /// <summary>
        /// Specifies the source type for the first position.
        /// </summary>
        public enum PositionSourceType
        {
            GameObject,  // Use GameObject position.
            Vector3     // Use Vector3 position.
        }

        [Tooltip("The operation that should be performed.")]
        [SerializeField] protected Operation m_Operation = Operation.LessThan;
        [Tooltip("The source type for the first position.")]
        [SerializeField] protected PositionSourceType m_Source1Type = PositionSourceType.GameObject;
        [Tooltip("The first GameObject. Used if Source 1 Type is GameObject.")]
        [SerializeField] protected SharedVariable<GameObject> m_GameObject1;
        [Tooltip("The first Vector3 position. Used if Source 1 Type is Vector3.")]
        [SerializeField] protected SharedVariable<Vector3> m_Position1;
        [Tooltip("The source type for the second position.")]
        [SerializeField] protected PositionSourceType m_Source2Type = PositionSourceType.GameObject;
        [Tooltip("The second GameObject. Used if Source 2 Type is GameObject.")]
        [SerializeField] protected SharedVariable<GameObject> m_GameObject2;
        [Tooltip("The second Vector3 position. Used if Source 2 Type is Vector3.")]
        [SerializeField] protected SharedVariable<Vector3> m_Position2;
        [Tooltip("The distance to compare against.")]
        [SerializeField] protected SharedVariable<float> m_Distance;

        /// <summary>
        /// Executes the conditional.
        /// </summary>
        public override TaskStatus OnUpdate()
        {
            Vector3 position1;
            if (m_Source1Type == PositionSourceType.GameObject) {
                var gameObject1 = m_GameObject1.Value == null ? m_GameObject : m_GameObject1.Value;
                if (gameObject1 == null) {
                    return TaskStatus.Failure;
                }
                position1 = gameObject1.transform.position;
            } else {
                position1 = m_Position1.Value;
            }

            Vector3 position2;
            if (m_Source2Type == PositionSourceType.GameObject) {
                if (m_GameObject2.Value == null) {
                    return TaskStatus.Failure;
                }
                position2 = m_GameObject2.Value.transform.position;
            } else {
                position2 = m_Position2.Value;
            }

            var distance = Vector3.Distance(position1, position2);

            switch (m_Operation) {
                case Operation.LessThan:
                    return distance < m_Distance.Value ? TaskStatus.Success : TaskStatus.Failure;
                case Operation.LessThanOrEqualTo:
                    return distance <= m_Distance.Value ? TaskStatus.Success : TaskStatus.Failure;
                case Operation.EqualTo:
                    return Mathf.Approximately(distance, m_Distance.Value) ? TaskStatus.Success : TaskStatus.Failure;
                case Operation.NotEqualTo:
                    return !Mathf.Approximately(distance, m_Distance.Value) ? TaskStatus.Success : TaskStatus.Failure;
                case Operation.GreaterThanOrEqualTo:
                    return distance >= m_Distance.Value ? TaskStatus.Success : TaskStatus.Failure;
                case Operation.GreaterThan:
                    return distance > m_Distance.Value ? TaskStatus.Success : TaskStatus.Failure;
            }

            return TaskStatus.Failure;
        }
    }
}
#endif