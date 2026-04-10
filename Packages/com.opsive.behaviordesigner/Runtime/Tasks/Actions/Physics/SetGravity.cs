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
    [Opsive.Shared.Utility.Description("Sets global or local gravity with smooth transition.")]
    public class SetGravity : Action
    {
        [Tooltip("The target gravity vector.")]
        [SerializeField] protected SharedVariable<Vector3> m_Gravity = new Vector3(0, -9.81f, 0);
        [Tooltip("The transition duration (0 = instant).")]
        [SerializeField] protected SharedVariable<float> m_TransitionDuration = 0.0f;
        [Tooltip("Whether to set global Physics.gravity or local Rigidbody gravity.")]
        [SerializeField] protected SharedVariable<bool> m_UseGlobalGravity = true;
        [Tooltip("The Rigidbody to set local gravity on. Only used if Use Global Gravity is false.")]
        [SerializeField] protected SharedVariable<Rigidbody> m_Rigidbody;

        private Vector3 m_StartGravity;
        private float m_ElapsedTime;

        /// <summary>
        /// Called when the action starts.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();
            m_ElapsedTime = 0.0f;

            if (m_UseGlobalGravity.Value) {
                m_StartGravity = Physics.gravity;
            } else if (m_Rigidbody.Value != null) {
                m_StartGravity = Physics.gravity;
            }
        }

        /// <summary>
        /// Updates the gravity.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_TransitionDuration.Value > 0.0f) {
                m_ElapsedTime += Time.deltaTime;
                var progress = Mathf.Clamp01(m_ElapsedTime / m_TransitionDuration.Value);
                var currentGravity = Vector3.Lerp(m_StartGravity, m_Gravity.Value, progress);

                if (m_UseGlobalGravity.Value) {
                    Physics.gravity = currentGravity;
                } else if (m_Rigidbody.Value != null) {
                    m_Rigidbody.Value.useGravity = false;
                    m_Rigidbody.Value.AddForce(currentGravity, ForceMode.Acceleration);
                }
            } else {
                if (m_UseGlobalGravity.Value) {
                    Physics.gravity = m_Gravity.Value;
                } else if (m_Rigidbody.Value != null) {
                    m_Rigidbody.Value.useGravity = false;
                    m_Rigidbody.Value.AddForce(m_Gravity.Value, ForceMode.Acceleration);
                }
            }

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_Gravity = new Vector3(0, -9.81f, 0);
            m_TransitionDuration = 0.0f;
            m_UseGlobalGravity = true;
            m_Rigidbody = null;
        }
    }
}
#endif