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
    [Opsive.Shared.Utility.Description("Sets the tag and layer for the GameObject.")]
    public class SetTagAndLayer : TargetGameObjectAction
    {
        [Tooltip("The tag to set. Leave empty to keep current tag.")]
        [SerializeField] protected SharedVariable<string> m_Tag = "";
        [Tooltip("The layer index to set. Set to -1 to keep current layer.")]
        [SerializeField] protected SharedVariable<int> m_Layer = -1;
        [Tooltip("Whether to set tag and layer recursively for all children.")]
        [SerializeField] protected SharedVariable<bool> m_Recursive = false;

        /// <summary>
        /// Sets the tag and layer.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            DoAssignment(m_ResolvedGameObject, m_Tag.Value, m_Layer.Value);
            return TaskStatus.Success;
        }

        /// <summary>
        /// Sets the tag and layer.
        /// </summary>
        private void DoAssignment(GameObject obj, string tag, int layer)
        {
            if (obj == null) {
                return;
            }

            if (!string.IsNullOrEmpty(tag)) {
                try {
                    obj.tag = tag;
                } catch (UnityException) {
                    Debug.LogWarning($"Tag '{tag}' does not exist. Please add it in the Tags & Layers settings.");
                }
            }

            if (layer >= 0 && layer <= 31) {
                obj.layer = layer;
            }

            if (m_Recursive.Value) {
                foreach (Transform child in obj.transform) {
                    DoAssignment(child.gameObject, tag, layer);
                }
            }
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_Tag = "";
            m_Layer = -1;
            m_Recursive = false;
        }
    }
}
#endif