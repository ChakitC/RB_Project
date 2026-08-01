#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Editor.Controls.NodeViews
{
    using Opsive.BehaviorDesigner.Runtime;
    using Opsive.BehaviorDesigner.Runtime.Components;
    using Opsive.BehaviorDesigner.Runtime.Tasks;
    using Opsive.BehaviorDesigner.Runtime.Tasks.Composites;
    using Unity.Entities;

    /// <summary>
    /// Utility methods for converting runtime task state into editor display state.
    /// </summary>
    internal static class RuntimeNodeStatusUtility
    {
        /// <summary>
        /// Returns the task status that should be displayed by editor node views.
        /// </summary>
        /// <param name="behaviorTree">The behavior tree that owns the runtime node.</param>
        /// <param name="runtimeIndex">The runtime index of the node.</param>
        /// <returns>The task status that should be displayed.</returns>
        public static TaskStatus GetDisplayStatus(BehaviorTree behaviorTree, int runtimeIndex)
        {
            if (behaviorTree == null) {
                return TaskStatus.Inactive;
            }

            var rawStatus = behaviorTree.GetRuntimeNodeStatus(runtimeIndex);
            if (rawStatus != TaskStatus.Queued && rawStatus != TaskStatus.Running) {
                return rawStatus;
            }

            if (IsSelectorEvaluatorReevaluationTask(behaviorTree, runtimeIndex)) {
                return TaskStatus.Inactive;
            }

            return rawStatus;
        }

        /// <summary>
        /// Returns true if the runtime index belongs to a SelectorEvaluator child that is only being reevaluated.
        /// </summary>
        /// <param name="behaviorTree">The behavior tree that owns the runtime node.</param>
        /// <param name="runtimeIndex">The runtime index of the node.</param>
        /// <returns>True if the runtime index belongs to a SelectorEvaluator reevaluation subtree.</returns>
        private static bool IsSelectorEvaluatorReevaluationTask(BehaviorTree behaviorTree, int runtimeIndex)
        {
            if (behaviorTree == null || runtimeIndex < 0 || behaviorTree.Entity == Entity.Null || behaviorTree.World == null || !behaviorTree.World.IsCreated ||
                !behaviorTree.World.EntityManager.Exists(behaviorTree.Entity) ||
                !behaviorTree.World.EntityManager.HasBuffer<TaskComponent>(behaviorTree.Entity) ||
                !behaviorTree.World.EntityManager.HasBuffer<SelectorEvaluatorComponent>(behaviorTree.Entity)) {
                return false;
            }

            var taskComponents = behaviorTree.World.EntityManager.GetBuffer<TaskComponent>(behaviorTree.Entity);
            if (runtimeIndex >= taskComponents.Length) {
                return false;
            }

            var selectorEvaluatorComponents = behaviorTree.World.EntityManager.GetBuffer<SelectorEvaluatorComponent>(behaviorTree.Entity);
            return SelectorEvaluator.IsRuntimeIndexReevaluating((ushort)runtimeIndex, taskComponents, selectorEvaluatorComponents);
        }
    }
}
#endif