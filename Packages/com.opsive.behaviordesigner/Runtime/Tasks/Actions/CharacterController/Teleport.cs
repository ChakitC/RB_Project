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
    [Opsive.Shared.Utility.Description("Teleports CharacterController to position with validation, ground snapping, and rotation.")]
    public class Teleport : TargetGameObjectAction
    {
        private const int c_DataVersion = 1;

        [Tooltip("The target position.")]
        [SerializeField] protected SharedVariable<Vector3> m_TargetPosition;
        [Tooltip("Should the target rotation be applied?")]
        [SerializeField] protected SharedVariable<bool> m_ApplyRotation = false;
        [Tooltip("The target rotation (Euler angles). Only used if Apply Rotation is enabled.")]
        [SerializeField] protected SharedVariable<Vector3> m_TargetRotation = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        [Tooltip("Whether to snap to ground after teleport.")]
        [SerializeField] protected SharedVariable<bool> m_SnapToGround = false;
        [Tooltip("The ground detection distance for snapping.")]
        [SerializeField] protected SharedVariable<float> m_GroundSnapDistance = 1.0f;
        [Tooltip("The layer mask for ground detection.")]
        [SerializeField] protected LayerMask m_GroundLayerMask = -1;
        [Tooltip("Whether the teleport was successful (output).")]
        [SerializeField] [RequireShared] protected SharedVariable<bool> m_TeleportSuccessful;
        [HideInInspector] [Tooltip("The serialized data version.")]
        [SerializeField] private int m_DataVersion;

        private CharacterController m_ResolvedCharacterController;
        private bool m_HasTeleported;

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
            MigrateLegacyFields();
            m_HasTeleported = false;
            m_TeleportSuccessful.Value = false;
        }

        /// <summary>
        /// Updates the teleport operation.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_ResolvedCharacterController == null) {
                return TaskStatus.Success;
            }

            MigrateLegacyFields();
            if (!m_HasTeleported) {
                var targetPosition = m_TargetPosition.Value;

                if (m_SnapToGround.Value) {
                    RaycastHit hit;
                    var origin = targetPosition + Vector3.up * m_GroundSnapDistance.Value;
                    if (Physics.Raycast(origin, Vector3.down, out hit, m_GroundSnapDistance.Value * 2.0f, m_GroundLayerMask)) {
                        targetPosition.y = hit.point.y + m_ResolvedCharacterController.height * 0.5f + m_ResolvedCharacterController.skinWidth;
                    }
                }

                m_ResolvedCharacterController.enabled = false;
                m_ResolvedTransform.position = targetPosition;
                m_ResolvedCharacterController.enabled = true;

                if (m_ApplyRotation.Value) {
                    m_ResolvedTransform.eulerAngles = m_TargetRotation.Value;
                }

                m_HasTeleported = true;
                m_TeleportSuccessful.Value = true;
            }

            return TaskStatus.Success;
        }

        /// <summary>
        /// Migrates legacy sentinel-based rotation fields to the explicit apply rotation toggle.
        /// </summary>
        public void MigrateLegacyFields()
        {
            if (m_DataVersion >= c_DataVersion) {
                return;
            }

            if (m_TargetRotation.IsShared || m_TargetRotation.Value.x != float.NegativeInfinity) {
                m_ApplyRotation.Value = true;
            }
            m_DataVersion = c_DataVersion;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_TargetPosition = null;
            m_ApplyRotation = false;
            m_TargetRotation = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            m_SnapToGround = false;
            m_GroundSnapDistance = 1.0f;
            m_GroundLayerMask = -1;
            m_TeleportSuccessful = null;
            m_DataVersion = c_DataVersion;
        }
    }
}
#endif