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
    [Opsive.Shared.Utility.Description("Sets multiple Rigidbody2D properties including mass, drag, angular drag, gravity scale, and constraints.")]
    public class SetRigidbody2DProperties : TargetGameObjectAction
    {
        [Tooltip("The mass to set. Set to negative value to ignore.")]
        [SerializeField] protected SharedVariable<float> m_Mass = -1.0f;
        [Tooltip("The linear drag to set. Set to negative value to ignore.")]
        [SerializeField] protected SharedVariable<float> m_LinearDrag = -1.0f;
        [Tooltip("The angular drag to set. Set to negative value to ignore.")]
        [SerializeField] protected SharedVariable<float> m_AngularDrag = -1.0f;
        [Tooltip("The gravity scale to set. Set to negative value to ignore.")]
        [SerializeField] protected SharedVariable<float> m_GravityScale = -1.0f;
        [Tooltip("Whether to freeze X position.")]
        [SerializeField] protected SharedVariable<bool> m_FreezePositionX = false;
        [Tooltip("Whether to freeze Y position.")]
        [SerializeField] protected SharedVariable<bool> m_FreezePositionY = false;
        [Tooltip("Whether to freeze rotation.")]
        [SerializeField] protected SharedVariable<bool> m_FreezeRotation = false;
        [Tooltip("The body type to set.")]
        [SerializeField] protected RigidbodyType2D m_BodyType = RigidbodyType2D.Dynamic;
        [Tooltip("Whether to set the body type.")]
        [SerializeField] protected SharedVariable<bool> m_SetBodyType = false;

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
        /// Updates the Rigidbody2D properties.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_ResolvedRigidbody2D == null) {
                return TaskStatus.Success;
            }

            if (m_Mass.Value >= 0.0f) {
                m_ResolvedRigidbody2D.mass = m_Mass.Value;
            }

            if (m_LinearDrag.Value >= 0.0f) {
#if UNITY_6000_0_OR_NEWER
                m_ResolvedRigidbody2D.linearDamping = m_LinearDrag.Value;
#else
                m_ResolvedRigidbody2D.drag = m_LinearDrag.Value;
#endif
            }

            if (m_AngularDrag.Value >= 0.0f) {
#if UNITY_6000_0_OR_NEWER
                m_ResolvedRigidbody2D.angularDamping = m_AngularDrag.Value;
#else
                m_ResolvedRigidbody2D.angularDrag = m_AngularDrag.Value;
#endif
            }

            if (m_GravityScale.Value >= 0.0f) {
                m_ResolvedRigidbody2D.gravityScale = m_GravityScale.Value;
            }

            if (m_SetBodyType.Value) {
                m_ResolvedRigidbody2D.bodyType = m_BodyType;
            }

            var constraints = RigidbodyConstraints2D.None;
            if (m_FreezePositionX.Value) {
                constraints |= RigidbodyConstraints2D.FreezePositionX;
            }
            if (m_FreezePositionY.Value) {
                constraints |= RigidbodyConstraints2D.FreezePositionY;
            }
            if (m_FreezeRotation.Value) {
                constraints |= RigidbodyConstraints2D.FreezeRotation;
            }
            m_ResolvedRigidbody2D.constraints = constraints;

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_Mass = -1.0f;
            m_LinearDrag = -1.0f;
            m_AngularDrag = -1.0f;
            m_GravityScale = -1.0f;
            m_FreezePositionX = false;
            m_FreezePositionY = false;
            m_FreezeRotation = false;
            m_BodyType = RigidbodyType2D.Dynamic;
            m_SetBodyType = false;
        }
    }
}
#endif