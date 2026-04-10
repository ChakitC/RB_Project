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
    [Opsive.Shared.Utility.Description("Gets layer name from layer index with validation.")]
    public class GetLayerName : Action
    {
        [Tooltip("The layer index.")]
        [SerializeField] protected SharedVariable<int> m_LayerIndex = 0;
        [Tooltip("The layer name.")]
        [SerializeField] [RequireShared] protected SharedVariable<string> m_LayerName;

        /// <summary>
        /// Gets the layer name.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_LayerIndex.Value >= 0 && m_LayerIndex.Value < 32) {
                var layerName = LayerMask.LayerToName(m_LayerIndex.Value);
                m_LayerName.Value = string.IsNullOrEmpty(layerName) ? "Layer " + m_LayerIndex.Value : layerName;
            } else {
                m_LayerName.Value = "";
            }

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_LayerIndex = 0;
            m_LayerName = null;
        }
    }
}
#endif