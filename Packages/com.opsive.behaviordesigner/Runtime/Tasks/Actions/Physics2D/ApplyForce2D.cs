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
    [Opsive.Shared.Utility.Description("Applies force to Rigidbody2D with mode selection and optional relative direction.")]
    public class ApplyForce2D : TargetGameObjectAction
    {
        /// <summary>
        /// Specifies the force mode.
        /// </summary>
        public enum ForceModeType
        {
            Force,
            Impulse
        }

        [Tooltip("The force vector to apply.")]
        [SerializeField] protected SharedVariable<Vector2> m_Force;
        [Tooltip("The force mode to use.")]
        [SerializeField] protected ForceModeType m_ForceMode = ForceModeType.Force;
        [Tooltip("Whether to apply the force in local space relative to the Rigidbody2D.")]
        [SerializeField] protected SharedVariable<bool> m_UseLocalSpace = false;
        [Tooltip("Whether to apply force at a specific position.")]
        [SerializeField] protected SharedVariable<bool> m_ApplyAtPosition = false;
        [Tooltip("The position to apply force at. Only used if Apply At Position is true.")]
        [SerializeField] protected SharedVariable<Vector2> m_Position;

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
        /// Applies the force to the Rigidbody2D.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_ResolvedRigidbody2D == null) {
                return TaskStatus.Success;
            }

            Vector2 force;
            if (m_UseLocalSpace.Value) {
                var angle = m_ResolvedRigidbody2D.rotation * Mathf.Deg2Rad;
                var cos = Mathf.Cos(angle);
                var sin = Mathf.Sin(angle);
                var forceValue = m_Force.Value;
                force = new Vector2(forceValue.x * cos - forceValue.y * sin, forceValue.x * sin + forceValue.y * cos);
            } else {
                force = m_Force.Value;
            }

            if (m_ApplyAtPosition.Value) {
                m_ResolvedRigidbody2D.AddForceAtPosition(force, m_Position.Value, (ForceMode2D)(int)m_ForceMode);
            } else {
                m_ResolvedRigidbody2D.AddForce(force, (ForceMode2D)(int)m_ForceMode);
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