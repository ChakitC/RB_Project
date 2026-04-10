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
    [Opsive.Shared.Utility.Description("Fades particle system in/out with duration control.")]
    public class Fade : TargetGameObjectAction
    {
        /// <summary>
        /// Specifies the fade direction.
        /// </summary>
        public enum FadeDirection
        {
            FadeIn,
            FadeOut
        }

        [Tooltip("The fade direction.")]
        [SerializeField] protected FadeDirection m_FadeDirection = FadeDirection.FadeOut;
        [Tooltip("The fade duration.")]
        [SerializeField] protected SharedVariable<float> m_Duration = 1.0f;

        private ParticleSystem m_ResolvedParticleSystem;
        private ParticleSystem.MainModule m_MainModule;
        private float m_StartAlpha;
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
                m_MainModule = m_ResolvedParticleSystem.main;
                var startColor = m_MainModule.startColor;
                if (startColor.mode == ParticleSystemGradientMode.Color) {
                    m_StartAlpha = startColor.color.a;
                } else {
                    m_StartAlpha = 1.0f;
                }

                if (m_FadeDirection == FadeDirection.FadeIn) {
                    m_ResolvedParticleSystem.Play();
                }
            }
        }

        /// <summary>
        /// Updates the particle fade.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_ResolvedParticleSystem == null) {
                return TaskStatus.Success;
            }

            m_ElapsedTime += Time.deltaTime;
            var progress = Mathf.Clamp01(m_ElapsedTime / m_Duration.Value);

            var targetAlpha = m_FadeDirection == FadeDirection.FadeIn ? 1.0f : 0.0f;
            var currentAlpha = Mathf.Lerp(m_StartAlpha, targetAlpha, progress);

            var startColor = m_MainModule.startColor;
            if (startColor.mode == ParticleSystemGradientMode.Color) {
                var color = startColor.color;
                color.a = currentAlpha;
                m_MainModule.startColor = color;
            }

            if (progress >= 1.0f) {
                if (m_FadeDirection == FadeDirection.FadeOut) {
                    m_ResolvedParticleSystem.Stop();
                }
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
            m_FadeDirection = FadeDirection.FadeOut;
            m_Duration = 1.0f;
        }
    }
}
#endif