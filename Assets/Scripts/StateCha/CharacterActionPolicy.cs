public static class CharacterActionPolicy
{
    public static ActionAdmissionResult Evaluate(CharacterAction action, in CharacterActionFacts f)
    {
        if (!f.Initialized) return Deny(ActionBlockReason.NotInitialized);
        if (action == CharacterAction.Rotate)
            return f.Stunned || Has(f, ControlBlockFlags.Rotate) ? Deny(ActionBlockReason.ControlBlocked) : Allow();
        if (f.Life == LifeStateId.Dead || (f.Life == LifeStateId.Down && action != CharacterAction.Move))
            return Deny(ActionBlockReason.NotAlive);

        // Reload has historically allowed menus/root/stun; preserve that policy, but not knockback.
        if (action == CharacterAction.Reload)
        {
            if (!f.HasWeapon) return Deny(ActionBlockReason.MissingModule);
            return f.ShootBlockingPlayback || f.Knockback ? Deny(ActionBlockReason.Busy) : Allow();
        }
        if (f.UI != UIStateId.Normal) return Deny(ActionBlockReason.MenuOpen);
        if (f.Knockback || f.Dashing) return Deny(ActionBlockReason.Busy);

        switch (action)
        {
            case CharacterAction.Move:
            case CharacterAction.Dash:
                if (f.Stunned || Has(f, ControlBlockFlags.Move)) return Deny(ActionBlockReason.ControlBlocked);
                if (f.Melee || (action == CharacterAction.Move && f.FullBodyReload)) return Deny(ActionBlockReason.Busy);
                if (action == CharacterAction.Dash && !f.HasDash) return Deny(ActionBlockReason.MissingModule);
                return Allow();
            case CharacterAction.Skill:
                if (f.Stunned || Has(f, ControlBlockFlags.Skill)) return Deny(ActionBlockReason.ControlBlocked);
                if (f.Melee) return Deny(ActionBlockReason.Busy);
                return f.HasSkill ? Allow() : Deny(ActionBlockReason.MissingModule);
            case CharacterAction.Melee:
                if (f.Stunned) return Deny(ActionBlockReason.ControlBlocked);
                if (f.SkillPlayback) return Deny(ActionBlockReason.Busy);
                return f.HasMelee ? Allow() : Deny(ActionBlockReason.MissingModule);
            case CharacterAction.FireRequest:
            case CharacterAction.Shoot:
                if (!f.HasWeapon) return Deny(ActionBlockReason.MissingModule);
                if (f.Stunned || Has(f, ControlBlockFlags.Shoot)) return Deny(ActionBlockReason.ControlBlocked);
                if (f.Melee || f.ShootBlockingPlayback) return Deny(ActionBlockReason.Busy);
                if (f.Reloading && (action != CharacterAction.FireRequest || !f.CanInterruptReload))
                    return Deny(ActionBlockReason.Reloading);
                if (f.EmptyMagazine) return Deny(ActionBlockReason.EmptyMagazine);
                return Allow();
            default:
                return Deny(ActionBlockReason.Busy);
        }
    }

    static bool Has(in CharacterActionFacts f, ControlBlockFlags flag) => (f.Blocks & flag) != 0;
    static ActionAdmissionResult Allow() => new(ActionBlockReason.None);
    static ActionAdmissionResult Deny(ActionBlockReason reason) => new(reason);
}
