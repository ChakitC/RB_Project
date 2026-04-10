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

    [Opsive.Shared.Utility.Description("Flanks the target from multiple angles. The agents will split into groups to attack from the left, right, and front sides of the target.")]
    [DefaultAttackDelay(AttackDelay.GroupArrival)]
    [NodeIcon("d4795ed1d166313438fcdb5ab5266985", "963074dc5fe63d546848bf7401376bc6")]
    public class Flank : TacticalBase
    {
        [Tooltip("The distance from the target when flanking.")]
        [SerializeField] protected SharedVariable<float> m_FlankDistance = 8f;
        [Tooltip("The percentage of agents that should flank from the left (0-1).")]
        [SerializeField] protected SharedVariable<float> m_LeftFlankPercentage = 0.25f;
        [Tooltip("The percentage of agents that should flank from the right (0-1).")]
        [SerializeField] protected SharedVariable<float> m_RightFlankPercentage = 0.25f;
        [Tooltip("The percentage of agents that should flank from the front (0-1).")]
        [SerializeField] protected SharedVariable<float> m_FrontFlankPercentage = 0.5f;

        protected override bool StopWithinRange => !m_Flank;

        private bool m_Flank;

        /// <summary>
        /// Initializes the default values.
        /// </summary>
        public override void OnAwake()
        {
            base.OnAwake();

            var percentage = m_LeftFlankPercentage.Value + m_RightFlankPercentage.Value + m_FrontFlankPercentage.Value;
            if (!Mathf.Approximately(percentage, 1)) {
                Debug.LogWarning("Warning: The flank percentages should add up to 1.");
            }
        }

        /// <summary>
        /// Starts the task.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();

            m_Flank = false;

            if (m_FlankDistance.Value > m_AttackAgent.MaxAttackDistance) {
                Debug.LogWarning("The FlankDistance should be less than the IAttackAgent.MaxAttackDistance.", gameObject);
            }
        }

        /// <summary>
        /// Updates the flank position.
        /// </summary>
        /// <returns>Success if the agent doesn't have any more targets to attack, otherwise Running if moving to position.</returns>
        public override TaskStatus OnUpdate()
        {
            var status = base.OnUpdate();
            // The position should be updated as soon as the agent is near the destination if they went around the center.
            if (status == TaskStatus.Running && m_Flank) {
                if (m_Pathfinder.RemainingDistance < (m_FlankDistance.Value * 0.1f)) {
                    m_Pathfinder.SetDesination(CalculateFormationPosition(m_FormationIndex, m_Group.Members.Count, m_Group.TargetPosition, m_Group.Direction, true, false));
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
            return CalculateFormationPosition(index, totalAgents, center, forward, samplePosition, true);
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
        private Vector3 CalculateFormationPosition(int index, int totalAgents, Vector3 center, Vector3 forward, bool samplePosition, bool wideFlank)
        {
            // Calculate how many agents should be in each position
            var leftAgents = Mathf.RoundToInt(totalAgents * (m_LeftFlankPercentage.Value));
            var rightAgents = Mathf.RoundToInt(totalAgents * (m_RightFlankPercentage.Value));
            var frontAgents = totalAgents - leftAgents - rightAgents; // Remaining agents go to front

            // Determine which position this agent should take.
            float angle;
            if (index < leftAgents) { // Left.
                angle = 90f;
                m_Flank = wideFlank;
            } else if (index < leftAgents + rightAgents) { // Right.
                angle = -90f;
                m_Flank = wideFlank;
            } else { // Front.
                angle = 180f;
            }

            // Calculate the position in local space
            var angleRad = angle * Mathf.Deg2Rad;
            Vector3 localPosition;
            var flankDistance = m_Flank ? m_FlankDistance.Value : 0;
            if (m_Is2D) {
                // Use the XY plane for 2D.
                localPosition = new Vector3(Mathf.Sin(angleRad) * flankDistance, Mathf.Cos(angleRad) * flankDistance, 0);
            } else {
                // Use the XZ plane for 3D.
                localPosition = new Vector3(Mathf.Sin(angleRad) * flankDistance, center.y, Mathf.Cos(angleRad) * flankDistance);
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
            saveData[1] = m_Flank;
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
            m_Flank = (bool)data[1];
        }

        /// <summary>
        /// Resets the task values.
        /// </summary>
        public override void Reset()
        {
            base.Reset();

            m_FlankDistance = 8;
            m_LeftFlankPercentage = 0.25f;
            m_RightFlankPercentage = 0.25f;
            m_FrontFlankPercentage = 0.5f;
        }
    }
} 