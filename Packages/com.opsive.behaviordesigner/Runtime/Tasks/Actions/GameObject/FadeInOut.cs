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
    [Opsive.Shared.Utility.Description("Fades a GameObject in or out using CanvasGroup or Renderer alpha. Returns Finished when fade is complete.")]
    public class FadeInOut : TargetGameObjectAction
    {
        /// <summary>
        /// Specifies the fade direction.
        /// </summary>
        public enum FadeDirection
        {
            FadeIn,     // Fade from transparent to opaque.
            FadeOut     // Fade from opaque to transparent.
        }

        [Tooltip("The fade direction.")]
        [SerializeField] protected FadeDirection m_FadeDirection = FadeDirection.FadeOut;
        [Tooltip("The fade duration (in seconds).")]
        [SerializeField] protected SharedVariable<float> m_FadeDuration = 1f;
        [Tooltip("The starting alpha. Only used if Fade Direction is Fade In.")]
        [SerializeField] protected SharedVariable<float> m_StartAlpha = 0f;
        [Tooltip("The ending alpha. Only used if Fade Direction is Fade Out.")]
        [SerializeField] protected SharedVariable<float> m_EndAlpha = 0f;

        private float m_ElapsedTime;
        private CanvasGroup m_CanvasGroup;
        private Renderer[] m_Renderers;
        private float[] m_InitialAlphas;
        private bool m_Initialized;

        /// <summary>
        /// Called when the state machine starts.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();
            m_ElapsedTime = 0f;
            m_Initialized = false;
            InitializeFade();
        }

        /// <summary>
        /// Initializes the fade components.
        /// </summary>
        private void InitializeFade()
        {
            m_CanvasGroup = m_ResolvedGameObject.GetComponent<CanvasGroup>();
            if (m_CanvasGroup == null) {
                m_Renderers = m_ResolvedGameObject.GetComponentsInChildren<Renderer>();
                if (m_Renderers != null && m_Renderers.Length > 0) {
                    m_InitialAlphas = new float[m_Renderers.Length];
                    for (int i = 0; i < m_Renderers.Length; ++i) {
                        if (m_Renderers[i].material.HasProperty("_Color")) {
                            m_InitialAlphas[i] = m_Renderers[i].material.color.a;
                        }
                    }
                }
            }

            // Set initial alpha.
            var initialAlpha = m_FadeDirection == FadeDirection.FadeIn ? m_StartAlpha.Value : (m_CanvasGroup != null ? m_CanvasGroup.alpha : (m_InitialAlphas != null && m_InitialAlphas.Length > 0 ? m_InitialAlphas[0] : 1f));
            SetAlpha(initialAlpha);
            m_Initialized = true;
        }

        /// <summary>
        /// Fades the GameObject.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (!m_Initialized) {
                return TaskStatus.Running;
            }

            m_ElapsedTime += Time.deltaTime;
            var progress = Mathf.Clamp01(m_ElapsedTime / m_FadeDuration.Value);

            float targetAlpha;
            if (m_FadeDirection == FadeDirection.FadeIn) {
                targetAlpha = Mathf.Lerp(m_StartAlpha.Value, 1f, progress);
            } else {
                var startAlpha = m_CanvasGroup != null ? 1f : (m_InitialAlphas != null && m_InitialAlphas.Length > 0 ? m_InitialAlphas[0] : 1f);
                targetAlpha = Mathf.Lerp(startAlpha, m_EndAlpha.Value, progress);
            }

            SetAlpha(targetAlpha);

            if (progress >= 1f) {
                return TaskStatus.Success;
            }

            return TaskStatus.Running;
        }

        /// <summary>
        /// Sets the alpha value on CanvasGroup or Renderers.
        /// </summary>
        private void SetAlpha(float alpha)
        {
            if (m_CanvasGroup != null) {
                m_CanvasGroup.alpha = alpha;
            } else if (m_Renderers != null) {
                for (int i = 0; i < m_Renderers.Length; ++i) {
                    if (m_Renderers[i].material.HasProperty("_Color")) {
                        var color = m_Renderers[i].material.color;
                        var targetAlpha = m_InitialAlphas != null && i < m_InitialAlphas.Length ? m_InitialAlphas[i] * alpha : alpha;
                        color.a = targetAlpha;
                        m_Renderers[i].material.color = color;
                    }
                }
            }
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_FadeDirection = FadeDirection.FadeOut;
            m_FadeDuration = 1f;
            m_StartAlpha = 0f;
            m_EndAlpha = 0f;
        }
    }
}
#endif