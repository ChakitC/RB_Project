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
    [Opsive.Shared.Utility.Description("Fades material alpha with duration and optional shader property name.")]
    public class FadeMaterialAlpha : TargetGameObjectAction
    {
        [Tooltip("The shader property name for alpha (default: _Color).")]
        [SerializeField] protected SharedVariable<string> m_PropertyName = "_Color";
        [Tooltip("The target alpha value.")]
        [SerializeField] protected SharedVariable<float> m_TargetAlpha = 1.0f;
        [Tooltip("The fade duration.")]
        [SerializeField] protected SharedVariable<float> m_Duration = 1.0f;

        private Renderer m_ResolvedRenderer;
        private Material m_Material;
        private float m_StartAlpha;
        private float m_ElapsedTime;

        /// <summary>
        /// Called when the action starts.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();
            m_ElapsedTime = 0.0f;
            m_ResolvedRenderer = m_ResolvedGameObject.GetComponent<Renderer>();
            if (m_ResolvedRenderer != null) {
                m_Material = m_ResolvedRenderer.material;
                if (m_Material != null) {
                    var propertyName = string.IsNullOrEmpty(m_PropertyName.Value) ? "_Color" : m_PropertyName.Value;
                    if (m_Material.HasProperty(propertyName)) {
                    m_StartAlpha = m_Material.GetColor(propertyName).a;
                    }
                }
            }
        }

        /// <summary>
        /// Updates the material alpha fade.
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

            var propertyName = string.IsNullOrEmpty(m_PropertyName.Value) ? "_Color" : m_PropertyName.Value;
            if (!m_Material.HasProperty(propertyName)) {
                return TaskStatus.Success;
            }

            m_ElapsedTime += Time.deltaTime;
            var progress = Mathf.Clamp01(m_ElapsedTime / m_Duration.Value);
            var targetAlpha = Mathf.Clamp01(m_TargetAlpha.Value);
            var currentAlpha = Mathf.Lerp(m_StartAlpha, targetAlpha, progress);

            var color = m_Material.GetColor(propertyName);
            color.a = currentAlpha;
            m_Material.SetColor(propertyName, color);

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
            m_PropertyName = "_Color";
            m_TargetAlpha = 1.0f;
            m_Duration = 1.0f;
        }
    }
}
#endif