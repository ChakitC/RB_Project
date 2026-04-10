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
    [Opsive.Shared.Utility.Description("Sets shadow type, strength, and resolution with validation.")]
    public class SetShadowSettings : TargetGameObjectAction
    {
        [Tooltip("The shadow casting mode.")]
        [SerializeField] protected UnityEngine.Rendering.ShadowCastingMode m_ShadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        [Tooltip("Whether to receive shadows.")]
        [SerializeField] protected SharedVariable<bool> m_ReceiveShadows = true;

        private Renderer m_ResolvedRenderer;

        /// <summary>
        /// Called when the action starts.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();
            m_ResolvedRenderer = m_ResolvedGameObject.GetComponent<Renderer>();
        }

        /// <summary>
        /// Updates the shadow settings.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_ResolvedRenderer == null) {
                return TaskStatus.Success;
            }

            m_ResolvedRenderer.shadowCastingMode = m_ShadowCastingMode;
            m_ResolvedRenderer.receiveShadows = m_ReceiveShadows.Value;

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_ShadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            m_ReceiveShadows = true;
        }
    }
}
#endif