#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.RigidbodyTasks
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("Rigidbody")]
    [Opsive.Shared.Utility.Description("Applies force at specific position with mode and optional relative direction.")]
    public class ApplyForceAtPosition : TargetGameObjectAction
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
        [Tooltip("The position to apply force at.")]
        [SerializeField] protected SharedVariable<Vector3> m_Position;
        [Tooltip("The force mode to use.")]
        [SerializeField] protected ForceModeType m_ForceMode = ForceModeType.Force;
        [Tooltip("Whether to apply the force in local space relative to the Rigidbody.")]
        [SerializeField] protected SharedVariable<bool> m_UseLocalSpace = false;

        private UnityEngine.Rigidbody m_ResolvedRigidbody;

        /// <summary>
        /// Initializes the target GameObject.
        /// </summary>
        protected override void InitializeTarget()
        {
            base.InitializeTarget();

            m_ResolvedRigidbody = m_ResolvedGameObject.GetComponent<UnityEngine.Rigidbody>();
        }

        /// <summary>
        /// Applies the force at position.
        /// </summary>
        public override TaskStatus OnUpdate()
        {
            if (m_ResolvedRigidbody == null) {
                return TaskStatus.Success;
            }

            var force = m_Force.Value;
            var position = m_Position.Value;

            if (m_UseLocalSpace.Value) {
                force = m_ResolvedRigidbody.transform.TransformDirection(force);
                position = m_ResolvedRigidbody.transform.TransformPoint(position);
            }

            var forceMode = (ForceMode)(int)m_ForceMode;
            m_ResolvedRigidbody.AddForceAtPosition(force, position, forceMode);

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_Force = null;
            m_Position = null;
            m_ForceMode = ForceModeType.Force;
            m_UseLocalSpace = false;
        }
    }
}
#endif