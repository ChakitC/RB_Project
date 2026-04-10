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
    [Opsive.Shared.Utility.Description("Gets CharacterController bounds (center, extents, min, max, size) with world/local space options.")]
    public class GetBounds : TargetGameObjectAction
    {
        [Tooltip("Whether to return bounds in world space (true) or local space (false).")]
        [SerializeField] protected SharedVariable<bool> m_WorldSpace = true;
        [Tooltip("The bounds center (output).")]
        [SerializeField] [RequireShared] protected SharedVariable<Vector3> m_BoundsCenter;
        [Tooltip("The bounds extents (output).")]
        [SerializeField] [RequireShared] protected SharedVariable<Vector3> m_BoundsExtents;
        [Tooltip("The bounds min corner (output).")]
        [SerializeField] [RequireShared] protected SharedVariable<Vector3> m_BoundsMin;
        [Tooltip("The bounds max corner (output).")]
        [SerializeField] [RequireShared] protected SharedVariable<Vector3> m_BoundsMax;
        [Tooltip("The bounds size (output).")]
        [SerializeField] [RequireShared] protected SharedVariable<Vector3> m_BoundsSize;

        private CharacterController m_ResolvedCharacterController;

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
        /// Updates the bounds retrieval.
        /// </summary>
        /// <returns>The status of the action.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_ResolvedCharacterController == null) {
                return TaskStatus.Success;
            }

            var bounds = m_ResolvedCharacterController.bounds;

            if (m_WorldSpace.Value) {
                m_BoundsCenter.Value = bounds.center;
                m_BoundsExtents.Value = bounds.extents;
                m_BoundsMin.Value = bounds.min;
                m_BoundsMax.Value = bounds.max;
                m_BoundsSize.Value = bounds.size;
            } else {
                var localCenter = m_ResolvedTransform.InverseTransformPoint(bounds.center);
                var localExtents = m_ResolvedTransform.InverseTransformDirection(bounds.extents);
                m_BoundsCenter.Value = localCenter;
                m_BoundsExtents.Value = localExtents;
                m_BoundsMin.Value = localCenter - localExtents;
                m_BoundsMax.Value = localCenter + localExtents;
                m_BoundsSize.Value = localExtents * 2.0f;
            }

            return TaskStatus.Success;
        }

        /// <summary>
        /// Resets the action values back to their default.
        /// </summary>
        public override void Reset()
        {
            base.Reset();
            m_WorldSpace = true;
            m_BoundsCenter = null;
            m_BoundsExtents = null;
            m_BoundsMin = null;
            m_BoundsMax = null;
            m_BoundsSize = null;
        }
    }
}
#endif