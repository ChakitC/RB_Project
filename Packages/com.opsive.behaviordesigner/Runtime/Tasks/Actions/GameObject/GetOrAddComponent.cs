#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.GameObjectTasks
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using Opsive.Shared.Utility;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("GameObject")]
    [Opsive.Shared.Utility.Description("Gets a component from a GameObject, or adds it if it doesn't exist. Outputs the GameObject reference.")]
    public class GetOrAddComponent : TargetGameObjectAction
    {
        [Tooltip("The component type name (e.g., 'Rigidbody', 'Camera', 'Transform').")]
        [SerializeField] protected SharedVariable<string> m_ComponentType;
        [SerializeField][RequireShared] protected SharedVariable m_StoreValue;

        /// <summary>
        /// Gets or adds the component.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (string.IsNullOrEmpty(m_ComponentType.Value)) {
                return TaskStatus.Success;
            }

            var componentType = TypeUtility.GetType(m_ComponentType.Value);
            if (componentType == null) {
                Debug.LogWarning($"GetOrAddComponent: Component type '{m_ComponentType.Value}' not found.");
                return TaskStatus.Success;
            }

            // Try to get component first.
            var component = m_ResolvedGameObject.GetComponent(componentType);
            if (component == null) {
                // Add component if it doesn't exist.
                component = m_ResolvedGameObject.AddComponent(componentType);
            }
            m_StoreValue.SetValue(component);

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_ComponentType = null;
            m_StoreValue = null;
        }
    }
}
#endif