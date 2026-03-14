#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Utility
{
    using Opsive.BehaviorDesigner.Runtime.Components;
    using Unity.Collections;
    using Unity.Entities;

    /// <summary>
    /// Utility functions that are used throughout the behavior tree execution.
    /// </summary>
    public static class ComponentUtility
    {
        /// <summary>
        /// The size of a ulong in bits.
        /// </summary>
        public static int ulongBitSize => sizeof(ulong) * 8;

        /// <summary>
        /// Adds an evaluation component to the specified entity based on the task count and evaluation type.
        /// </summary>
        /// <param name="world">The ECS world to add the component to.</param>
        /// <param name="entity">The entity to add the evaluation component to.</param>
        /// <param name="taskCount">The total number of tasks in the behavior tree.</param>
        /// <param name="evaluationType">The type of evaluation to perform.</param>
        /// <param name="maxEvaluationCount">The maximum number of evaluations allowed.</param>
        public static void AddEvaluationComponent(World world, Entity entity, int taskCount, EvaluationType evaluationType, int maxEvaluationCount)
        {
            if (taskCount < 192) {
                world.EntityManager.AddComponent<EvaluationComponent32>(entity);
                var evaluatedTasks = new FixedList32Bytes<ulong>();
                if (evaluationType == EvaluationType.EntireTree) {
                    for (int i = 0; i <= taskCount / ulongBitSize; ++i) {
                        evaluatedTasks.Add(0);
                    }
                } else {
                    evaluatedTasks.Add(0);
                }
                world.EntityManager.AddComponentData(entity, new EvaluationComponent32() { EvaluationType = evaluationType, MaxEvaluationCount = (ushort)UnityEngine.Mathf.Max(1, maxEvaluationCount), EvaluatedTasks = evaluatedTasks });
            } else if (taskCount < 448) {
                world.EntityManager.AddComponent<EvaluationComponent64>(entity);
                var evaluatedTasks = new FixedList64Bytes<ulong>();
                if (evaluationType == EvaluationType.EntireTree) {
                    for (int i = 0; i <= taskCount / ulongBitSize; ++i) {
                        evaluatedTasks.Add(0);
                    }
                } else {
                    evaluatedTasks.Add(0);
                }
                world.EntityManager.AddComponentData(entity, new EvaluationComponent64() { EvaluationType = evaluationType, MaxEvaluationCount = (ushort)UnityEngine.Mathf.Max(1, maxEvaluationCount), EvaluatedTasks = evaluatedTasks });
            } else if (taskCount < 960) {
                world.EntityManager.AddComponent<EvaluationComponent128>(entity);
                var evaluatedTasks = new FixedList128Bytes<ulong>();
                if (evaluationType == EvaluationType.EntireTree) {
                    for (int i = 0; i <= taskCount / ulongBitSize; ++i) {
                        evaluatedTasks.Add(0);
                    }
                } else {
                    evaluatedTasks.Add(0);
                }
                world.EntityManager.AddComponentData(entity, new EvaluationComponent128() { EvaluationType = evaluationType, MaxEvaluationCount = (ushort)UnityEngine.Mathf.Max(1, maxEvaluationCount), EvaluatedTasks = evaluatedTasks });
            } else if (taskCount < 4032) {
                world.EntityManager.AddComponent<EvaluationComponent512>(entity);
                var evaluatedTasks = new FixedList512Bytes<ulong>();
                if (evaluationType == EvaluationType.EntireTree) {
                    for (int i = 0; i <= taskCount / ulongBitSize; ++i) {
                        evaluatedTasks.Add(0);
                    }
                } else {
                    evaluatedTasks.Add(0);
                }
                world.EntityManager.AddComponentData(entity, new EvaluationComponent512() { EvaluationType = evaluationType, MaxEvaluationCount = (ushort)UnityEngine.Mathf.Max(1, maxEvaluationCount), EvaluatedTasks = evaluatedTasks });
            } else if (taskCount < 32704) {
                world.EntityManager.AddComponent<EvaluationComponent4096>(entity);
                var evaluatedTasks = new FixedList4096Bytes<ulong>();
                if (evaluationType == EvaluationType.EntireTree) {
                    for (int i = 0; i <= taskCount / ulongBitSize; ++i) {
                        evaluatedTasks.Add(0);
                    }
                } else {
                    evaluatedTasks.Add(0);
                }
                world.EntityManager.AddComponentData(entity, new EvaluationComponent4096() { EvaluationType = evaluationType, MaxEvaluationCount = (ushort)UnityEngine.Mathf.Max(1, maxEvaluationCount), EvaluatedTasks = evaluatedTasks });
            } else {
                UnityEngine.Debug.LogError("Error: Trees with more than 32,703 tasks are not supported. Please email support@opsive.com.");
            }
        }

        /// <summary>
        /// Resets the evaluation component data for the specified entity by clearing all evaluated task flags.
        /// </summary>
        /// <param name="world">The ECS world containing the entity.</param>
        /// <param name="entity">The entity whose evaluation component should be reset.</param>
        public static void ResetEvaluationComponent(World world, Entity entity)
        {
            if (world.EntityManager.HasComponent<EvaluationComponent32>(entity)) {
                var evaluateComponent = world.EntityManager.GetComponentData<EvaluationComponent32>(entity);
                var evaluatedTasks = evaluateComponent.EvaluatedTasks;
                for (int i = 0; i < evaluatedTasks.Length; ++i) {
                    evaluatedTasks[i] = 0;
                }
                evaluateComponent.EvaluatedTasks = evaluatedTasks;
                world.EntityManager.SetComponentData(entity, evaluateComponent);
            } else if (world.EntityManager.HasComponent<EvaluationComponent64>(entity)) {
                var evaluateComponent = world.EntityManager.GetComponentData<EvaluationComponent64>(entity);
                var evaluatedTasks = evaluateComponent.EvaluatedTasks;
                for (int i = 0; i < evaluatedTasks.Length; ++i) {
                    evaluatedTasks[i] = 0;
                }
                evaluateComponent.EvaluatedTasks = evaluatedTasks;
                world.EntityManager.SetComponentData(entity, evaluateComponent);
            } else if (world.EntityManager.HasComponent<EvaluationComponent128>(entity)) {
                var evaluateComponent = world.EntityManager.GetComponentData<EvaluationComponent128>(entity);
                var evaluatedTasks = evaluateComponent.EvaluatedTasks;
                for (int i = 0; i < evaluatedTasks.Length; ++i) {
                    evaluatedTasks[i] = 0;
                }
                evaluateComponent.EvaluatedTasks = evaluatedTasks;
                world.EntityManager.SetComponentData(entity, evaluateComponent);
            } else if (world.EntityManager.HasComponent<EvaluationComponent512>(entity)) {
                var evaluateComponent = world.EntityManager.GetComponentData<EvaluationComponent512>(entity);
                var evaluatedTasks = evaluateComponent.EvaluatedTasks;
                for (int i = 0; i < evaluatedTasks.Length; ++i) {
                    evaluatedTasks[i] = 0;
                }
                evaluateComponent.EvaluatedTasks = evaluatedTasks;
                world.EntityManager.SetComponentData(entity, evaluateComponent);
            } else if (world.EntityManager.HasComponent<EvaluationComponent4096>(entity)) {
                var evaluateComponent = world.EntityManager.GetComponentData<EvaluationComponent4096>(entity);
                var evaluatedTasks = evaluateComponent.EvaluatedTasks;
                for (int i = 0; i < evaluatedTasks.Length; ++i) {
                    evaluatedTasks[i] = 0;
                }
                evaluateComponent.EvaluatedTasks = evaluatedTasks;
                world.EntityManager.SetComponentData(entity, evaluateComponent);
            }
        }

        /// <summary>
        /// Removes the evaluation component from the specified entity.
        /// </summary>
        /// <param name="world">The ECS world containing the entity.</param>
        /// <param name="entity">The entity whose evaluation component should be removed.</param>
        public static void RemoveEvaluationComponent(World world, Entity entity)
        {
            if (world.EntityManager.HasComponent<EvaluationComponent32>(entity)) {
                world.EntityManager.RemoveComponent<EvaluationComponent32>(entity);
            } else if (world.EntityManager.HasComponent<EvaluationComponent64>(entity)) {
                world.EntityManager.RemoveComponent<EvaluationComponent64>(entity);
            } else if (world.EntityManager.HasComponent<EvaluationComponent128>(entity)) {
                world.EntityManager.RemoveComponent<EvaluationComponent128>(entity);
            } else if (world.EntityManager.HasComponent<EvaluationComponent512>(entity)) {
                world.EntityManager.RemoveComponent<EvaluationComponent512>(entity);
            } else if (world.EntityManager.HasComponent<EvaluationComponent4096>(entity)) {
                world.EntityManager.RemoveComponent<EvaluationComponent4096>(entity);
            }
        }

        /// <summary>
        /// Adds the components necessary in order to trigger an interrupt.
        /// </summary>
        /// <param name="entityManager">The EntityManager that the entity belongs to.</param>
        /// <param name="entity">The entity that should have the components added.</param>
        public static void AddInterruptComponents(EntityManager entityManager, Entity entity)
        {
            entityManager.AddComponent<InterruptFlag>(entity);
            entityManager.SetComponentEnabled<InterruptFlag>(entity, false);
            entityManager.AddComponent<InterruptedFlag>(entity);
            entityManager.SetComponentEnabled<InterruptedFlag>(entity, false);
        }
    }
}
#endif