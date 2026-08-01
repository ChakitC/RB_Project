#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.Variables
{
    using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    /// <summary>
    /// Base class for setting a SharedVariable by name in a selected scope.
    /// </summary>
    public abstract class SetVariableBase : TargetBehaviorTreeAction
    {
        /// <summary>
        /// Specifies the container scope that should be searched.
        /// </summary>
        public enum VariableScope
        {
            Graph,
            GameObject,
            Scene,
            Project
        }

        [Tooltip("The scope of the SharedVariable that should be set.")]
        [SerializeField] protected VariableScope m_VariableScope = VariableScope.Graph;

        /// <summary>
        /// Returns the SharedVariable with the specified name.
        /// </summary>
        /// <param name="variableName">The variable name.</param>
        /// <returns>The SharedVariable with the specified name.</returns>
        protected SharedVariable GetVariable(SharedVariable<string> variableName)
        {
            if (variableName == null || string.IsNullOrEmpty(variableName.Value)) {
                return null;
            }

            return GetVariableContainer()?.GetVariable(variableName.Value);
        }

        /// <summary>
        /// Returns the typed SharedVariable with the specified name.
        /// </summary>
        /// <typeparam name="T">The type of SharedVariable.</typeparam>
        /// <param name="variableName">The variable name.</param>
        /// <returns>The typed SharedVariable with the specified name.</returns>
        protected SharedVariable<T> GetVariable<T>(SharedVariable<string> variableName)
        {
            if (variableName == null || string.IsNullOrEmpty(variableName.Value)) {
                return null;
            }

            return GetVariableContainer()?.GetVariable<T>(variableName.Value);
        }

        /// <summary>
        /// Sets the value of the typed SharedVariable.
        /// </summary>
        /// <typeparam name="T">The type of SharedVariable.</typeparam>
        /// <param name="variableName">The variable name.</param>
        /// <param name="value">The value to set.</param>
        /// <returns>True if the variable was set.</returns>
        protected bool SetVariableValue<T>(SharedVariable<string> variableName, T value)
        {
            var destination = GetVariable<T>(variableName);
            if (destination == null) {
                return false;
            }

            destination.Value = value;
            return true;
        }

        /// <summary>
        /// Resets the task values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_VariableScope = VariableScope.Graph;
        }

        /// <summary>
        /// Returns the container that owns the target variable.
        /// </summary>
        /// <returns>The target variable container.</returns>
        private ISharedVariableContainer GetVariableContainer()
        {
            switch (m_VariableScope) {
                case VariableScope.GameObject:
                    var targetGameObject = (m_TargetGameObject == null || m_TargetGameObject.Value == null) ? m_GameObject : m_TargetGameObject.Value;
                    return targetGameObject != null ? targetGameObject.GetComponent<GameObjectSharedVariables>() : null;
                case VariableScope.Scene:
                    return SceneSharedVariables.Instance;
                case VariableScope.Project:
                    return ProjectSharedVariables.Instance;
                default:
                    return m_ResolvedBehaviorTree;
            }
        }
    }

    /// <summary>
    /// Base class for getting a SharedVariable by name from a selected scope.
    /// </summary>
    public abstract class GetVariableBase : TargetBehaviorTreeAction
    {
        /// <summary>
        /// Specifies the container scope that should be searched.
        /// </summary>
        public enum VariableScope
        {
            Graph,
            GameObject,
            Scene,
            Project
        }

        [Tooltip("The scope of the SharedVariable that should be retrieved.")]
        [SerializeField] protected VariableScope m_VariableScope = VariableScope.Graph;

        /// <summary>
        /// Returns the SharedVariable with the specified name.
        /// </summary>
        /// <param name="variableName">The variable name.</param>
        /// <returns>The SharedVariable with the specified name.</returns>
        protected SharedVariable GetVariable(SharedVariable<string> variableName)
        {
            if (variableName == null || string.IsNullOrEmpty(variableName.Value)) {
                return null;
            }

            return GetVariableContainer()?.GetVariable(variableName.Value);
        }

        /// <summary>
        /// Returns the typed SharedVariable with the specified name.
        /// </summary>
        /// <typeparam name="T">The type of SharedVariable.</typeparam>
        /// <param name="variableName">The variable name.</param>
        /// <returns>The typed SharedVariable with the specified name.</returns>
        protected SharedVariable<T> GetVariable<T>(SharedVariable<string> variableName)
        {
            if (variableName == null || string.IsNullOrEmpty(variableName.Value)) {
                return null;
            }

            return GetVariableContainer()?.GetVariable<T>(variableName.Value);
        }

        /// <summary>
        /// Resets the task values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_VariableScope = VariableScope.Graph;
        }

        /// <summary>
        /// Returns the container that owns the source variable.
        /// </summary>
        /// <returns>The source variable container.</returns>
        private ISharedVariableContainer GetVariableContainer()
        {
            switch (m_VariableScope) {
                case VariableScope.GameObject:
                    var targetGameObject = (m_TargetGameObject == null || m_TargetGameObject.Value == null) ? m_GameObject : m_TargetGameObject.Value;
                    return targetGameObject != null ? targetGameObject.GetComponent<GameObjectSharedVariables>() : null;
                case VariableScope.Scene:
                    return SceneSharedVariables.Instance;
                case VariableScope.Project:
                    return ProjectSharedVariables.Instance;
                default:
                    return m_ResolvedBehaviorTree;
            }
        }
    }
}
#endif