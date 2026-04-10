/// ---------------------------------------------
/// Tactical Pack for Behavior Designer Pro
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.AddOns.TacticalPack.Runtime.Tasks
{
    using Opsive.GraphDesigner.Runtime;
    using UnityEngine;

    [Opsive.Shared.Utility.Description("Directly attacks the target. The agents will move to the target's position and attack when in range.")]
    [DefaultAttackDelay(AttackDelay.Arrival)]
    [NodeIcon("b64095b4b8463114ebfe943ab30bc263", "3fcce0d7d8286e941bae9d79d8ffc2fc")]
    public class Attack : TacticalBase
    {
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
            return m_AttackTarget != null ? m_AttackTarget.position : center;
        }
    }
} 