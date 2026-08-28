using UnityEngine;

/// <summary>
/// Decides whether the initiating character is calm enough to be pulled into a conversation.
/// Dialogue may only start from a safe/idle state: never mid-dash, mid-shot, mid-reload, mid-skill,
/// while stunned or downed, while a menu is open, or while another cinematic owns the stage.
///
/// Everything is read from the character's existing <see cref="StateHub"/> and modules — no new
/// gameplay flag is introduced for dialogue.
/// </summary>
public static class DialogueSafeStateGate
{
    public static bool CanStart(CharacteContext ctx, out string reason)
    {
        if (ctx == null)
        {
            reason = "No initiating character context.";
            return false;
        }

        if (CutsceneDirector.IsCinematicPlaying)
        {
            reason = "A cinematic is already playing.";
            return false;
        }

        if (DialogueDirector.HasInstance && DialogueDirector.Instance.IsPlaying)
        {
            reason = "A dialogue is already playing.";
            return false;
        }

        ctx.ResolveReferences();

        StateHub stateHub = ctx.stateHub;
        if (stateHub == null)
        {
            reason = "Initiating character has no StateHub.";
            return false;
        }

        if (!stateHub.IsAlive || stateHub.Isdown)
        {
            reason = "Initiating character is not alive.";
            return false;
        }

        if (stateHub.UISM != null && stateHub.UISM.CurrentId != UIStateId.Normal)
        {
            reason = "A menu is open.";
            return false;
        }

        if (stateHub.MoveSM != null)
        {
            MoveStateId move = stateHub.MoveSM.CurrentId;
            if (move == MoveStateId.Dash || move == MoveStateId.Stunned || move == MoveStateId.Knockback)
            {
                reason = $"Initiating character is in move state '{move}'.";
                return false;
            }
        }

        if (stateHub.WeaponSM != null)
        {
            WeaponStateId weapon = stateHub.WeaponSM.CurrentId;
            if (weapon == WeaponStateId.Firing ||
                weapon == WeaponStateId.Reloading ||
                weapon == WeaponStateId.Melee)
            {
                reason = $"Initiating character is in weapon state '{weapon}'.";
                return false;
            }
        }

        if (ctx.DashSystem != null && ctx.DashSystem.IsDashing)
        {
            reason = "Initiating character is dashing.";
            return false;
        }

        WeaponSystem weaponSystem = ctx.WeaponSystem;
        if (weaponSystem != null && (weaponSystem.IsReloading || weaponSystem.IsFiringActivity))
        {
            reason = "Initiating character is shooting or reloading.";
            return false;
        }

        if (stateHub.IsAirborne)
        {
            reason = "Initiating character is airborne.";
            return false;
        }

        CharacterAnimBrain animBrain = ctx.AnimBrain;
        if (animBrain != null && animBrain.IsSkillPlaybackActive)
        {
            reason = "A skill is still playing.";
            return false;
        }

        reason = string.Empty;
        return true;
    }
}
