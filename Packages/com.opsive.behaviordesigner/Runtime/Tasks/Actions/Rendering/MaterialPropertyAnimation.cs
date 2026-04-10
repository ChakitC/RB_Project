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
    [Opsive.Shared.Utility.Description("Animates material property with easing curves.")]
    public class MaterialPropertyAnimation : TargetGameObjectAction
    {
        /// <summary>
        /// Specifies the property type to animate.
        /// </summary>
        public enum PropertyType
        {
            Float,
            Color,
            Vector
        }

        [Tooltip("The property type to animate.")]
        [SerializeField] protected PropertyType m_PropertyType = PropertyType.Float;
        [Tooltip("The shader property name.")]
        [SerializeField] protected SharedVariable<string> m_PropertyName = "_Color";
        [Tooltip("The animation curve for the property value.")]
        [SerializeField] protected AnimationCurve m_AnimationCurve = AnimationCurve.Linear(0, 0, 1, 1);
        [Tooltip("The duration to evaluate the curve over.")]
        [SerializeField] protected SharedVariable<float> m_Duration = 1.0f;
        [Tooltip("The value multiplier (for float/vector).")]
        [SerializeField] protected SharedVariable<float> m_ValueMultiplier = 1.0f;
        [Tooltip("The color multiplier (for color).")]
        [SerializeField] protected SharedVariable<Color> m_ColorMultiplier = Color.white;

        private Renderer m_ResolvedRenderer;
        private Material m_Material;
        private float m_ElapsedTime;

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
        /// Updates the material property animation.
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
            var curveValue = m_AnimationCurve.Evaluate(progress);

            switch (m_PropertyType) {
                case PropertyType.Float:
                    m_Material.SetFloat(propertyName, curveValue * m_ValueMultiplier.Value);
                    break;
                case PropertyType.Color:
                    m_Material.SetColor(propertyName, m_ColorMultiplier.Value * curveValue);
                    break;
                case PropertyType.Vector:
                    m_Material.SetVector(propertyName, Vector4.one * curveValue * m_ValueMultiplier.Value);
                    break;
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
            m_PropertyType = PropertyType.Float;
            m_PropertyName = "_Color";
            m_AnimationCurve = AnimationCurve.Linear(0, 0, 1, 1);
            m_Duration = 1.0f;
            m_ValueMultiplier = 1.0f;
            m_ColorMultiplier = Color.white;
        }
    }
}
#endif