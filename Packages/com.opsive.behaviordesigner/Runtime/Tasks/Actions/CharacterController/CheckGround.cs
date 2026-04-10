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
    [Opsive.Shared.Utility.Description("Checks ground beneath CharacterController with distance, normal, and surface detection.")]
    public class CheckGround : TargetGameObjectAction
    {
        [Tooltip("The ground check distance.")]
        [SerializeField] protected SharedVariable<float> m_GroundCheckDistance = 1.0f;
        [Tooltip("The layer mask for ground detection.")]
        [SerializeField] protected LayerMask m_GroundLayerMask = -1;
        [Tooltip("Whether ground was detected (output).")]
        [SerializeField] [RequireShared] protected SharedVariable<bool> m_GroundDetected;
        [Tooltip("The distance to ground (output).")]
        [SerializeField] [RequireShared] protected SharedVariable<float> m_GroundDistance;
        [Tooltip("The ground surface normal (output).")]
        [SerializeField] [RequireShared] protected SharedVariable<Vector3> m_GroundNormal;
        [Tooltip("The ground hit point (output).")]
        [SerializeField] [RequireShared] protected SharedVariable<Vector3> m_GroundHitPoint;
        [Tooltip("The GameObject that was hit (output).")]
        [SerializeField] [RequireShared] protected SharedVariable<GameObject> m_GroundGameObject;

        private CharacterController m_ResolvedCharacterController;

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
        /// Updates the ground check.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_ResolvedCharacterController == null) {
                m_GroundDetected.Value = false;
                m_GroundDistance.Value = float.MaxValue;
                return TaskStatus.Success;
            }

            var origin = m_ResolvedTransform.position + m_ResolvedCharacterController.center;
            var maxDistance = m_GroundCheckDistance.Value + m_ResolvedCharacterController.height * 0.5f;

            RaycastHit hit;
            var groundDetected = Physics.Raycast(origin, Vector3.down, out hit, maxDistance, m_GroundLayerMask);

            if (groundDetected) {
                m_GroundDetected.Value = true;
                m_GroundDistance.Value = hit.distance - m_ResolvedCharacterController.height * 0.5f;
                m_GroundNormal.Value = hit.normal;
                m_GroundHitPoint.Value = hit.point;
                m_GroundGameObject.Value = hit.collider.gameObject;
            } else {
                m_GroundDetected.Value = false;
                m_GroundDistance.Value = float.MaxValue;
                m_GroundNormal.Value = Vector3.up;
                m_GroundHitPoint.Value = Vector3.zero;
                m_GroundGameObject.Value = null;
            }

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_GroundCheckDistance = 1.0f;
            m_GroundLayerMask = -1;
            m_GroundDetected = null;
            m_GroundDistance = null;
            m_GroundNormal = null;
            m_GroundHitPoint = null;
            m_GroundGameObject = null;
        }
    }
}
#endif