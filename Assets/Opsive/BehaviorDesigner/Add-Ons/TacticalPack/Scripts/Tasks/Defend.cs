/// ---------------------------------------------
/// Tactical Pack for Behavior Designer Pro
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.AddOns.TacticalPack.Runtime.Tasks
{
    using Opsive.BehaviorDesigner.Runtime.Tasks;
    using Opsive.GraphDesigner.Runtime;
    using Opsive.GraphDesigner.Runtime.Variables;
    using UnityEngine;

    [Opsive.Shared.Utility.Description("Defends a specified object or position. The agents will form a defensive perimeter around the defend object and attack any enemies that come within range.")]
    [NodeIcon("298eaee25c3621a4bb49e8b7a6e53a56", "30506c25774164748a3c84c38783fe1a")]
    public class Defend : TacticalBase
    {
        [Tooltip("The object that should be defended.")]
        [SerializeField] protected SharedVariable<GameObject> m_DefendObject;
        [Tooltip("The radius of the circle formation.")]
        [SerializeField] protected SharedVariable<float> m_Radius = 3f;
        [Tooltip("The radius around the defend object to defend.")]
        [SerializeField] protected SharedVariable<float> m_DefendRadius = 10;
        [Tooltip("The maximum distance that the agents can defend from the defend object.")]
        [SerializeField] protected SharedVariable<float> m_MaxDistance = 15;

        protected override bool StopWithinRange => true;
        protected override bool ContinuousTargetSearch => true;
        public override Vector3 TargetPosition
        {
            get
            {
                if (m_ActiveTarget != null) return m_ActiveTarget.transform.position;
                if (m_DefendObject.Value != null) return m_DefendObject.Value.transform.position;
                return m_Transform.position;
            }
        }

        private GameObject m_ActiveTarget;

        /// <summary>
        /// Starts the task.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();

            m_ActiveTarget = null;
        }

        /// <summary>
        /// Updates the task.
        /// </summary>
        /// <returns>Success if the agent doesn't have any more targets to attack, otherwise Running if moving to position.</returns>
        public override TaskStatus OnUpdate()
        {
            var taskStatus = base.OnUpdate();
            if (taskStatus == TaskStatus.Failure) {
                return taskStatus;
            }
            if (m_ActiveTarget == null) {
                var targetDirection = (m_AttackTarget.position - m_DefendObject.Value.transform.position);
                if (targetDirection.magnitude < m_DefendRadius.Value) {
                    m_ActiveTarget = m_AttackTarget.gameObject;
                }
            } else {
                var distanceFromCenter = (m_ActiveTarget.transform.position - m_DefendObject.Value.transform.position).magnitude;
                if (distanceFromCenter > m_MaxDistance.Value || !m_AttackDamageable.IsAlive) {
                    m_ActiveTarget = null;
                }
            }

            if (m_ActiveTarget == null && m_Pathfinder.HasArrived() && AttackStatus == CanAttackStatus.OutOfRange) {
                m_AttackAgent.RotateTowards((m_Transform.position - m_DefendObject.Value.transform.position).normalized);
            }
            return taskStatus;
        }

        /// <summary>
        /// Calculate the position for this agent in the formation.
        /// </summary>
        /// <param name="index">The index of this agent in the formation.</param>
        /// <param name="totalAgents">The total number of agents in the formation.</param>
        /// <param name="center">The center position of the formation.</param>
        /// <param name="forward">The forward direction of the formation.</param>
        /// <param name="samplePosition">Should the position be sampled?</param>
        /// <returns>The position for this agent.</returns>
        public override Vector3 CalculateFormationPosition(int index, int totalAgents, Vector3 center, Vector3 forward, bool samplePosition)
        {
            if (m_ActiveTarget != null) {
                return m_ActiveTarget.transform.position;
            }

            if (m_DefendObject.Value == null) {
                return m_Transform.position;
            }

            var angle = (360f / totalAgents) * index;
            var angleRad = angle * Mathf.Deg2Rad;

            // Calculate the position in local space.
            var defendPosition = m_DefendObject.Value.transform.position;
            Vector3 localPosition;
            if (m_Is2D) {
                // Use the XY plane for 2D.
                localPosition = new Vector3(Mathf.Sin(angleRad) * m_Radius.Value, Mathf.Cos(angleRad) * m_Radius.Value, 0);
            } else {
                // Use the XZ plane for 3D.
                localPosition = new Vector3(Mathf.Sin(angleRad) * m_Radius.Value, defendPosition.y, Mathf.Cos(angleRad) * m_Radius.Value);
            }

            // Calculate the agent's position.
            var position = defendPosition + localPosition;
            var validPos = position;

            if (samplePosition && SamplePosition(ref validPos)) {
                position = validPos;
            }

            return position;
        }

        /// <summary>
        /// Resets the task values.
        /// </summary>
        public override void Reset()
        {
            base.Reset();

            m_DefendObject = null;
            m_Radius = 3;
            m_DefendRadius = 10;
            m_MaxDistance = 15;
        }

        /// <summary>
        /// Draws gizmos to visualize the radius of the defending area.
        /// </summary>
        protected override void OnDrawGizmos()
        {
#if UNITY_EDITOR
            if (m_DefendObject.Value == null) {
                return;
            }

            var originalColor = Gizmos.color;
            Gizmos.color = Editor.BehaviorDesignerSettings.Instance.DefaultGizmosColor;
            Gizmos.DrawWireSphere(m_DefendObject.Value.transform.position, m_DefendRadius.Value);
            if (m_MaxDistance.Value != float.MaxValue) {
                var color = Gizmos.color;
                color.a = 0.1f;
                Gizmos.color = color;
                Gizmos.DrawWireSphere(m_DefendObject.Value.transform.position, m_MaxDistance.Value);
            }
            Gizmos.color = originalColor;
#endif
        }
    }
} 