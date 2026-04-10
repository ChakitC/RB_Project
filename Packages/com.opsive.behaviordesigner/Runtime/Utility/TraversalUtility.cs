#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Utility
{
    using Opsive.BehaviorDesigner.Runtime.Components;
    using Unity.Burst;
    using Unity.Entities;

    /// <summary>
    /// Utility functions that are used throughout the behavior tree execution.
    /// </summary>
    [BurstCompile]
    public static class TraversalUtility
    {
        /// <summary>
        /// Returns true if the specified index is a child of the parent index.
        /// </summary>
        /// <param name="index">The index to determine if it is a child.</param>
        /// <param name="parentIndex">The index of the parent.</param>
        /// <param name="taskComponents">The array of nodes.</param>
        /// <returns>True if the specified index is a child of the parent index.</returns>
        [BurstCompile]
        public static bool IsParent(ushort index, ushort parentIndex, ref DynamicBuffer<TaskComponent> taskComponents)
        {
            if (parentIndex == ushort.MaxValue || index == ushort.MaxValue) {
                return false;
            }

            // The child can be considered a parent of itself.
            if (parentIndex == index) {
                return true;
            }

            var parentTaskComponent = taskComponents[parentIndex];
            // Fast path: ChildUpperIndex stores the last descendant index for range checks.
            if (parentTaskComponent.ChildUpperIndex > parentTaskComponent.Index) {
                return index > parentTaskComponent.Index && index <= parentTaskComponent.ChildUpperIndex;
            }
            // ChildUpperIndex may still be uninitialized on deserialized trees. Fall back to parent walking.
            if (parentTaskComponent.ChildUpperIndex == parentTaskComponent.Index) {
                if (parentTaskComponent.Index + 1 >= taskComponents.Length || taskComponents[parentTaskComponent.Index + 1].ParentIndex != parentTaskComponent.Index) {
                    return false;
                }
            }

            // Return true as soon as there is a parent.
            while (index != ushort.MaxValue) {
                if (index == parentIndex) {
                    return true;
                }

                index = taskComponents[index].ParentIndex;
            }

            return false;
        }

        /// <summary>
        /// Returns the total number of children belonging to the specified node.
        /// </summary>
        /// <param name="index">The index of the task to retrieve the child count of.</param>
        /// <param name="taskComponents">The array of nodes.</param>
        /// <returns>The total number of children belonging to the specified node.</returns>
        public static int GetChildCount(int index, ref DynamicBuffer<TaskComponent> taskComponents)
        {
            if (index == ushort.MaxValue) {
                return 0;
            }

            var taskComponent = taskComponents[index];
            // Fast path: ChildUpperIndex represents the inclusive descendant upper bound.
            if (taskComponent.ChildUpperIndex > taskComponent.Index) {
                return taskComponent.ChildUpperIndex - taskComponent.Index;
            }
            // ChildUpperIndex can be equal to Index before initialization. Fall back to parent walking.
            if (taskComponent.ChildUpperIndex == taskComponent.Index) {
                if (taskComponent.Index + 1 >= taskComponents.Length || taskComponents[taskComponent.Index + 1].ParentIndex != taskComponent.Index) {
                    return 0;
                }
            }
            if (taskComponent.SiblingIndex != ushort.MaxValue) {
                return taskComponent.SiblingIndex - taskComponent.Index - 1;
            }

            if (taskComponent.Index + 1 == taskComponents.Length) {
                return 0;
            }

            var childTaskComponent = taskComponents[taskComponent.Index + 1];
            if (childTaskComponent.ParentIndex != taskComponent.Index) {
                return 0;
            }

            // Determine the child count based off of the sibling index.
            var lastChildTaskComponent = childTaskComponent;
            while (childTaskComponent.ParentIndex == taskComponent.Index) {
                lastChildTaskComponent = childTaskComponent;
                if (childTaskComponent.SiblingIndex == ushort.MaxValue) {
                    break;
                }
                childTaskComponent = taskComponents[childTaskComponent.SiblingIndex];
            }

            return lastChildTaskComponent.Index - taskComponent.Index + GetChildCount(lastChildTaskComponent.Index, ref taskComponents);
        }

        /// <summary>
        /// Populates each task's child upper bound index.
        /// </summary>
        /// <param name="taskComponents">The task buffer that should be updated.</param>
        [BurstCompile]
        public static void PopulateChildUpperIndices(ref DynamicBuffer<TaskComponent> taskComponents)
        {
            for (int i = 0; i < taskComponents.Length; ++i) {
                var taskComponent = taskComponents[i];
                var childCount = GetChildCount(taskComponent.Index, ref taskComponents);
                taskComponent.ChildUpperIndex = (ushort)(taskComponent.Index + childCount);
                taskComponents[i] = taskComponent;
            }
        }

        /// <summary>
        /// Returns the immediate number of children belonging to the specified task.
        /// </summary>
        /// <param name="task">The task to retrieve the children of.</param>
        /// <param name="taskComponents">The list of tasks.</param>
        /// <returns>The number of immediate children belonging to the specified task.</returns>
        [BurstCompile]
        public static int GetImmediateChildCount(ref TaskComponent task, ref DynamicBuffer<TaskComponent> taskComponents)
        {
            var count = 0;
            var siblingIndex = task.Index + 1;
            while (siblingIndex < taskComponents.Length && taskComponents[siblingIndex].ParentIndex == task.Index) {
                count++;
                siblingIndex = taskComponents[siblingIndex].SiblingIndex;
            }
            return count;
        }
    }
}
#endif