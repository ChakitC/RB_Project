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

    [Opsive.Shared.Utility.Description("Retreats from the target.")]
    [NodeIcon("bfa3ca2dfd6effd43b207b024c50a914", "68cc3dfbfff79a84caae161bcf169886")]
    public class Retreat : TacticalBase
    {
        [Tooltip("The distance that the agents should retreat.")]
        [SerializeField] protected SharedVariable<float> m_Distance = 20f;

        private Vector3? m_CacheOffset;

        public override bool AssignOptimialIndicies => false;
        protected override bool StopWithinRange => false;
        public override Vector3 TargetPosition => m_AttackTarget != null ? m_AttackTarget.position : m_Transform.position;

        /// <summary>
        /// Updates the task.
        /// </summary>
        /// <returns>Success when the agents have retreated.</returns>
        public override TaskStatus OnUpdate()
        {
            var status = base.OnUpdate();
            // Most tactical tasks end when the targets have been destroyed. The retreat task should end when the agents are far away.
            if (status == TaskStatus.Running && m_Group.State == Shared.Runtime.FormationsManager.FormationState.Arrived) {
                status = TaskStatus.Success;
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
            var retreatPosition = center - forward.normalized * m_Distance.Value;
            if (m_CacheOffset.HasValue) {
                return retreatPosition + m_CacheOffset.Value;
            }

            // Calculate and cache the offset from the center
            var offset = m_Transform.position - center;
            var position = retreatPosition + offset;
            var validPos = position;
            if (samplePosition && SamplePosition(ref validPos)) {
                position = validPos;
            }
            m_CacheOffset = position - retreatPosition;

            return position;
        }

        /// <summary>
        /// The task has ended.
        /// </summary>
        public override void OnEnd()
        {
            base.OnEnd();
            m_CacheOffset = null;
        }

        /// <summary>
        /// Data structure for saving the retreat formation state.
        /// </summary>
        private class RetreatSaveData
        {
            [Tooltip("The base formation save data containing common formation state.")]
            public FormationSaveData BaseData;
            [Tooltip("Indicates if the agent has a valid cache offset.")]
            public bool HasCacheOffset;
            [Tooltip("The cached offset from the formation center for this agent's position.")]
            public Vector3 CacheOffset;
        }

        /// <summary>
        /// Returns the current task state.
        /// </summary>
        /// <param name="world">The DOTS world.</param>
        /// <param name="entity">The DOTS entity.</param>
        /// <returns>The current task state.</returns>
        public override object Save(World world, Entity entity)
        {
            var saveData = base.Save(world, entity);
            if (saveData == null) {
                return null;
            }

            var retreatSaveData = new RetreatSaveData();
            retreatSaveData.BaseData = (FormationSaveData)saveData;
            if (m_CacheOffset.HasValue) {
                retreatSaveData.HasCacheOffset = true;
                retreatSaveData.CacheOffset = m_CacheOffset.Value;
            }
            return retreatSaveData;
        }

        /// <summary>
        /// Loads the previous task state.
        /// </summary>
        /// <param name="saveData">The previous task state.</param>
        /// <param name="world">The DOTS world.</param>
        /// <param name="entity">The DOTS entity.</param>
        public override void Load(object saveData, World world, Entity entity)
        {
            if (saveData == null) {
                return;
            }

            var retreatSaveData = (RetreatSaveData)saveData;
            base.Load(retreatSaveData.BaseData, world, entity);
            if (retreatSaveData.HasCacheOffset) {
                m_CacheOffset = retreatSaveData.CacheOffset;
            }
        }

        /// <summary>
        /// Resets the task values.
        /// </summary>
        public override void Reset()
        {
            base.Reset();

            m_Distance = 20;
        }
    }
}
