#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.RenderingTasks
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using Opsive.BehaviorDesigner.Runtime.Tasks.Actions.TransformTasks;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("Rendering")]
    [Opsive.Shared.Utility.Description("Changes material property (color, float, vector) over time with curve.")]
    public class SetMaterialPropertyOverTime : TargetGameObjectAction
    {
        /// <summary>
        /// Specifies the property type.
        /// </summary>
        public enum PropertyType
        {
            Color,
            Float,
            Vector
        }

        [Tooltip("The material property name.")]
        [SerializeField] protected SharedVariable<string> m_PropertyName = "_Color";
        [Tooltip("The property type to animate.")]
        [SerializeField] protected PropertyType m_PropertyType = PropertyType.Color;
        [Tooltip("The starting value (Color, Float, or Vector).")]
        [SerializeField] protected SharedVariable<Color> m_StartColor = Color.white;
        [Tooltip("The starting float value.")]
        [SerializeField] protected SharedVariable<float> m_StartFloat = 0.0f;
        [Tooltip("The starting vector value.")]
        [SerializeField] protected SharedVariable<Vector4> m_StartVector = Vector4.zero;
        [Tooltip("The target value (Color, Float, or Vector).")]
        [SerializeField] protected SharedVariable<Color> m_TargetColor = Color.white;
        [Tooltip("The target float value.")]
        [SerializeField] protected SharedVariable<float> m_TargetFloat = 1.0f;
        [Tooltip("The target vector value.")]
        [SerializeField] protected SharedVariable<Vector4> m_TargetVector = Vector4.zero;
        [Tooltip("The animation duration.")]
        [SerializeField] protected SharedVariable<float> m_Duration = 1.0f;
        [Tooltip("The easing curve type.")]
        [SerializeField] protected SmoothMoveTo.EasingType m_EasingType = SmoothMoveTo.EasingType.Linear;

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
        /// Updates the material property.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_ResolvedRenderer == null) {
                return TaskStatus.Failure;
            }

            if (m_Material == null) {
                m_Material = m_ResolvedRenderer.material;
            }
            if (m_Material == null) {
                return TaskStatus.Failure;
            }

            if (string.IsNullOrEmpty(m_PropertyName.Value)) {
                return TaskStatus.Failure;
            }

            if (!m_Material.HasProperty(m_PropertyName.Value)) {
                return TaskStatus.Failure;
            }

            m_ElapsedTime += Time.deltaTime;
            var progress = Mathf.Clamp01(m_ElapsedTime / m_Duration.Value);
            var easedProgress = ApplyEasing(progress);

            switch (m_PropertyType) {
                case PropertyType.Color:
                    m_Material.SetColor(m_PropertyName.Value, Color.Lerp(m_StartColor.Value, m_TargetColor.Value, easedProgress));
                    break;
                case PropertyType.Float:
                    m_Material.SetFloat(m_PropertyName.Value, Mathf.Lerp(m_StartFloat.Value, m_TargetFloat.Value, easedProgress));
                    break;
                case PropertyType.Vector:
                    m_Material.SetVector(m_PropertyName.Value, Vector4.Lerp(m_StartVector.Value, m_TargetVector.Value, easedProgress));
                    break;
            }

            if (progress >= 1.0f) {
                return TaskStatus.Success;
            }

            return TaskStatus.Running;
        }

        /// <summary>
        /// Applies easing to the value.
        /// </summary>
        private float ApplyEasing(float t)
        {
            switch (m_EasingType) {
                case SmoothMoveTo.EasingType.EaseIn:
                    return t * t;
                case SmoothMoveTo.EasingType.EaseOut:
                    return 1.0f - (1.0f - t) * (1.0f - t);
                case SmoothMoveTo.EasingType.EaseInOut:
                    return t < 0.5f ? 2.0f * t * t : 1.0f - Mathf.Pow(-2.0f * t + 2.0f, 2.0f) / 2.0f;
                default:
                    return t;
            }
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_PropertyName = "_Color";
            m_PropertyType = PropertyType.Color;
            m_StartColor = Color.white;
            m_StartFloat = 0.0f;
            m_StartVector = Vector4.zero;
            m_TargetColor = Color.white;
            m_TargetFloat = 1.0f;
            m_TargetVector = Vector4.zero;
            m_Duration = 1.0f;
            m_EasingType = SmoothMoveTo.EasingType.Linear;
        }
    }
}
#endif