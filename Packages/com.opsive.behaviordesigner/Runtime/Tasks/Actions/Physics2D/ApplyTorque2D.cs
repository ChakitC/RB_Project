#if GRAPH_DESIGNER
/// ---------------------------------------------
/// Behavior Designer
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.Runtime.Tasks.Actions.Physics2DTasks
{
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    [Opsive.Shared.Utility.Category("Physics2D")]
    [Opsive.Shared.Utility.Description("Applies torque (rotational force) to Rigidbody2D with mode selection.")]
    public class ApplyTorque2D : TargetGameObjectAction
    {
        /// <summary>
        /// Specifies the force mode.
        /// </summary>
        public enum ForceModeType
        {
            Force,
            Impulse
        }

        [Tooltip("The torque to apply (in degrees per second squared for Force mode, or degrees per second for Impulse mode).")]
        [SerializeField] protected SharedVariable<float> m_Torque = 10.0f;
        [Tooltip("The force mode to use.")]
        [SerializeField] protected ForceModeType m_ForceMode = ForceModeType.Force;
        [Tooltip("The maximum torque to apply (clamps the torque value).")]
        [SerializeField] protected SharedVariable<float> m_MaxTorque = Mathf.Infinity;

        private Rigidbody2D m_ResolvedRigidbody2D;

        /// <summary>
        /// Called when the action starts.
        /// </summary>
        /// <summary>
        /// Initializes the target GameObject.
        /// </summary>
        protected override void InitializeTarget()
        {
            base.InitializeTarget();

            m_ResolvedRigidbody2D = m_ResolvedGameObject.GetComponent<Rigidbody2D>();
        }

        /// <summary>
        /// Applies the torque to the Rigidbody2D.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_ResolvedRigidbody2D == null) {
                return TaskStatus.Success;
            }

            m_ResolvedRigidbody2D.AddTorque(Mathf.Clamp(m_Torque.Value, -m_MaxTorque.Value, m_MaxTorque.Value), (ForceMode2D)(int)m_ForceMode);

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_Torque = 10.0f;
            m_ForceMode = ForceModeType.Force;
            m_MaxTorque = Mathf.Infinity;
        }
    }
}
#endif