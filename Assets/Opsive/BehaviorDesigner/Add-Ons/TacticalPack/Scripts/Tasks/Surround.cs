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
    using Unity.Entities;
    using UnityEngine;

    [Opsive.Shared.Utility.Description("Surrounds the target in a circular formation. The agents will form a circle around the target and attack from all sides.")]
    [DefaultAttackDelay(AttackDelay.GroupArrival)]
    [NodeIcon("a1876680c488cab4796605f54185429b", "d88d3def22e207047bc5375cc93a70ac")]
    public class Surround : TacticalBase
    {
        [Tooltip("The radius of the circle formation.")]
        [SerializeField] protected SharedVariable<float> m_Radius = 5f;

        protected override bool StopWithinRange => false;

        private bool m_CanAvoidCenter;
        private bool m_AvoidCenter;

        /// <summary>
        /// The task has started.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();

            m_CanAvoidCenter = true;
        }

        /// <summary>
        /// Updates the task.
        /// </summary>
        /// <returns>Success if the agent doesn't have any more targets to attack, otherwise Running if moving to position.</returns>
        public override TaskStatus OnUpdate()
        {
            var status = base.OnUpdate();
            // The position should be updated as soon as the agent is near the destination if they went around the center.
            if (status == TaskStatus.Running && m_AvoidCenter) {
                if (m_Pathfinder.RemainingDistance < (m_Radius.Value * 0.1f)) {
                    m_Pathfinder.SetDesination(CalculateFormationPosition(m_FormationIndex, m_Group.Members.Count, m_AttackTarget.position, m_Group.Direction, true, m_CanAvoidCenter));
                    m_CanAvoidCenter = false;
                }
            }
            return status;
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
            return CalculateFormationPosition(index, totalAgents, center, forward, samplePosition, m_CanAvoidCenter);
        }

        /// <summary>
        /// Calculate the position for this agent in the formation.
        /// </summary>
        /// <param name="index">The index of this agent in the formation.</param>
        /// <param name="totalAgents">The total number of agents in the formation.</param>
        /// <param name="center">The center position of the formation.</param>
        /// <param name="forward">The forward direction of the formation.</param>
        /// <param name="samplePosition">Should the position be sampled?</param>
        /// <param name="avoidCenter">Should the agent avoid the center?</param>
        /// <returns>The position for this agent.</returns>
        private Vector3 CalculateFormationPosition(int index, int totalAgents, Vector3 center, Vector3 forward, bool samplePosition, bool avoidCenter)
        {
            var angle = ((360f / totalAgents) * index + 180) % 360;
            // Don't go through the center when travelling to the other side of the circle.
            m_AvoidCenter = avoidCenter && (angle < 90 || angle > 270);
            if (m_AvoidCenter) {
                angle = angle < 90 ? 90 : 270;
            }
            var angleRad = angle * Mathf.Deg2Rad;

            // Calculate the position in local space.
            Vector3 localPosition;
            if (m_Is2D) {
                // Use the XY plane for 2D.
                localPosition = new Vector3(Mathf.Sin(angleRad) * m_Radius.Value, Mathf.Cos(angleRad) * m_Radius.Value, 0);
            } else {
                // Use the XZ plane for 3D.
                localPosition = new Vector3(Mathf.Sin(angleRad) * m_Radius.Value, center.y, Mathf.Cos(angleRad) * m_Radius.Value);
            }

            // Calculate the agent's position.
            var position = center + localPosition;
            var validPos = position;

            if (samplePosition && SamplePosition(ref validPos)) {
                position = validPos;
            }

            return position;
        }

        /// <summary>
        /// Returns the current task state.
        /// </summary>
        /// <param name="world">The DOTS world.</param>
        /// <param name="entity">The DOTS entity.</param>
        /// <returns>The current task state.</returns>
        public override object Save(World world, Entity entity)
        {
            var saveData = new object[2];
            saveData[0] = base.Save(world, entity);
            saveData[1] = m_AvoidCenter;
            return saveData;
        }

        /// <summary>
        /// Loads the previous task state.
        /// </summary>
        /// <param name="saveData">The previous task state.</param>
        /// <param name="world">The DOTS world.</param>
        /// <param name="entity">The DOTS entity.</param>
        public override void Load(object saveData, World world, Entity entity)
        {
            var data = saveData as object[];
            base.Load(data[0], world, entity);
            m_AvoidCenter = (bool)data[1];
        }

        /// <summary>
        /// Resets the task values.
        /// </summary>
        public override void Reset()
        {
            base.Reset();

            m_Radius = 5;
        }
    }
} 