/// <summary>
/// How a <see cref="CharacterVerticalMotor"/> owns the Y axis for its actor.
/// </summary>
public enum CharacterVerticalMode
{
    /// <summary>
    /// Gravity integrates every frame. For actors held up only by a CharacterController,
    /// such as the player.
    /// </summary>
    Always,

    /// <summary>
    /// Dormant until the actor is launched. For NavMeshAgent-driven actors, where the agent
    /// already holds the actor on the NavMesh surface and running gravity while grounded would
    /// only fight it.
    /// </summary>
    AgentDriven
}
