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

    [Opsive.Shared.Utility.Description("Waits for the target to pass by before attacking.")]
    [NodeIcon("ff9e614c59029824c878116155a3f5a6", "e99710c44ee902a4abe294e7deb598b1")]
    public class Ambush : TacticalBase
    {
        [Tooltip("The minimum distance that the agents can attack.")]
        public SharedVariable<float> m_MinDistance = 10;
        [Tooltip("The number of seconds to wait after the enemies have passed before the agents start attacking.")]
        public SharedVariable<float> m_Delay;

        private float m_AmbushTime;
        private bool m_CanAmbush;

        /// <summary>
        /// Starts the task.
        /// </summary>
        public override void OnStart()
        {
            base.OnStart();

            m_AmbushTime = -1;
            m_CanAmbush = false;
        }

        /// <summary>
        /// Updates the task.
        /// </summary>
        /// <returns>Success if the agent doesn't have any more targets to attack, otherwise Running if moving to position.</returns>
        public override TaskStatus OnUpdate()
        {
            // The task should perform its normal update when the agents are ambushing the target.
            // Do not update the base task when the tasks are waiting to prevent any destinations from being set.
            if (m_CanAmbush || m_Group.State != Shared.Runtime.FormationsManager.FormationState.MoveToTarget) {
                var status = base.OnUpdate();
                if (status == TaskStatus.Failure || status == TaskStatus.Success) {
                    return status;
                }
            } else if (m_Group.Leader == this) {
                // The ambush is based off of the leader. As soon as the leader says to ambush the agents should attack.
                if (Vector3.Distance(TargetPosition, m_Transform.position) > m_MinDistance.Value) {
                    if (m_AmbushTime == -1) {
                        m_AmbushTime = Time.time;
                    }

                    if (m_AmbushTime + m_Delay.Value <= Time.time) {
                        for (int i = 0; i < m_Group.Members.Count; ++i) {
                            if (m_Group.Members[i] is Ambush ambush) {
                                ambush.StartAmbush();
                            }
                        }
                    }
                }
            }
            return TaskStatus.Running;
        }

        /// <summary>
        /// Starts the ambush.
        /// </summary>
        public void StartAmbush()
        {
            m_CanAmbush = true;
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
            if (m_AttackTarget == null) {
                return m_Transform.position;
            }
            return m_CanAmbush ? m_AttackTarget.position : m_Transform.position;
        }

        /// <summary>
        /// Returns the current task state.
        /// </summary>
        /// <param name="world">The DOTS world.</param>
        /// <param name="entity">The DOTS entity.</param>
        /// <returns>The current task state.</returns>
        public override object Save(World world, Entity entity)
        {
            var saveData = new object[3];
            saveData[0] = base.Save(world, entity);
            saveData[1] = m_AmbushTime == -1 ? -1 : Time.time - m_AmbushTime;
            saveData[2] = m_CanAmbush;
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
            var elapsedTime = (float)data[1];
            m_AmbushTime = elapsedTime == -1 ? -1 : Time.time - elapsedTime;
            m_CanAmbush = (bool)data[2];
        }

        /// <summary>
        /// Resets the task values.
        /// </summary>
        public override void Reset()
        {
            base.Reset();

            m_MinDistance = 10;
            m_Delay = 0;
        }
    }
}
