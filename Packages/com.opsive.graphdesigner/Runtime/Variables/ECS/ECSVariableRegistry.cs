#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Graph Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.GraphDesigner.Runtime.Variables.ECS
{
    using System;
    using System.Collections.Generic;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Entities;
    using Unity.Mathematics;
    using UnityEngine;

    /// <summary>
    /// Managed registry created once per entity during graph initialization. Collects SharedVariable registrations from all ECS tasks, deduplicates by name+scope,
    /// bakes initial values into DynamicBuffer<SharedVariableElement>, and supports syncing runtime values between the ECS buffer and managed SharedVariables.
    /// </summary>
    public class ECSVariableRegistry
    {
        private readonly Dictionary<(string, SharedVariable.SharingScope), int> m_IndexMap = new();
        private readonly List<float4> m_InitialValues = new();
        private readonly List<float4> m_LastSyncedValues = new();
        private readonly List<Action<DynamicBuffer<SharedVariableElement>>> m_SyncToManagedActions = new();
        private readonly List<Action<DynamicBuffer<SharedVariableElement>>> m_SyncToECSActions = new();
        private readonly List<bool> m_DirtyManagedValues = new();
        private readonly List<SharedVariable> m_TrackedSharedVariables = new();
        private readonly List<Action> m_ManagedChangeTrackingActions = new();
        private bool m_SuppressManagedValueTracking;

        /// <summary>
        /// Gets the number of variables registered so far.
        /// </summary>
        public int Count => m_InitialValues.Count;

        /// <summary>
        /// Registers a SharedVariable with the registry and returns its buffer index.
        /// </summary>
        /// <param name="sharedVariable">The SharedVariable that should be registered.</param>
        /// <returns>The buffer index of the registered SharedVariable, or -1 if the SharedVariable is null.</returns>
        public unsafe int Register<T>(SharedVariable<T> sharedVariable) where T : unmanaged
        {
            if (sharedVariable == null) {
                return -1;
            }

            var size = UnsafeUtility.SizeOf<T>();
            Debug.Assert(size <= 16, $"SharedVariable<{typeof(T).Name}> value size ({size} bytes) exceeds the 16-byte SharedVariableElement limit. Use a smaller unmanaged type.");

            // Deduplicate named shared variables by name + scope.
            var name = sharedVariable.Name ?? string.Empty;
            var key = (name, sharedVariable.Scope);
            if (!string.IsNullOrEmpty(name) && m_IndexMap.TryGetValue(key, out var existingIndex)) {
                return existingIndex;
            }

            var index = m_InitialValues.Count;
            if (!string.IsNullOrEmpty(name)) {
                m_IndexMap[key] = index;
            }

            // Encode the initial value into a float4 via raw memory copy.
            T value = sharedVariable.Value;
            float4 f4 = default;
            UnsafeUtility.MemCpy(&f4, &value, size);
            m_InitialValues.Add(f4);
            m_LastSyncedValues.Add(f4);
            m_DirtyManagedValues.Add(false);

            // Capture sync delegates so managed and ECS tasks stay in sync while the tree is running.
            var capturedVar = sharedVariable;
            var capturedIndex = index;
            Action trackingAction = () => {
                if (!m_SuppressManagedValueTracking) {
                    m_DirtyManagedValues[capturedIndex] = true;
                }
            };
            sharedVariable.OnValueChange += trackingAction;
            m_TrackedSharedVariables.Add(sharedVariable);
            m_ManagedChangeTrackingActions.Add(trackingAction);
            m_SyncToManagedActions.Add((buffer) => {
                capturedVar.Value = buffer.Get<T>(capturedIndex);
            });
            m_SyncToECSActions.Add((buffer) => {
                buffer.Set(capturedIndex, capturedVar.Value);
            });

            return index;
        }

        /// <summary>
        /// Creates the DynamicBuffer<SharedVariableElement> on the entity and writes all registered initial values. Call once after all tasks have registered their variables.
        /// </summary>
        /// <param name="world">The world that owns the entity.</param>
        /// <param name="entity">The entity that should receive the shared variable buffer.</param>
        public void Bake(World world, Entity entity)
        {
            if (m_InitialValues.Count == 0) {
                return;
            }

            DynamicBuffer<SharedVariableElement> buffer;
            if (world.EntityManager.HasBuffer<SharedVariableElement>(entity)) {
                buffer = world.EntityManager.GetBuffer<SharedVariableElement>(entity);
                buffer.Clear();
            } else {
                buffer = world.EntityManager.AddBuffer<SharedVariableElement>(entity);
            }
            for (int i = 0; i < m_InitialValues.Count; ++i) {
                buffer.Add(new SharedVariableElement { Value = m_InitialValues[i] });
            }
        }

        /// <summary>
        /// Writes the current managed SharedVariable values into the ECS buffer.
        /// </summary>
        /// <param name="world">The world that owns the entity.</param>
        /// <param name="entity">The entity whose shared variable buffer should be synced.</param>
        public void SyncToECS(World world, Entity entity)
        {
            if (m_SyncToECSActions.Count == 0 || world == null || entity == Entity.Null) {
                return;
            }

            if (!world.EntityManager.HasBuffer<SharedVariableElement>(entity)) {
                return;
            }

            var buffer = world.EntityManager.GetBuffer<SharedVariableElement>(entity);
            for (int i = 0; i < m_SyncToECSActions.Count; ++i) {
                if (!m_DirtyManagedValues[i]) {
                    continue;
                }
                m_SyncToECSActions[i](buffer);
                m_LastSyncedValues[i] = buffer[i].Value;
                m_DirtyManagedValues[i] = false;
            }
        }

        /// <summary>
        /// Reads current ECS buffer values back into the managed SharedVariable instances.
        /// </summary>
        /// <param name="world">The world that owns the entity.</param>
        /// <param name="entity">The entity whose shared variable buffer should be synced.</param>
        public void SyncToManaged(World world, Entity entity)
        {
            if (m_SyncToManagedActions.Count == 0 || world == null || entity == Entity.Null) {
                return;
            }

            if (!world.EntityManager.HasBuffer<SharedVariableElement>(entity)) {
                return;
            }

            var buffer = world.EntityManager.GetBuffer<SharedVariableElement>(entity);
            m_SuppressManagedValueTracking = true;
            try {
                for (int i = 0; i < m_SyncToManagedActions.Count; ++i) {
                    var bufferValue = buffer[i].Value;
                    if (SharedVariableBufferValueEquals(m_LastSyncedValues[i], bufferValue)) {
                        continue;
                    }

                    m_SyncToManagedActions[i](buffer);
                    m_LastSyncedValues[i] = bufferValue;
                    m_DirtyManagedValues[i] = false;
                }
            } finally {
                m_SuppressManagedValueTracking = false;
            }
        }

        /// <summary>
        /// Returns true if the two raw shared variable buffer values are byte-for-byte identical.
        /// </summary>
        /// <param name="lhs">The first variable buffer.</param>
        /// <param name="lhs">The second variable buffer.</param>
        /// <returns>True if the variable values are equal.</returns>
        private static unsafe bool SharedVariableBufferValueEquals(float4 lhs, float4 rhs)
        {
            return UnsafeUtility.MemCmp(&lhs, &rhs, UnsafeUtility.SizeOf<float4>()) == 0;
        }

        /// <summary>
        /// Removes any managed SharedVariable change listeners registered by the registry.
        /// </summary>
        public void Dispose()
        {
            for (int i = 0; i < m_TrackedSharedVariables.Count; ++i) {
                if (m_TrackedSharedVariables[i] == null) {
                    continue;
                }
                m_TrackedSharedVariables[i].OnValueChange -= m_ManagedChangeTrackingActions[i];
            }

            m_TrackedSharedVariables.Clear();
            m_ManagedChangeTrackingActions.Clear();
        }
    }
}
#endif