/// ---------------------------------------------
/// Shared Add-On for Behavior Designer Pro
/// Copyright (c) Opsive. All Rights Reserved.
/// https://www.opsive.com
/// ---------------------------------------------
namespace Opsive.BehaviorDesigner.AddOns.Shared.Runtime.Pathfinding
{
    using UnityEngine;

    /// <summary>
    /// Abstract class for any Movement Pack pathfinding implementation.
    /// </summary>
    public abstract class Pathfinder
    {
        /// <summary>
        /// The velocity of the agent.
        /// </summary>
        public abstract Vector3 Velocity { get; }
        /// <summary>
        /// The remaining distance of the agent.
        /// </summary>
        public abstract float RemainingDistance { get; }
        /// <summary>
        /// The destination of the agent.
        /// </summary>
        public abstract Vector3 Destination { get; }
        /// <summary>
        /// The speed of the agent.
        /// </summary>
        public abstract float Speed { get; set; }

        /// <summary>
        /// Initializes the pathfinder.
        /// </summary>
        /// <param name="gameObject">The GameObject that the pathfinder belongs to.</param>
        public abstract void Initialize(GameObject gameObject);

        /// <summary>
        /// The task has started.
        /// </summary>
        public virtual void OnStart() { }

        /// <summary>
        /// Set a new pathfinding destination.
        /// </summary>
        /// <param name="destination">The destination to set.</param>
        /// <returns>True if the destination is valid.</returns>
        public abstract bool SetDesination(Vector3 destination);

        /// <summary>
        /// Does the agent have a pathfinding path?
        /// </summary>
        /// <returns>True if the agent has a pathfinding path.</returns>
        public abstract bool HasPath();

        /// <summary>
        /// Returns true if the position is a valid pathfinding position.
        /// </summary>
        /// <param name="position">The position to sample. The position will be updated to the valid sampled position.</param>
        /// <returns>True if the position is a valid pathfinding position.</returns>
        public abstract bool SamplePosition(ref Vector3 position);

        /// <summary>
        /// Has the agent arrived at the destination?
        /// </summary>
        /// <returns>True if the agent has arrived at the destination.</returns>
        public abstract bool HasArrived();

        /// <summary>
        /// The agent should stop moving.
        /// </summary>
        public abstract void Stop();

        /// <summary>
        /// The task has ended.
        /// </summary>
        public virtual void OnEnd() { }

        /// <summary>
        /// Resets the values back to their default.
        /// </summary>
        public virtual void Reset() { }
    }
}