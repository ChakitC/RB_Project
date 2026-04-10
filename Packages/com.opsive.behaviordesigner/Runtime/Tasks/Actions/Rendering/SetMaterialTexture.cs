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
    [Opsive.Shared.Utility.Description("Sets material texture with optional tiling and offset.")]
    public class SetMaterialTexture : TargetGameObjectAction
    {
        [Tooltip("The shader property name for the texture (default: _MainTex).")]
        [SerializeField] protected SharedVariable<string> m_PropertyName = "_MainTex";
        [Tooltip("The texture to set.")]
        [SerializeField] protected SharedVariable<Texture> m_Texture;
        [Tooltip("The texture tiling (x, y).")]
        [SerializeField] protected SharedVariable<Vector2> m_Tiling = Vector2.one;
        [Tooltip("The texture offset (x, y).")]
        [SerializeField] protected SharedVariable<Vector2> m_Offset = Vector2.zero;

        private Renderer m_ResolvedRenderer;
        private Material m_Material;

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
        /// Updates the texture settings.
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

            var propertyName = string.IsNullOrEmpty(m_PropertyName.Value) ? "_MainTex" : m_PropertyName.Value;
            if (!m_Material.HasProperty(propertyName)) {
                return TaskStatus.Success;
            }

            if (m_Texture.Value != null) {
                m_Material.SetTexture(propertyName, m_Texture.Value);
            }

            m_Material.SetTextureScale(propertyName, m_Tiling.Value);
            m_Material.SetTextureOffset(propertyName, m_Offset.Value);

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_PropertyName = "_MainTex";
            m_Texture = null;
            m_Tiling = Vector2.one;
            m_Offset = Vector2.zero;
        }
    }
}
#endif