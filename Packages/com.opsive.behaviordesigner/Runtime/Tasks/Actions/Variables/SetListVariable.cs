#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.Variables
{
    using Opsive.GraphDesigner.Runtime;
    using Opsive.GraphDesigner.Runtime.Variables;
    using System;
    using System.Collections;
    using UnityEngine;

    [Opsive.Shared.Utility.Description("Sets the value of a list SharedVariable by name.")]
    [Shared.Utility.Category("Variables")]
    public class SetListVariable : SetVariableBase
    {
        [Tooltip("The name of the list SharedVariable.")]
        [SerializeField] protected SharedVariable<string> m_VariableName;
        [Tooltip("The list value to set.")]
        [SerializeField] protected SharedVariable m_Value;

        /// <summary>
        /// Executes the task.
        /// </summary>
        /// <returns>The execution status of the task.</returns>
        public override TaskStatus OnUpdate()
        {
            var destination = GetVariable(m_VariableName);
            TrySetValue(m_Value, destination);
            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the task values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_VariableName = "";
            m_Value = null;
        }

        /// <summary>
        /// Tries to assign the source list variable value to the destination list variable.
        /// </summary>
        /// <param name="source">The source list variable.</param>
        /// <param name="destination">The destination list variable.</param>
        /// <returns>True if the value was assigned.</returns>
        private static bool TrySetValue(SharedVariable source, SharedVariable destination)
        {
            if (source == null || destination == null) {
                return false;
            }

            var sourceType = source.ElementType;
            var destinationType = destination.ElementType;
            if (!IsListType(sourceType) || !IsListType(destinationType) || !destinationType.IsAssignableFrom(sourceType)) {
                return false;
            }

            var sourceValue = source.GetValue();
            if (sourceValue == null) {
                destination.SetValue(null);
                return true;
            }

            var destinationValue = destination.GetValue();
            if (ReferenceEquals(sourceValue, destinationValue)) {
                return true;
            }

            if (sourceValue is Array sourceArray) {
                destination.SetValue(sourceArray.Clone());
                return true;
            }

            if (sourceValue is IList sourceList) {
                if (destinationValue is IList destinationList && !ReferenceEquals(sourceList, destinationList) && !destinationList.IsReadOnly && !destinationList.IsFixedSize) {
                    destinationList.Clear();
                    for (int i = 0; i < sourceList.Count; ++i) {
                        destinationList.Add(sourceList[i]);
                    }
                    return true;
                }

                var copiedList = CreateListInstance(destinationType);
                if (copiedList != null) {
                    for (int i = 0; i < sourceList.Count; ++i) {
                        copiedList.Add(sourceList[i]);
                    }
                    destination.SetValue(copiedList);
                    return true;
                }
            }

            destination.SetValue(sourceValue);
            return true;
        }

        /// <summary>
        /// Returns true if the type stores a list-like value.
        /// </summary>
        /// <param name="type">The type to inspect.</param>
        /// <returns>True if the type stores a list-like value.</returns>
        private static bool IsListType(Type type)
        {
            return type != null && (type.IsArray || typeof(IList).IsAssignableFrom(type));
        }

        /// <summary>
        /// Creates a writable list instance for the specified type.
        /// </summary>
        /// <param name="type">The list type to create.</param>
        /// <returns>A writable list instance, or null if one cannot be created.</returns>
        private static IList CreateListInstance(Type type)
        {
            if (type == null || type.IsArray || type.IsAbstract || type.IsInterface) {
                return null;
            }

            try {
                if (Activator.CreateInstance(type) is IList list && !list.IsReadOnly && !list.IsFixedSize) {
                    return list;
                }
            } catch {
                // Some IList implementations cannot be created with a default constructor.
            }

            return null;
        }
    }

    /// <summary>
    /// Gets the value of a list SharedVariable by name.
    /// </summary>
    [Opsive.Shared.Utility.Description("Gets the value of a list SharedVariable by name.")]
    [Shared.Utility.Category("Variables")]
    public class GetListVariable : GetVariableBase
    {
        [Tooltip("The name of the list SharedVariable.")]
        [SerializeField] protected SharedVariable<string> m_VariableName;
        [Tooltip("The list variable to store the retrieved value.")]
        [RequireShared] [SerializeField] protected SharedVariable m_Destination;

        /// <summary>
        /// Executes the task.
        /// </summary>
        /// <returns>The execution status of the task.</returns>
        public override TaskStatus OnUpdate()
        {
            var source = GetVariable(m_VariableName);
            TrySetValue(source, m_Destination);
            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the task values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_VariableName = "";
            m_Destination = null;
        }

        /// <summary>
        /// Tries to assign the source list variable value to the destination list variable.
        /// </summary>
        /// <param name="source">The source list variable.</param>
        /// <param name="destination">The destination list variable.</param>
        /// <returns>True if the value was assigned.</returns>
        private static bool TrySetValue(SharedVariable source, SharedVariable destination)
        {
            if (source == null || destination == null) {
                return false;
            }

            var sourceType = source.ElementType;
            var destinationType = destination.ElementType;
            if (!IsListType(sourceType) || !IsListType(destinationType) || !destinationType.IsAssignableFrom(sourceType)) {
                return false;
            }

            var sourceValue = source.GetValue();
            if (sourceValue == null) {
                destination.SetValue(null);
                return true;
            }

            var destinationValue = destination.GetValue();
            if (ReferenceEquals(sourceValue, destinationValue)) {
                return true;
            }

            if (sourceValue is Array sourceArray) {
                destination.SetValue(sourceArray.Clone());
                return true;
            }

            if (sourceValue is IList sourceList) {
                if (destinationValue is IList destinationList && !ReferenceEquals(sourceList, destinationList) && !destinationList.IsReadOnly && !destinationList.IsFixedSize) {
                    destinationList.Clear();
                    for (int i = 0; i < sourceList.Count; ++i) {
                        destinationList.Add(sourceList[i]);
                    }
                    return true;
                }

                var copiedList = CreateListInstance(destinationType);
                if (copiedList != null) {
                    for (int i = 0; i < sourceList.Count; ++i) {
                        copiedList.Add(sourceList[i]);
                    }
                    destination.SetValue(copiedList);
                    return true;
                }
            }

            destination.SetValue(sourceValue);
            return true;
        }

        /// <summary>
        /// Returns true if the type stores a list-like value.
        /// </summary>
        /// <param name="type">The type to inspect.</param>
        /// <returns>True if the type stores a list-like value.</returns>
        private static bool IsListType(Type type)
        {
            return type != null && (type.IsArray || typeof(IList).IsAssignableFrom(type));
        }

        /// <summary>
        /// Creates a writable list instance for the specified type.
        /// </summary>
        /// <param name="type">The list type to create.</param>
        /// <returns>A writable list instance, or null if one cannot be created.</returns>
        private static IList CreateListInstance(Type type)
        {
            if (type == null || type.IsArray || type.IsAbstract || type.IsInterface) {
                return null;
            }

            try {
                if (Activator.CreateInstance(type) is IList list && !list.IsReadOnly && !list.IsFixedSize) {
                    return list;
                }
            } catch {
                // Some IList implementations cannot be created with a default constructor.
            }

            return null;
        }
    }
}
#endif