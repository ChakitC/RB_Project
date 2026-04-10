/// ---------------------------------------------
/// Tactical Pack for Behavior Designer Pro
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.AddOns.TacticalPack.Runtime
{
    using UnityEngine;

    /// <summary>
    /// Interface for an agent that is able to attack.
    /// </summary>
    public interface IAttackAgent
    {
        /// <summary>
        /// The closest distance that the agent is able to attack from.
        /// </summary>
        float MinAttackDistance { get; }
        /// <summary>
        /// The furthest distance that the agent is able to attack from.
        /// </summary>
        float MaxAttackDistance { get; }
        /// <summary>
        /// The maximum angle from the target to the agent in order for agent to be able to attack.
        /// </summary>
        float AttackAngleThreshold { get; }

        /// <summary>
        /// Rotates the agent towards the specified direction.
        /// </summary>
        /// <param name="direction">The direction to rate towards.</param>
        void RotateTowards(Vector3 direction);

        /// <summary>
        /// Tries to do the actual actual attack.
        /// </summary>
        /// <param name="targetTransform">The target Transform that should be attacked.</param>
        /// <param name="targetDamageable">The target Damageable that should be attacked.</param>
        void Attack(Transform targetTransform, IDamageable targetDamageable);
    }
}