/// ---------------------------------------------
/// Tactical Pack for Behavior Designer Pro
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.AddOns.TacticalPack.Runtime.Tasks
{
    using Opsive.BehaviorDesigner.AddOns.Shared.Runtime;
    using Opsive.BehaviorDesigner.Runtime.Tasks;
    using Opsive.GraphDesigner.Runtime;
    using Opsive.GraphDesigner.Runtime.Variables;
    using Unity.Entities;
    using UnityEngine;

    [Opsive.Shared.Utility.Description("Advances toward the target using a leapfrog maneuver. The agents will split into two groups that take turns moving forward.")]
    [NodeIcon("09277a047a1379d40b17c672c4744677", "dc2a6069026e1aa47ad63c8d496fae49")]
    public class Leapfrog : TacticalBase
    {
        [Tooltip("The spacing between agents in the row (x) and between rows (y).")]
        [SerializeField] protected SharedVariable<Vector2> m_Spacing = new Vector2(2f, 2f);
        [Tooltip("The maximum number of agents allowed in a single row.")]
        [SerializeField] protected SharedVariable<int> m_MaxAgentsPerRow = 3;
        [Tooltip("The distance between the two groups when leapfrogging.")]
        [SerializeField] protected SharedVariable<float> m_GroupSpacing = 10f;
        [Tooltip("The distance each group should move before stopping.")]
        [SerializeField] protected SharedVariable<float> m_LeapDistance = 20f;

        protected override bool StopWithinRange => true;
        public override bool CanMoveIntoInitialFormation => true;

        private bool m_IsFirstGroup;
        private bool m_Attack;
        private Vector3 m_StartPosition;
        private bool m_IsFirstGroupMoving;
        private float m_LeapDistanceMultiplier = 1;

        /// <summary>
        /// Starts the task.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();

            m_Attack = false;
            m_IsFirstGroupMoving = true;
            m_LeapDistanceMultiplier = 1;
        }

        /// <summary>
        /// Updates the task.
        /// </summary>
        /// <returns>Success if the agent doesn't have any more targets to attack, otherwise Running if moving to position.</returns>
        public override TaskStatus OnUpdate()
        {
            // Only the leader manages the leapfrogging movement
            if (m_Group.Leader == this) {
                var attack = CanAnyAgentsAttack();
                if (attack || m_Group.State == FormationsManager.FormationState.Arrived) {
                    m_IsFirstGroupMoving = !m_IsFirstGroupMoving;
                    m_StartPosition = m_Transform.position;
                    m_Group.State = FormationsManager.FormationState.MoveToTarget;
                    m_LeapDistanceMultiplier = 2; // After the first group has moved the next group needs to leap the first group.

                    for (int i = 0; i < m_Group.Members.Count; ++i) {
                        (m_Group.Members[i] as Leapfrog).UpdateGroupMovement(attack, m_IsFirstGroupMoving, m_StartPosition, m_LeapDistanceMultiplier);
                    }
                }
            }

            return base.OnUpdate();
        }

        /// <summary>
        /// Check if any agents are ready to attack.
        /// </summary>
        /// <returns>True if any agents are able to attack.</returns>
        protected bool CanAnyAgentsAttack()
        {
            for (int i = 0; i < m_Group.Members.Count; ++i) {
                var attackStatus = (m_Group.Members[i] as TacticalBase).AttackStatus;
                if (attackStatus != CanAttackStatus.OutOfRange) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Updates the group movement state.
        /// </summary>
        /// <param name="attack">Should the agents attack?</param>
        /// <param name="isFirstGroupMoving">Is the first group moving?</param>
        /// <param name="startPosition">The position where the movement started.</param>
        /// <param name="leapDistanceMultiplier">The multiplier to apply to the leap distance.</param>
        public void UpdateGroupMovement(bool attack, bool isFirstGroupMoving, Vector3 startPosition, float leapDistanceMultiplier)
        {
            m_Attack = attack;
            m_IsFirstGroupMoving = isFirstGroupMoving;
            m_StartPosition = startPosition;
            m_LeapDistanceMultiplier = leapDistanceMultiplier;
            m_Pathfinder.SetDesination(CalculateFormationPosition(m_FormationIndex, m_Group.Members.Count, TargetPosition, m_Group.Direction, true));
        }

        public override void UpdateFormationIndex(int index, Vector3? desiredPosition = null)
        {
            m_IsFirstGroup = index < Mathf.CeilToInt(m_Group.Members.Count / 2f);

            base.UpdateFormationIndex(index, desiredPosition);
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
            // Move directly to the target if any agents can attack.
            if (m_Attack) {
                return center;
            }

            // Stay in the current position if this group cannot attack.
            if (m_Group.State == FormationsManager.FormationState.MoveToTarget && m_IsFirstGroup != m_IsFirstGroupMoving) {
                return m_Transform.position;
            }

            // Calculate which group this agent belongs to.
            var firstGroupCount = Mathf.CeilToInt(totalAgents / 2f);
            var isFirstGroup = index < firstGroupCount;
            
            // Adjust index for the current group.
            var groupIndex = isFirstGroup ? index : index - firstGroupCount;
            var groupTotal = isFirstGroup ? firstGroupCount : totalAgents - firstGroupCount;

            var row = groupIndex / m_MaxAgentsPerRow.Value;
            var column = groupIndex % m_MaxAgentsPerRow.Value;

            // Calculate the offset from center based on row and column.
            float horizontalOffset;
            if (groupIndex == 0 || m_Group.State == FormationsManager.FormationState.MoveToTarget) {
                // The leader doesn't have an offset, nor does if the agents are moving to the target.
                // If moving to the target the transform position is used and that already has an offset relative to the center.
                horizontalOffset = 0;
            } else {
                // Even indices go left, odd indices go right.
                var sideOffset = Mathf.CeilToInt(column / 2f);
                horizontalOffset = (column % 2 == 1) ? sideOffset * m_Spacing.Value.x : -sideOffset * m_Spacing.Value.x;
            }

            // Separate the groups horizontally.
            if (!isFirstGroup && m_Group.State != FormationsManager.FormationState.MoveToTarget) {
                horizontalOffset += m_GroupSpacing.Value;
            }

            // Center the rows vertically by offsetting from the center.
            var totalRows = Mathf.CeilToInt((float)groupTotal / m_MaxAgentsPerRow.Value);
            var verticalOffset = (row - (totalRows - 1) / 2f) * m_Spacing.Value.y + (m_Group.State == FormationsManager.FormationState.MoveToTarget ? (m_LeapDistance.Value * m_LeapDistanceMultiplier) : 0);

            // The position and rotation depends on the perspective.
            Vector3 localPosition;
            Quaternion rotation;
            if (m_Is2D) {
                // Use the XY plane for 2D.
                localPosition = new Vector3(horizontalOffset, verticalOffset, 0);
                var forward2D = new Vector2(forward.x, forward.y).normalized;
                rotation = Quaternion.Euler(0, 0, Mathf.Atan2(forward2D.y, forward2D.x) * Mathf.Rad2Deg);
            } else {
                // Use the XZ plane for 3D.
                localPosition = new Vector3(horizontalOffset, 0, verticalOffset);
                rotation = Quaternion.LookRotation(forward, m_Transform.up);
            }

            // Calculate the agent's position. If the agent is moving to the target then the current position is used so the agent doesn't go the entire way to the target.
            var position = (m_Group.State == FormationsManager.FormationState.MoveToTarget ? m_Transform.position : center) + rotation * localPosition;
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
            var saveData = new object[6];
            saveData[0] = base.Save(world, entity);
            saveData[1] = m_IsFirstGroup;
            saveData[2] = m_Attack;
            saveData[3] = m_StartPosition;
            saveData[4] = m_IsFirstGroupMoving;
            saveData[5] = m_LeapDistanceMultiplier;
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
            m_IsFirstGroup = (bool)data[1];
            m_Attack = (bool)data[2];
            m_StartPosition = (Vector3)data[3];
            m_IsFirstGroupMoving = (bool)data[4];
            m_LeapDistanceMultiplier = (float)data[5];
        }

        /// <summary>
        /// Resets the task values.
        /// </summary>
        public override void Reset()
        {
            base.Reset();

            m_Spacing = new Vector2(2, 2);
            m_MaxAgentsPerRow = 3;
            m_GroupSpacing = 10;
            m_LeapDistance = 20;
        }
    }
} 