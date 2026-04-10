/// ---------------------------------------------
/// Tactical Pack for Behavior Designer Pro
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.AddOns.TacticalPack.Runtime.Tasks
{
    using Opsive.BehaviorDesigner.AddOns.Shared.Runtime;
    using Opsive.BehaviorDesigner.Runtime.Tasks;
    using Opsive.BehaviorDesigner.Runtime.Utility;
    using Opsive.GraphDesigner.Runtime;
    using Opsive.GraphDesigner.Runtime.Variables;
    using Unity.Entities;
    using UnityEngine;

    [Opsive.Shared.Utility.Description("Executes a shoot and scoot maneuver. The agents will move to a position, attack the target for a set amount of time, and then move to a new position.")]
    [DefaultAttackDelay(AttackDelay.GroupArrival)]
    [NodeIcon("06505abeb772877448ae2667a153ef9d", "9ebab0f5760b6e84ea0b65e8b70a10b4")]
    public class ShootAndScoot : TacticalBase
    {
        [Tooltip("The radius from the target to position the row.")]
        [SerializeField] protected SharedVariable<float> m_Radius = 5f;
        [Tooltip("The spacing between agents in the row (x) and between rows (y).")]
        [SerializeField] protected SharedVariable<Vector2> m_Spacing = new Vector2(2f, 2f);
        [Tooltip("The maximum number of agents allowed in a single row.")]
        [SerializeField] protected SharedVariable<int> m_MaxAgentsPerRow = 3;
        [Tooltip("The minimum and maximum time to stay in position before moving.")]
        [SerializeField] protected SharedVariable<RangeFloat> m_TimeInPosition = new RangeFloat(3f, 5f);
        [Tooltip("The minimum and maximum angle offset when moving to a new position.")]
        [SerializeField] protected SharedVariable<RangeFloat> m_AngleOffset = new RangeFloat(25, 50);

        public override Vector3 TargetPosition
        {
            get {
                var centerPosition = Vector3.zero;
                var validTargets = 0;
                for (int i = 0; i < m_Targets.Value.Length; ++i) {
                    if (!m_TargetDamageables[i].IsAlive) {
                        continue;
                    }
                    centerPosition += m_Targets.Value[i].transform.position;
                    validTargets++;
                }
                if (validTargets > 0) {
                    return centerPosition / validTargets;
                }
                return base.TargetPosition;
            }
        }

        protected override bool StopWithinRange => false;

        private float m_CurrentAngle;
        private float m_NextPositionTime;
        private bool m_IsMoving;

        /// <summary>
        /// Starts the task.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();

            m_CurrentAngle = 180f;
            m_IsMoving = true;
        }

        /// <summary>
        /// Updates the task.
        /// </summary>
        /// <returns>Success if the agent doesn't have any more targets to attack, otherwise Running if moving to position.</returns>
        public override TaskStatus OnUpdate()
        {
            if (m_Group.Leader == this && m_Group.State == FormationsManager.FormationState.Arrived) {
                if (m_IsMoving) {
                    m_NextPositionTime = Time.time + m_TimeInPosition.Value.RandomValue;
                    m_IsMoving = false;
                }

                if (Time.time >= m_NextPositionTime) {
                    m_CurrentAngle = (m_CurrentAngle - m_AngleOffset.Value.RandomValue) % 360f;
                    m_IsMoving = true;

                    m_Group.State = FormationsManager.FormationState.MoveToTarget;
                    for (int i = 0; i < m_Group.Members.Count; ++i) {
                        (m_Group.Members[i] as ShootAndScoot).Move(m_CurrentAngle, m_NextPositionTime);
                    }
                }
            }

            return base.OnUpdate();
        }

        /// <summary>
        /// Moves to a new position.
        /// </summary>
        /// <param name="angle">The current angle.</param>
        /// <param name="nextPositionTime">The next time to move.</param>
        public void Move(float angle, float nextPositionTime)
        {
            m_CurrentAngle = angle;
            m_NextPositionTime = nextPositionTime;
            m_Pathfinder.SetDesination(CalculateFormationPosition(m_FormationIndex, m_Group.Members.Count, TargetPosition, m_Group.Direction, true));
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
            var row = index / m_MaxAgentsPerRow.Value;
            var column = index % m_MaxAgentsPerRow.Value;

            // Calculate the offset from center based on row and column
            float horizontalOffset;
            if (index == 0) { // The leader agent should not have any offset.
                horizontalOffset = 0;
            } else {
                // Even indices go left, odd indices go right.
                var sideOffset = Mathf.CeilToInt(column / 2f);
                horizontalOffset = (column % 2 == 1) ? sideOffset * m_Spacing.Value.x : -sideOffset * m_Spacing.Value.x;
            }

            // Center the rows vertically by offsetting from the center.
            var totalRows = Mathf.CeilToInt((float)totalAgents / m_MaxAgentsPerRow.Value);
            var verticalOffset = (row - (totalRows - 1) / 2f) * m_Spacing.Value.y + m_Radius.Value;

            // The position and rotation depends on the perspective.
            Vector3 localPosition;
            Quaternion rotation;
            if (m_Is2D) {
                // Use the XY plane for 2D.
                localPosition = new Vector3(horizontalOffset, verticalOffset, 0);
                rotation = Quaternion.Euler(0, 0, m_CurrentAngle);
            } else {
                // Use the XZ plane for 3D.
                localPosition = new Vector3(horizontalOffset, 0, verticalOffset);
                rotation = Quaternion.AngleAxis(m_CurrentAngle, m_Transform.up);
            }


            // Calculate the agent's position.
            var position = center + rotation * localPosition;
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
            var saveData = new object[4];
            saveData[0] = base.Save(world, entity);
            saveData[1] = m_CurrentAngle;
            saveData[2] = m_NextPositionTime;
            saveData[3] = m_IsMoving;
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
            m_CurrentAngle = (float)data[1];
            m_NextPositionTime = (float)data[2];
            m_IsMoving = (bool)data[3];
        }

        /// <summary>
        /// Resets the task values.
        /// </summary>
        public override void Reset()
        {
            base.Reset();

            m_Radius = 5;
            m_Spacing = new Vector2(2, 2);
            m_MaxAgentsPerRow = 3;
            m_TimeInPosition = new RangeFloat(3, 5);
            m_AngleOffset = new RangeFloat(25, 50);
        }
    }
} 