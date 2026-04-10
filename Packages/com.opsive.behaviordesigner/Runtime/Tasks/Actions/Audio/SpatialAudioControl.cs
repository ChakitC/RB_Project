#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.AudioTasks
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("Audio")]
    [Opsive.Shared.Utility.Description("Controls 3D audio positioning, min/max distance, and spatial blend.")]
    public class SpatialAudioControl : TargetGameObjectAction
    {
        [Tooltip("The minimum distance for 3D audio.")]
        [SerializeField] protected SharedVariable<float> m_MinDistance = 1.0f;
        [Tooltip("The maximum distance for 3D audio.")]
        [SerializeField] protected SharedVariable<float> m_MaxDistance = 500.0f;
        [Tooltip("The spatial blend (0 = 2D, 1 = 3D).")]
        [SerializeField] protected SharedVariable<float> m_SpatialBlend = 1.0f;
        [Tooltip("The rolloff mode.")]
        [SerializeField] protected AudioRolloffMode m_RolloffMode = AudioRolloffMode.Logarithmic;

        private AudioSource m_ResolvedAudioSource;

        /// <summary>
        /// Initializes the target GameObject.
        /// </summary>
        protected override void InitializeTarget()
        {
            base.InitializeTarget();

            m_ResolvedAudioSource = m_ResolvedGameObject.GetComponent<AudioSource>();
        }

        /// <summary>
        /// Updates the spatial audio settings.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_ResolvedAudioSource == null) {
                return TaskStatus.Success;
            }

            m_ResolvedAudioSource.minDistance = m_MinDistance.Value;
            m_ResolvedAudioSource.maxDistance = m_MaxDistance.Value;
            m_ResolvedAudioSource.spatialBlend = Mathf.Clamp01(m_SpatialBlend.Value);
            m_ResolvedAudioSource.rolloffMode = m_RolloffMode;

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_MinDistance = 1.0f;
            m_MaxDistance = 500.0f;
            m_SpatialBlend = 1.0f;
            m_RolloffMode = AudioRolloffMode.Logarithmic;
        }
    }
}
#endif