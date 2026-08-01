#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Utility
{
    using System;
    using Opsive.BehaviorDesigner.Runtime.Components;
    using Opsive.BehaviorDesigner.Runtime.Groups;
    using Opsive.BehaviorDesigner.Runtime.Systems;
    using Unity.Entities;
    using UnityEngine;

    /// <summary>
    /// Systems assembly implementation for the runtime traversal bridge.
    /// </summary>
    public sealed class BehaviorTreeExecutionController : IBehaviorTreeExecutionController
    {
        /// <summary>
        /// Registers the controller when Unity reloads runtime subsystems.
        /// </summary>
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterController()
        {
            BehaviorTreeExecution.Register(new BehaviorTreeExecutionController());
        }

        /// <summary>
        /// Returns (creating if necessary) the behavior tree system group.
        /// </summary>
        /// <param name="world">The ECS world.</param>
        /// <returns>The behavior tree system group.</returns>
        public ComponentSystemGroup GetOrCreateBehaviorTreeGroup(World world)
        {
            return world.GetOrCreateSystemManaged<BehaviorTreeSystemGroup>();
        }

        /// <summary>
        /// Returns the existing behavior tree system group without creating one.
        /// </summary>
        /// <param name="world">The ECS world.</param>
        /// <returns>The existing behavior tree system group (can be null).</returns>
        public ComponentSystemGroup GetExistingBehaviorTreeGroup(World world)
        {
            return world.GetExistingSystemManaged<BehaviorTreeSystemGroup>();
        }

        /// <summary>
        /// Returns (creating if necessary) the before-traversal system group.
        /// </summary>
        /// <param name="world">The ECS world.</param>
        /// <returns>The before-traversal system group.</returns>
        public ComponentSystemGroup GetOrCreateBeforeTraversalGroup(World world)
        {
            return world.GetOrCreateSystemManaged<BeforeTraversalSystemGroup>();
        }

        /// <summary>
        /// Returns (creating if necessary) the traversal task system group.
        /// </summary>
        /// <param name="world">The ECS world.</param>
        /// <returns>The traversal task system group.</returns>
        public ComponentSystemGroup GetOrCreateTraversalTaskGroup(World world)
        {
            return world.GetOrCreateSystemManaged<TraversalTaskSystemGroup>();
        }

        /// <summary>
        /// Returns (creating if necessary) the reevaluate task system group.
        /// </summary>
        /// <param name="world">The ECS world.</param>
        /// <returns>The reevaluate task system group.</returns>
        public ComponentSystemGroup GetOrCreateReevaluateTaskGroup(World world)
        {
            return world.GetOrCreateSystemManaged<ReevaluateTaskSystemGroup>();
        }

        /// <summary>
        /// Returns (creating if necessary) the interrupt task system group.
        /// </summary>
        /// <param name="world">The ECS world.</param>
        /// <returns>The interrupt task system group.</returns>
        public ComponentSystemGroup GetOrCreateInterruptTaskGroup(World world)
        {
            return world.GetOrCreateSystemManaged<InterruptTaskSystemGroup>();
        }

        /// <summary>
        /// Adds the core cleanup systems to the behavior tree group.
        /// </summary>
        /// <param name="world">The ECS world.</param>
        /// <param name="behaviorTreeSystemGroup">The behavior tree system group.</param>
        public void AddCleanupSystems(World world, ComponentSystemGroup behaviorTreeSystemGroup)
        {
            behaviorTreeSystemGroup.AddSystemToUpdateList(world.GetOrCreateSystem<EvaluationCleanupSystem>());
            behaviorTreeSystemGroup.AddSystemToUpdateList(world.GetOrCreateSystem<InterruptedCleanupSystem>());
        }

        /// <summary>
        /// Adds the reevaluation system to the before-traversal group.
        /// </summary>
        /// <param name="world">The ECS world.</param>
        /// <param name="beforeTraversalSystemGroup">The before-traversal group.</param>
        public void AddReevaluateSystem(World world, ComponentSystemGroup beforeTraversalSystemGroup)
        {
            beforeTraversalSystemGroup.AddSystemToUpdateList(world.GetOrCreateSystem(typeof(ReevaluateSystem)));
        }

        /// <summary>
        /// Adds the TaskObject system to the traversal task group.
        /// </summary>
        /// <param name="world">The ECS world.</param>
        /// <param name="traversalTaskSystemGroup">The traversal task group.</param>
        public void AddTaskObjectSystem(World world, ComponentSystemGroup traversalTaskSystemGroup)
        {
            traversalTaskSystemGroup.AddSystemToUpdateList(world.GetOrCreateSystem(typeof(TaskObjectSystem)));
        }

        /// <summary>
        /// Enables the baked behavior tree system.
        /// </summary>
        /// <param name="world">The ECS world.</param>
        public void EnableBakedBehaviorTreeSystem(World world)
        {
            world.Unmanaged.GetExistingSystemState<StartBakedBehaviorTreeSystem>().Enabled = true;
        }

        /// <summary>
        /// Enables the baked behavior tree system.
        /// </summary>
        /// <param name="world">The unmanaged ECS world.</param>
        public void EnableBakedBehaviorTreeSystem(WorldUnmanaged world)
        {
            world.GetExistingSystemState<StartBakedBehaviorTreeSystem>().Enabled = true;
        }

        /// <summary>
        /// Returns the TaskObject reevaluation system type.
        /// </summary>
        public Type TaskObjectReevaluateSystemType { get => typeof(TaskObjectReevaluateSystem); }

        /// <summary>
        /// Completes one traversal loop and returns true if another evaluation pass should run.
        /// </summary>
        /// <param name="world">The ECS world.</param>
        /// <param name="entityManager">The entity manager.</param>
        /// <returns>True if the traversal group should evaluate again.</returns>
        public bool CompleteTraversal(World world, EntityManager entityManager)
        {
            var determineEvaluationSystemHandle = world.GetExistingSystem<DetermineEvaluationSystem>();
            var determineEvaluationSystem = entityManager.WorldUnmanaged.GetUnsafeSystemRef<DetermineEvaluationSystem>(determineEvaluationSystemHandle);
            determineEvaluationSystem.Complete(entityManager);
            var evaluate = determineEvaluationSystem.Evaluate;

            CompleteEvaluation(world, entityManager, false);

            return evaluate;
        }

        /// <summary>
        /// Completes the evaluation system.
        /// </summary>
        /// <param name="world">The ECS world.</param>
        /// <param name="entityManager">The entity manager.</param>
        /// <param name="stopRunning">Is the traversal group stopping?</param>
        public void CompleteEvaluation(World world, EntityManager entityManager, bool stopRunning)
        {
            var evaluationSystemHandle = world.GetExistingSystem<EvaluationSystem>();
            var evaluationSystem = entityManager.WorldUnmanaged.GetUnsafeSystemRef<EvaluationSystem>(evaluationSystemHandle);
            evaluationSystem.Complete(entityManager, stopRunning);
        }

        /// <summary>
        /// Returns true if any behavior tree is still active.
        /// </summary>
        /// <param name="world">The ECS world.</param>
        /// <param name="entityManager">The entity manager.</param>
        /// <returns>True if any behavior tree is still active.</returns>
        public bool IsActive(World world, EntityManager entityManager)
        {
            var determineEvaluationSystemHandle = world.GetExistingSystem<DetermineEvaluationSystem>();
            var determineEvaluationSystem = entityManager.WorldUnmanaged.GetUnsafeSystemRef<DetermineEvaluationSystem>(determineEvaluationSystemHandle);
            return determineEvaluationSystem.Active;
        }
    }
}
#endif