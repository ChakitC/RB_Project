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
    [Opsive.Shared.Utility.Description("Sets linear and angular drag with smooth transition.")]
    public class DragControl : TargetGameObjectAction
    {
        [Tooltip("The target linear drag.")]
        [SerializeField] protected SharedVariable<float> m_LinearDrag = 0.0f;
        [Tooltip("The target angular drag.")]
        [SerializeField] protected SharedVariable<float> m_AngularDrag = 0.05f;
        [Tooltip("The transition duration (0 = instant).")]
        [SerializeField] protected SharedVariable<float> m_TransitionDuration = 0.0f;

        private UnityEngine.Rigidbody m_ResolvedRigidbody;
        private float m_StartLinearDrag;
        private float m_StartAngularDrag;
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
#if UNITY_6000_0_OR_NEWER
                m_StartLinearDrag = m_ResolvedRigidbody.linearDamping;
                m_StartAngularDrag = m_ResolvedRigidbody.angularDamping;
#else
                m_StartLinearDrag = m_ResolvedRigidbody.drag;
                m_StartAngularDrag = m_ResolvedRigidbody.angularDrag;
#endif
            }
        }

        /// <summary>
        /// Updates the drag values.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_ResolvedRigidbody == null) {
                return TaskStatus.Success;
            }

#if UNITY_6000_0_OR_NEWER
            if (m_TransitionDuration.Value > 0.0f) {
                m_ElapsedTime += Time.deltaTime;
                var progress = Mathf.Clamp01(m_ElapsedTime / m_TransitionDuration.Value);
                m_ResolvedRigidbody.linearDamping = Mathf.Lerp(m_StartLinearDrag, m_LinearDrag.Value, progress);
                m_ResolvedRigidbody.angularDamping = Mathf.Lerp(m_StartAngularDrag, m_AngularDrag.Value, progress);
            } else {
                m_ResolvedRigidbody.linearDamping = m_LinearDrag.Value;
                m_ResolvedRigidbody.angularDamping = m_AngularDrag.Value;
            }
#else
            if (m_TransitionDuration.Value > 0.0f) {
                m_ElapsedTime += Time.deltaTime;
                var progress = Mathf.Clamp01(m_ElapsedTime / m_TransitionDuration.Value);
                m_ResolvedRigidbody.drag = Mathf.Lerp(m_StartLinearDrag, m_LinearDrag.Value, progress);
                m_ResolvedRigidbody.angularDrag = Mathf.Lerp(m_StartAngularDrag, m_AngularDrag.Value, progress);
            } else {
                m_ResolvedRigidbody.drag = m_LinearDrag.Value;
                m_ResolvedRigidbody.angularDrag = m_AngularDrag.Value;
            }
#endif

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_LinearDrag = 0.0f;
            m_AngularDrag = 0.05f;
            m_TransitionDuration = 0.0f;
        }
    }
}
#endif