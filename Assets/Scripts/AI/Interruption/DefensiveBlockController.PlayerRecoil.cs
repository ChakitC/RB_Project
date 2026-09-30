using UnityEngine;
using UnityEngine.AI;

public sealed partial class DefensiveBlockController
{
    CharacterMovementMotor playerRecoilMotor;
    StateHub playerRecoilHub;
    PlayerMovementCC playerRecoilMovement;
    int playerRecoilControlToken, playerRecoilMovementToken;

    void BeginPlayerRecoil()
    {
        EndPlayerRecoil();
        // Self/cooperative guards already move the Player through their own recoil.
        if (ctx == player || cooperativeSession != null || player == null || slideDistance <= 0f || slideSeconds <= 0f)
            return;
        player.EnsureMovementMotor();
        playerRecoilMotor = player.MovementMotor;
        if (playerRecoilMotor == null) return;
        playerRecoilHub = player.stateHub;
        if (playerRecoilHub != null)
            playerRecoilControlToken = playerRecoilHub.AcquireExternalControlBlockToken(ControlBlockFlags.Move | ControlBlockFlags.Rotate, nameof(DefensiveBlockController));
        playerRecoilMovement = player.movement;
        if (playerRecoilMovement != null)
            playerRecoilMovementToken = playerRecoilMovement.AcquireSuspensionToken();
    }

    void MovePlayerRecoil(Vector3 displacement)
    {
        if (playerRecoilMotor == null) return;
        if (player == null || !player.isActiveAndEnabled || player.LifeGeneration != playerLife ||
            player.HealthSystem == null || !player.HealthSystem.IsAlive ||
            (player.KnockbackMotor != null && player.KnockbackMotor.IsActive) ||
            (player.DefensiveBlock != null && player.DefensiveBlock.IsExecuting))
        { EndPlayerRecoil(); return; }
        // Follow only actual planar Ally motion: a blocked Ally cannot push the Player on alone.
        displacement.y = 0f;
        MovePlayerAlongNavMesh(playerRecoilMotor, displacement);
    }

    internal static Vector3 MovePlayerAlongNavMesh(CharacterMovementMotor motor, Vector3 displacement)
    {
        Vector3 origin = motor.ActorRoot.position;
        if (displacement.sqrMagnitude <= .0000001f ||
            !NavMesh.SamplePosition(origin, out var start, .2f, NavMesh.AllAreas) ||
            Mathf.Abs(start.position.y - origin.y) >= .2f) return Vector3.zero;
        Vector3 desired = origin + displacement;
        if (NavMesh.Raycast(start.position, desired, out var edge, NavMesh.AllAreas)) desired = edge.position;
        if (!NavMesh.SamplePosition(desired, out var sample, .2f, NavMesh.AllAreas) ||
            Mathf.Abs(sample.position.y - origin.y) >= .2f) return Vector3.zero;
        return motor.Move(sample.position - origin, CharacterMovementSettings.Default);
    }

    void EndPlayerRecoil()
    {
        if (playerRecoilControlToken != 0 && playerRecoilHub != null)
            playerRecoilHub.ReleaseExternalControlBlockToken(playerRecoilControlToken);
        if (playerRecoilMovementToken != 0 && playerRecoilMovement != null)
            playerRecoilMovement.ReleaseSuspensionToken(playerRecoilMovementToken);
        playerRecoilControlToken = playerRecoilMovementToken = 0;
        playerRecoilMotor = null;
        playerRecoilHub = null;
        playerRecoilMovement = null;
    }
}
