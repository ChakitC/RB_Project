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
    [Opsive.Shared.Utility.Description("Sets mass with smooth transition and optional center of mass adjustment.")]
    public class SetMass : TargetGameObjectAction
    {
        [Tooltip("The target mass.")]
        [SerializeField] protected SharedVariable<float> m_TargetMass = 1.0f;
        [Tooltip("The transition duration (0 = instant).")]
        [SerializeField] protected SharedVariable<float> m_TransitionDuration = 0.0f;
        [Tooltip("The center of mass offset.")]
        [SerializeField] protected SharedVariable<Vector3> m_CenterOfMassOffset = Vector3.zero;

        private UnityEngine.Rigidbody m_ResolvedRigidbody;
        private float m_StartMass;
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

            m_ResolvedRigidbody = m_ResolvedGameObject.GetComponent<UnityEngine.Rigidbody>();
        }

        /// <summary>
        /// Called when the action starts.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();
            m_ElapsedTime = 0.0f;
            if (m_ResolvedRigidbody != null) {
                m_StartMass = m_ResolvedRigidbody.mass;
            }
        }

        /// <summary>
        /// Updates the Rigidbody mass.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_ResolvedRigidbody == null) {
                return TaskStatus.Success;
            }

            if (m_TransitionDuration.Value > 0.0f) {
                m_ElapsedTime += Time.deltaTime;
                var progress = Mathf.Clamp01(m_ElapsedTime / m_TransitionDuration.Value);
                m_ResolvedRigidbody.mass = Mathf.Lerp(m_StartMass, m_TargetMass.Value, progress);
            } else {
                m_ResolvedRigidbody.mass = m_TargetMass.Value;
            }

            m_ResolvedRigidbody.centerOfMass = m_CenterOfMassOffset.Value;

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_TargetMass = 1.0f;
            m_TransitionDuration = 0.0f;
            m_CenterOfMassOffset = Vector3.zero;
        }
    }
}
#endif