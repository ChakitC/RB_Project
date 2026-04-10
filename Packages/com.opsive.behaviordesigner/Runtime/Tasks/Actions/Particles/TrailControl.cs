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
    [Opsive.Shared.Utility.Description("Controls particle trail with width, color, and lifetime.")]
    public class TrailControl : TargetGameObjectAction
    {
        [Tooltip("The trail width multiplier.")]
        [SerializeField] protected SharedVariable<float> m_WidthMultiplier = 1.0f;
        [Tooltip("The trail color.")]
        [SerializeField] protected SharedVariable<Color> m_TrailColor = Color.white;
        [Tooltip("The trail lifetime.")]
        [SerializeField] protected SharedVariable<float> m_TrailLifetime = 1.0f;

        private ParticleSystem m_ResolvedParticleSystem;
        private ParticleSystem.TrailModule m_TrailModule;

        /// <summary>
        /// Initializes the target GameObject.
        /// </summary>
        protected override void InitializeTarget()
        {
            base.InitializeTarget();

            m_ResolvedParticleSystem = m_ResolvedGameObject.GetComponent<ParticleSystem>();
        }

        /// <summary>
        /// Updates the trail settings.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_ResolvedParticleSystem == null) {
                return TaskStatus.Success;
            }

            m_TrailModule = m_ResolvedParticleSystem.trails;
            m_TrailModule.enabled = true;

            m_TrailModule.widthOverTrailMultiplier = m_WidthMultiplier.Value;
            m_TrailModule.colorOverLifetime = m_TrailColor.Value;
            m_TrailModule.lifetime = m_TrailLifetime.Value;

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_WidthMultiplier = 1.0f;
            m_TrailColor = Color.white;
            m_TrailLifetime = 1.0f;
        }
    }
}
#endif