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
    [Opsive.Shared.Utility.Description("Sets Rigidbody constraints with freeze position/rotation and interpolation mode.")]
    public class SetConstraints : TargetGameObjectAction
    {
        [Tooltip("Freeze position on X axis.")]
        [SerializeField] protected SharedVariable<bool> m_FreezePositionX = false;
        [Tooltip("Freeze position on Y axis.")]
        [SerializeField] protected SharedVariable<bool> m_FreezePositionY = false;
        [Tooltip("Freeze position on Z axis.")]
        [SerializeField] protected SharedVariable<bool> m_FreezePositionZ = false;
        [Tooltip("Freeze rotation on X axis.")]
        [SerializeField] protected SharedVariable<bool> m_FreezeRotationX = false;
        [Tooltip("Freeze rotation on Y axis.")]
        [SerializeField] protected SharedVariable<bool> m_FreezeRotationY = false;
        [Tooltip("Freeze rotation on Z axis.")]
        [SerializeField] protected SharedVariable<bool> m_FreezeRotationZ = false;
        [Tooltip("The interpolation mode.")]
        [SerializeField] protected RigidbodyInterpolation m_Interpolation = RigidbodyInterpolation.None;

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
        /// Updates the constraints.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_ResolvedRigidbody == null) {
                return TaskStatus.Success;
            }

            var constraints = RigidbodyConstraints.None;
            if (m_FreezePositionX.Value) {
                constraints |= RigidbodyConstraints.FreezePositionX;
            }
            if (m_FreezePositionY.Value) {
                constraints |= RigidbodyConstraints.FreezePositionY;
            }
            if (m_FreezePositionZ.Value) {
                constraints |= RigidbodyConstraints.FreezePositionZ;
            }
            if (m_FreezeRotationX.Value) {
                constraints |= RigidbodyConstraints.FreezeRotationX;
            }
            if (m_FreezeRotationY.Value) {
                constraints |= RigidbodyConstraints.FreezeRotationY;
            }
            if (m_FreezeRotationZ.Value) {
                constraints |= RigidbodyConstraints.FreezeRotationZ;
            }

            m_ResolvedRigidbody.constraints = constraints;
            m_ResolvedRigidbody.interpolation = m_Interpolation;

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_FreezePositionX = false;
            m_FreezePositionY = false;
            m_FreezePositionZ = false;
            m_FreezeRotationX = false;
            m_FreezeRotationY = false;
            m_FreezeRotationZ = false;
            m_Interpolation = RigidbodyInterpolation.None;
        }
    }
}
#endif