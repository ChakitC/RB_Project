#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.RenderingTasks
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("Rendering")]
    [Opsive.Shared.Utility.Description("Sets material render queue with validation.")]
    public class SetRenderQueue : TargetGameObjectAction
    {
        [Tooltip("The render queue value.")]
        [SerializeField] protected SharedVariable<int> m_RenderQueue = 2000;

        private Renderer m_ResolvedRenderer;
        private Material m_Material;

        /// <summary>
        /// Called when the action starts.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();
            m_ResolvedRenderer = m_ResolvedGameObject.GetComponent<Renderer>();
            if (m_ResolvedRenderer != null) {
                m_Material = m_ResolvedRenderer.material;
            }
        }

        /// <summary>
        /// Updates the render queue.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_ResolvedRenderer == null) {
                return TaskStatus.Success;
            }

            if (m_Material == null) {
                m_Material = m_ResolvedRenderer.material;
            }
            if (m_Material == null) {
                return TaskStatus.Success;
            }

            if (m_RenderQueue.Value >= 0 && m_RenderQueue.Value <= 5000) {
                m_Material.renderQueue = m_RenderQueue.Value;
            }

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_RenderQueue = 2000;
        }
    }
}
#endif