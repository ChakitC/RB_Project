public static class CharacterActionPolicy
{
    // Continuation ignores menus and its own activity; resources and geometry belong to owners.
    public static ActionAdmissionResult Evaluate(CharacterAction action, in CharacterActionFacts f, bool continuing = false)
    {
        if (!f.Initialized) return Deny(ActionBlockReason.NotInitialized);
        if (f.Life == LifeStateId.Dead ||
            (f.Life == LifeStateId.Down && action != CharacterAction.Move && action != CharacterAction.Rotate))
            return Deny(ActionBlockReason.NotAlive);
        bool survivesMenu = continuing && (action == CharacterAction.Reload || action == CharacterAction.Skill ||
            action == CharacterAction.Melee || action == CharacterAction.Block || action == CharacterAction.Chain);
        if (!survivesMenu && (f.UI != UIStateId.Normal || f.Paused)) return Deny(ActionBlockReason.MenuOpen);
        if (continuing && f.ProtectedPlayback && (action == CharacterAction.Skill || action == CharacterAction.Chain))
            return Allow();
        if (f.Stunned || Has(f, RequiredControl(action))) return Deny(ActionBlockReason.ControlBlocked);
        if (continuing && survivesMenu)
            return f.Knockback ? Deny(ActionBlockReason.Busy) : Allow();
        if (f.ProtectedPlayback) return Deny(ActionBlockReason.Busy);
        if (action == CharacterAction.Rotate) return Allow();
        bool reloadOverDash = action == CharacterAction.Reload && !f.FullBodyReloadConfigured;
        if (f.Knockback || (f.Dashing && action != CharacterAction.Slide && !reloadOverDash &&
            !(continuing && (action == CharacterAction.Dash || action == CharacterAction.Sprint))))
            return Deny(ActionBlockReason.Busy);

        switch (action)
        {
            case CharacterAction.Move:
            case CharacterAction.Crouch:
            case CharacterAction.Slide:
            case CharacterAction.Sprint:
                if (f.Melee || f.FullBodyReload || f.RootMotion || f.ShootBlockingPlayback)
                    return Deny(ActionBlockReason.Busy);
                if (action == CharacterAction.Sprint && (f.Reloading || f.Aiming || f.Firing || f.Crouching || f.Sliding))
                    return Deny(ActionBlockReason.Busy);
                return Allow(action == CharacterAction.Slide ? ActionCancellation.Dash | ActionCancellation.Sprint :
                    action == CharacterAction.Crouch ? ActionCancellation.Sprint : ActionCancellation.None);
            case CharacterAction.Dash:
                if (f.Melee || f.ShootBlockingPlayback) return Deny(ActionBlockReason.Busy);
                if (!f.HasDash) return Deny(ActionBlockReason.MissingModule);
                return Allow(ActionCancellation.Slide | ActionCancellation.Firing |
                    (f.FullBodyReload ? ActionCancellation.Reload : ActionCancellation.None));
            case CharacterAction.Reload:
                if (!f.HasWeapon) return Deny(ActionBlockReason.MissingModule);
                if (f.Melee || f.ShootBlockingPlayback) return Deny(ActionBlockReason.Busy);
                return Allow(ActionCancellation.Sprint | ActionCancellation.Firing |
                    (f.FullBodyReloadConfigured ? ActionCancellation.Slide : ActionCancellation.None));
            case CharacterAction.Skill:
                if (f.Melee || f.ShootBlockingPlayback) return Deny(ActionBlockReason.Busy);
                if (!f.HasSkill) return Deny(ActionBlockReason.MissingModule);
                return Allow(CombatCancellations);
            case CharacterAction.Melee:
                if (f.SkillPlayback) return Deny(ActionBlockReason.Busy);
                if (f.Reloading && !f.MeleeCanInterruptReload) return Deny(ActionBlockReason.Reloading);
                return f.HasMelee ? Allow(CombatCancellations) : Deny(ActionBlockReason.MissingModule);
            case CharacterAction.FireRequest:
            case CharacterAction.Shoot:
                if (!f.HasWeapon) return Deny(ActionBlockReason.MissingModule);
                if (f.Melee || f.ShootBlockingPlayback) return Deny(ActionBlockReason.Busy);
                if (f.Reloading && (action != CharacterAction.FireRequest || !f.CanInterruptReload))
                    return Deny(ActionBlockReason.Reloading);
                if (f.EmptyMagazine) return Deny(ActionBlockReason.EmptyMagazine);
                return Allow(ActionCancellation.Sprint | (f.Reloading ? ActionCancellation.Reload : ActionCancellation.None));
            case CharacterAction.Aim:
                if (!f.HasWeapon) return Deny(ActionBlockReason.MissingModule);
                if (f.Melee || f.ShootBlockingPlayback) return Deny(ActionBlockReason.Busy);
                return Allow(ActionCancellation.Sprint);
            case CharacterAction.Block:
            case CharacterAction.Chain:
                // Target/window/party and animation authority stay with the existing owners.
                return Allow(ActionCancellation.Sprint | ActionCancellation.Slide);
            default:
                return Deny(ActionBlockReason.Busy);
        }
    }

    const ActionCancellation CombatCancellations = ActionCancellation.Sprint | ActionCancellation.Slide |
        ActionCancellation.Reload | ActionCancellation.Firing;
    static ControlBlockFlags RequiredControl(CharacterAction action) => action switch
    {
        CharacterAction.Move or CharacterAction.Dash or CharacterAction.Sprint or CharacterAction.Crouch or CharacterAction.Slide => ControlBlockFlags.Move,
        CharacterAction.FireRequest or CharacterAction.Shoot or CharacterAction.Reload or CharacterAction.Aim => ControlBlockFlags.Shoot,
        CharacterAction.Skill or CharacterAction.Chain => ControlBlockFlags.Skill,
        CharacterAction.Rotate => ControlBlockFlags.Rotate,
        _ => ControlBlockFlags.None
    };
    static bool Has(in CharacterActionFacts f, ControlBlockFlags flag) => (f.Blocks & flag) != 0;
    static ActionAdmissionResult Allow(ActionCancellation cancel = ActionCancellation.None) => new(ActionBlockReason.None, cancel);
    static ActionAdmissionResult Deny(ActionBlockReason reason) => new(reason);
}
