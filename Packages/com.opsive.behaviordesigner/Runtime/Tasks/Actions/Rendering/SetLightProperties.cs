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
    [Opsive.Shared.Utility.Description("Sets light color, intensity, range, and type with smooth transition.")]
    public class SetLightProperties : TargetGameObjectAction
    {
        [Tooltip("The light color.")]
        [SerializeField] protected SharedVariable<Color> m_LightColor = Color.white;
        [Tooltip("The light intensity.")]
        [SerializeField] protected SharedVariable<float> m_Intensity = 1.0f;
        [Tooltip("The light range.")]
        [SerializeField] protected SharedVariable<float> m_Range = 10.0f;
        [Tooltip("The light type.")]
        [SerializeField] protected LightType m_LightType = LightType.Point;
        [Tooltip("The transition duration for color/intensity (0 = instant).")]
        [SerializeField] protected SharedVariable<float> m_TransitionDuration = 0.0f;

        private Light m_ResolvedLight;
        private Color m_StartColor;
        private float m_StartIntensity;
        private float m_ElapsedTime;

        /// <summary>
        /// Called when the action starts.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();
            m_ElapsedTime = 0.0f;
            m_ResolvedLight = m_ResolvedGameObject.GetComponent<Light>();
            if (m_ResolvedLight != null) {
                m_StartColor = m_ResolvedLight.color;
                m_StartIntensity = m_ResolvedLight.intensity;
            }
        }

        /// <summary>
        /// Updates the light properties.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_ResolvedLight == null) {
                return TaskStatus.Success;
            }

            m_ResolvedLight.type = m_LightType;

            m_ResolvedLight.range = m_Range.Value;

            if (m_TransitionDuration.Value > 0.0f) {
                m_ElapsedTime += Time.deltaTime;
                var progress = Mathf.Clamp01(m_ElapsedTime / m_TransitionDuration.Value);

                m_ResolvedLight.color = Color.Lerp(m_StartColor, m_LightColor.Value, progress);
                m_ResolvedLight.intensity = Mathf.Lerp(m_StartIntensity, m_Intensity.Value, progress);
            } else {
                m_ResolvedLight.color = m_LightColor.Value;
                m_ResolvedLight.intensity = m_Intensity.Value;
            }

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_LightColor = Color.white;
            m_Intensity = 1.0f;
            m_Range = 10.0f;
            m_LightType = LightType.Point;
            m_TransitionDuration = 0.0f;
        }
    }
}
#endif