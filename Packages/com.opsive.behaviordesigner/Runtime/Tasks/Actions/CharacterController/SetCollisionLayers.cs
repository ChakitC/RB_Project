#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.CharacterControllerTasks
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("Character Controller")]
    [Opsive.Shared.Utility.Description("Sets CharacterController collision detection layers (include/exclude) with validation.")]
    public class SetCollisionLayers : TargetGameObjectAction
    {
        [Tooltip("The include layers (layers to detect collisions with).")]
        [SerializeField] protected LayerMask m_IncludeLayers = -1;
        [Tooltip("The exclude layers (layers to ignore collisions with).")]
        [SerializeField] protected LayerMask m_ExcludeLayers = 0;
        [Tooltip("The layer override priority.")]
        [SerializeField] protected SharedVariable<int> m_LayerOverridePriority = 0;

        private CharacterController m_ResolvedCharacterController;

        /// <summary>
        /// Initializes the target GameObject.
        /// </summary>
        protected override void InitializeTarget()
        {
            base.InitializeTarget();

            m_ResolvedCharacterController = m_ResolvedGameObject.GetComponent<CharacterController>();
        }

        /// <summary>
        /// Updates the collision layer settings.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_ResolvedCharacterController == null) {
                return TaskStatus.Success;
            }

            m_ResolvedCharacterController.includeLayers = m_IncludeLayers;
            m_ResolvedCharacterController.excludeLayers = m_ExcludeLayers;
            m_ResolvedCharacterController.layerOverridePriority = m_LayerOverridePriority.Value;

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_IncludeLayers = -1;
            m_ExcludeLayers = 0;
            m_LayerOverridePriority = 0;
        }
    }
}
#endif