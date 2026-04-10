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
    [Opsive.Shared.Utility.Description("Sets multiple CharacterController properties (height, radius, center, slope limit, step offset) with smooth transitions.")]
    public class SetProperties : TargetGameObjectAction
    {
        [Tooltip("The target height.")]
        [SerializeField] protected SharedVariable<float> m_Height = 2.0f;
        [Tooltip("The target radius.")]
        [SerializeField] protected SharedVariable<float> m_Radius = 0.5f;
        [Tooltip("The target center offset.")]
        [SerializeField] protected SharedVariable<Vector3> m_Center = Vector3.zero;
        [Tooltip("The slope limit (degrees).")]
        [SerializeField] protected SharedVariable<float> m_SlopeLimit = 45.0f;
        [Tooltip("The step offset.")]
        [SerializeField] protected SharedVariable<float> m_StepOffset = 0.3f;
        [Tooltip("The transition duration for height/radius/center (0 = instant).")]
        [SerializeField] protected SharedVariable<float> m_TransitionDuration = 0.0f;

        private CharacterController m_ResolvedCharacterController;
        private float m_StartHeight;
        private float m_StartRadius;
        private Vector3 m_StartCenter;
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

            m_ResolvedCharacterController = m_ResolvedGameObject.GetComponent<CharacterController>();
        }

        /// <summary>
        /// Called when the action starts.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();
            m_ElapsedTime = 0.0f;
            if (m_ResolvedCharacterController != null) {
                m_StartHeight = m_ResolvedCharacterController.height;
                m_StartRadius = m_ResolvedCharacterController.radius;
                m_StartCenter = m_ResolvedCharacterController.center;
            }
        }

        /// <summary>
        /// Updates the CharacterController properties.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_ResolvedCharacterController == null) {
                return TaskStatus.Success;
            }

            if (m_TransitionDuration.Value > 0.0f) {
                m_ElapsedTime += Time.deltaTime;
                var progress = Mathf.Clamp01(m_ElapsedTime / m_TransitionDuration.Value);

                m_ResolvedCharacterController.height = Mathf.Lerp(m_StartHeight, m_Height.Value, progress);
                m_ResolvedCharacterController.radius = Mathf.Lerp(m_StartRadius, m_Radius.Value, progress);
                m_ResolvedCharacterController.center = Vector3.Lerp(m_StartCenter, m_Center.Value, progress);
            } else {
                m_ResolvedCharacterController.height = m_Height.Value;
                m_ResolvedCharacterController.radius = m_Radius.Value;
                m_ResolvedCharacterController.center = m_Center.Value;
            }

            m_ResolvedCharacterController.slopeLimit = m_SlopeLimit.Value;
            m_ResolvedCharacterController.stepOffset = m_StepOffset.Value;

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_Height = 2.0f;
            m_Radius = 0.5f;
            m_Center = Vector3.zero;
            m_SlopeLimit = 45.0f;
            m_StepOffset = 0.3f;
            m_TransitionDuration = 0.0f;
        }
    }
}
#endif