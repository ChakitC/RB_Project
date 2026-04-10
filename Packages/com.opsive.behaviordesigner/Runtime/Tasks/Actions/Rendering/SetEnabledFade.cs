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
    [Opsive.Shared.Utility.Description("Enables/disables renderer with optional fade transition.")]
    public class SetEnabledFade : TargetGameObjectAction
    {
        [Tooltip("Whether to enable the renderer.")]
        [SerializeField] protected SharedVariable<bool> m_Enable = true;
        [Tooltip("Whether to fade the material alpha when enabling/disabling.")]
        [SerializeField] protected SharedVariable<bool> m_FadeTransition = false;
        [Tooltip("The fade duration (0 = instant).")]
        [SerializeField] protected SharedVariable<float> m_FadeDuration = 0.5f;

        private Renderer m_ResolvedRenderer;
        private Material m_Material;
        private float m_ElapsedTime;
        private bool m_IsFading;

        /// <summary>
        /// Called when the action starts.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();
            m_ElapsedTime = 0.0f;
            m_IsFading = false;
            m_ResolvedRenderer = m_ResolvedGameObject.GetComponent<Renderer>();
            if (m_ResolvedRenderer != null) {
                m_Material = m_ResolvedRenderer.material;
            }
        }

        /// <summary>
        /// Updates the renderer enabled state.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_ResolvedRenderer == null) {
                return TaskStatus.Failure;
            }

            if (m_FadeTransition.Value && m_FadeDuration.Value > 0.0f) {
                if (!m_IsFading) {
                    m_IsFading = true;
                    m_ElapsedTime = 0.0f;
                }

                m_ElapsedTime += Time.deltaTime;

                var progress = Mathf.Clamp01(m_ElapsedTime / m_FadeDuration.Value);
                var targetAlpha = m_Enable.Value ? 1.0f : 0.0f;
                var currentAlpha = Mathf.Lerp(1.0f - targetAlpha, targetAlpha, progress);
                if (m_Material != null && m_Material.HasProperty("_Color")) {
                    var color = m_Material.color;
                    color.a = currentAlpha;
                    m_Material.color = color;
                }

                if (progress >= 1.0f) {
                    m_ResolvedRenderer.enabled = m_Enable.Value;
                    m_IsFading = false;
                    return TaskStatus.Success;
                }

                return TaskStatus.Running;
            } else {
                m_ResolvedRenderer.enabled = m_Enable.Value;
            }

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_Enable = true;
            m_FadeTransition = false;
            m_FadeDuration = 0.5f;
        }
    }
}
#endif