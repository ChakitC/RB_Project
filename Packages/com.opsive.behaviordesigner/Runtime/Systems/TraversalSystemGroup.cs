#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Groups
{
    using Opsive.BehaviorDesigner.Runtime.Utility;
    using Unity.Entities;

    /// <summary>
    /// Main group for the systems that are responsible for traversing the behavior tree.
    /// </summary>
    [UpdateInGroup(typeof(BehaviorTreeSystemGroup))]
    [UpdateAfter(typeof(InterruptSystemGroup))]
    public partial class TraversalSystemGroup : ComponentSystemGroup
    {
        /// <summary>
        /// Updates the systems. Determines if the systems need to keep being evaluated.
        /// </summary>
        protected override void OnUpdate()
        {
#if UNITY_EDITOR
            var count = 0;
#endif
            // While the traversal is running a task may restart its own behavior tree (for example a task that toggles
            // the tree's GameObject active state, firing OnDisable/OnEnable). Flag that a traversal is in progress so
            // the restart begins from a fresh state on the next tick instead of re-entering the current traversal.
            BehaviorTreeExecution.IsTraversing = true;
            try {
                bool evaluate;
                do {
                    base.OnUpdate();

                    evaluate = BehaviorTreeExecution.CompleteTraversal(World, EntityManager);

#if UNITY_EDITOR
                    if (evaluate) {
                        count++;
                        if (count == ushort.MaxValue / 10) {
                            UnityEngine.Debug.LogWarning("An infinite loop would have been caused by the TraversalSystemGroup. Please email support@opsive.com with steps to reproduce this error.");
                            break;
                        }
                    }
#endif
                } while (evaluate);
            } finally {
                BehaviorTreeExecution.IsTraversing = false;
            }
        }

        /// <summary>
        /// The group has stopped running.
        /// </summary>
        protected override void OnStopRunning()
        {
            base.OnStopRunning();

            BehaviorTreeExecution.CompleteEvaluation(World, EntityManager, true);
        }
    }
}
#endif