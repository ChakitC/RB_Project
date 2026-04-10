#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.Math
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;
    using UnityEngine.Scripting.APIUpdating;

    [Tooltip("Returns a random integer between the specified values.")]
    [Shared.Utility.Category("Math")]
    [MovedFrom(false, "Opsive.BehaviorDesigner.Tasks.Actions.Math", "Opsive.BehaviorDesigner.Runtime", "RandomInteger")]
    public class RandomInt : Action
    {
        [Tooltip("The minimum integer value (inclusive).")]
        [SerializeField] protected SharedVariable<int> m_MinimumInteger;
        [Tooltip("The maximum integer value (exclusive).")]
        [SerializeField] protected SharedVariable<int> m_MaximumInteger;
        [Tooltip("The stored random integer value.")]
        [RequireShared] [SerializeField] protected SharedVariable<int> m_StoreResult;
        [Tooltip("The seed of the random number generator. Set to 0 to disable.")]
        [SerializeField] protected int m_Seed;

        /// <summary>
        /// Callback when the behavior tree is initialized.
        /// </summary>
        public override void OnAwake()
        {
            if (m_Seed != 0) {
                Random.InitState(m_Seed);
            }
        }

        /// <summary>
        /// Executes the task logic.
        /// </summary>
        /// <returns>The status of the task.</returns>
        public override TaskStatus OnUpdate()
        {
            m_StoreResult.Value = Random.Range(m_MinimumInteger.Value, m_MaximumInteger.Value);
            return base.OnUpdate();
        }

    }
}
#endif