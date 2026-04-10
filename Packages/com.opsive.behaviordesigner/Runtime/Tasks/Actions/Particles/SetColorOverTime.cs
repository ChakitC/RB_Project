#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.ParticlesTasks
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("Particles")]
    [Opsive.Shared.Utility.Description("Changes particle color over time with gradient support.")]
    public class SetColorOverTime : TargetGameObjectAction
    {
        [Tooltip("The target color gradient.")]
        [SerializeField] protected Gradient m_ColorGradient;
        [Tooltip("The transition duration.")]
        [SerializeField] protected SharedVariable<float> m_Duration = 1.0f;

        private ParticleSystem m_ResolvedParticleSystem;
        private ParticleSystem.ColorOverLifetimeModule m_ColorOverLifetime;
        private Gradient m_StartGradient;
        private float m_ElapsedTime;

        /// <summary>
        /// Called when the state machine is initialized.
        /// </summary>
        /// <summary>
        /// Initializes the target GameObject.
        /// </summary>
        protected override void InitializeTarget()
        {
            base.InitializeTarget();

            m_ResolvedParticleSystem = m_ResolvedGameObject.GetComponent<ParticleSystem>();
        }

        /// <summary>
        /// Called when the action starts.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();
            m_ElapsedTime = 0.0f;
            if (m_ResolvedParticleSystem != null) {
                m_ColorOverLifetime = m_ResolvedParticleSystem.colorOverLifetime;
                m_ColorOverLifetime.enabled = true;
                if (m_ColorOverLifetime.color.mode == ParticleSystemGradientMode.Gradient) {
                    m_StartGradient = m_ColorOverLifetime.color.gradient;
                }
            }
        }

        /// <summary>
        /// Updates the particle color.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_ResolvedParticleSystem == null || m_ColorGradient == null) {
                return TaskStatus.Success;
            }

            m_ElapsedTime += Time.deltaTime;
            var progress = Mathf.Clamp01(m_ElapsedTime / m_Duration.Value);

            if (m_StartGradient != null) {
                var blendedGradient = new Gradient();
                var colorKeys = new GradientColorKey[m_ColorGradient.colorKeys.Length];
                var alphaKeys = new GradientAlphaKey[m_ColorGradient.alphaKeys.Length];

                for (int i = 0; i < colorKeys.Length; ++i) {
                    var startColor = m_StartGradient.Evaluate(m_ColorGradient.colorKeys[i].time);
                    var targetColor = m_ColorGradient.colorKeys[i].color;
                    colorKeys[i] = new GradientColorKey(Color.Lerp(startColor, targetColor, progress), m_ColorGradient.colorKeys[i].time);
                }

                for (int i = 0; i < alphaKeys.Length; ++i) {
                    var startAlpha = m_StartGradient.Evaluate(m_ColorGradient.alphaKeys[i].time).a;
                    var targetAlpha = m_ColorGradient.alphaKeys[i].alpha;
                    alphaKeys[i] = new GradientAlphaKey(Mathf.Lerp(startAlpha, targetAlpha, progress), m_ColorGradient.alphaKeys[i].time);
                }

                blendedGradient.SetKeys(colorKeys, alphaKeys);
                m_ColorOverLifetime.color = blendedGradient;
            } else {
                m_ColorOverLifetime.color = m_ColorGradient;
            }

            if (progress >= 1.0f) {
                return TaskStatus.Success;
            }

            return TaskStatus.Running;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_ColorGradient = null;
            m_Duration = 1.0f;
        }
    }
}
#endif