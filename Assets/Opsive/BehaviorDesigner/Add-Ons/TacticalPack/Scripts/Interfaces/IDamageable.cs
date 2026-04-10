/// ---------------------------------------------
/// Tactical Pack for Behavior Designer Pro
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.AddOns.TacticalPack.Runtime
{
    /// <summary>
    /// Interface for objects that can take damage.
    /// </summary>
    public interface IDamageable
    {
        /// <summary>
        /// Is the object currently alive?
        /// </summary>
        bool IsAlive { get; }

        /// <summary>
        /// Take damage by the specified amount.
        /// </summary>
        /// <param name="amount">The amount of damage to take.</param>
        void Damage(float amount);
    }
}