#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.PhysicsTasks
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("Physics")]
    [Opsive.Shared.Utility.Description("Applies force to Rigidbody with mode selection and optional relative direction.")]
    public class ApplyForceWithMode : TargetGameObjectAction
    {
        /// <summary>
        /// Specifies the force mode.
        /// </summary>
        public enum ForceModeType
        {
            Force,
            Impulse,
            Acceleration,
            VelocityChange
        }

        [Tooltip("The force vector to apply.")]
        [SerializeField] protected SharedVariable<Vector3> m_Force;
        [Tooltip("The force mode to use.")]
        [SerializeField] protected ForceModeType m_ForceMode = ForceModeType.Force;
        [Tooltip("Whether to apply the force in local space relative to the Rigidbody.")]
        [SerializeField] protected SharedVariable<bool> m_UseLocalSpace = false;
        [Tooltip("Whether to apply force at a specific position.")]
        [SerializeField] protected SharedVariable<bool> m_ApplyAtPosition = false;
        [Tooltip("The position to apply force at. Only used if Apply At Position is true.")]
        [SerializeField] protected SharedVariable<Vector3> m_Position;

        private Rigidbody m_ResolvedRigidbody;

        /// <summary>
        /// Initializes the target GameObject.
        /// </summary>
        protected override void InitializeTarget()
        {
            base.InitializeTarget();

            m_ResolvedRigidbody = m_ResolvedGameObject.GetComponent<Rigidbody>();
        }

        /// <summary>
        /// Applies the force to the Rigidbody.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_ResolvedRigidbody == null) {
                return TaskStatus.Success;
            }

            var force = m_Force.Value;

            if (m_UseLocalSpace.Value) {
                force = m_ResolvedRigidbody.transform.TransformDirection(force);
            }

            var forceMode = (ForceMode)(int)m_ForceMode;

            if (m_ApplyAtPosition.Value) {
                m_ResolvedRigidbody.AddForceAtPosition(force, m_Position.Value, forceMode);
            } else {
                m_ResolvedRigidbody.AddForce(force, forceMode);
            }

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_Force = null;
            m_ForceMode = ForceModeType.Force;
            m_UseLocalSpace = false;
            m_ApplyAtPosition = false;
            m_Position = null;
        }
    }
}
#endif