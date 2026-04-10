#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.GameObjectTasks
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("GameObject")]
    [Opsive.Shared.Utility.Description("Gets a component from a GameObject's parent hierarchy using the string component type name.")]
    public class GetComponentInParent : TargetGameObjectAction
    {
        [Tooltip("The component type name (e.g., 'Rigidbody', 'Camera', 'Transform').")]
        [SerializeField] protected SharedVariable<string> m_ComponentType;
        [Tooltip("The retrieved component.")]
        [RequireShared] [SerializeField] protected SharedVariable m_StoreResult;

        /// <summary>
        /// Executes the action logic.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (string.IsNullOrEmpty(m_ComponentType.Value)) {
                return TaskStatus.Success;
            }

            var componentType = Opsive.Shared.Utility.TypeUtility.GetType(m_ComponentType.Value);
            m_StoreResult.SetValue(m_ResolvedGameObject.GetComponentInParent(componentType));
            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_ComponentType = string.Empty;
            m_StoreResult = null;
        }
    }
}
#endif