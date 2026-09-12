using System;

public static class PartyComboTriggerEvaluator
{
    public static bool Matches(
        PartyComboSkillDef definition,
        PartyRuntime party,
        PartyRuntimeActor owner,
        PartyRuntimeActor eventActor,
        in PassiveEventContext context)
    {
        PartyComboTriggerRule rule = definition != null ? definition.trigger : null;
        if (definition == null || rule == null || owner == null || party == null)
            return false;

        if (!MatchesSource(rule.sourceRelation, party, owner, eventActor))
            return false;
        if (!MatchesOrigin(rule.originFilter, context.Origin))
            return false;
        if (!string.IsNullOrWhiteSpace(rule.eventSourceId) &&
            !string.Equals(rule.eventSourceId, context.EventSourceId, StringComparison.Ordinal))
        {
            return false;
        }

        switch (rule.kind)
        {
            case PartyComboTriggerKind.ComboSkillCommitted:
                if (context.Type != PassiveEventType.ComboSkillCommitted)
                    return false;
                return rule.allowSameComboSkill ||
                       !string.Equals(context.Metadata.ComboSkillId, definition.RuntimeId, StringComparison.Ordinal);

            case PartyComboTriggerKind.TargetHasStatus:
                return (context.Type == PassiveEventType.StatusApplied ||
                        context.Type == PassiveEventType.StatusStackChanged) &&
                       TargetHasRequiredStatus(rule, context);

            case PartyComboTriggerKind.EnteredBreak:
                return PartyComboFeatureGate.PublishersEnabled &&
                       context.Type == PassiveEventType.Hit &&
                       context.Metadata.EnteredChainReady;

            default:
                return false;
        }
    }

    public static bool IsSupportedMvpTrigger(PartyComboTriggerKind kind)
    {
        return kind == PartyComboTriggerKind.ComboSkillCommitted ||
               kind == PartyComboTriggerKind.TargetHasStatus ||
               kind == PartyComboTriggerKind.EnteredBreak;
    }

    static bool MatchesSource(
        PartyComboSourceRelation relation,
        PartyRuntime party,
        PartyRuntimeActor owner,
        PartyRuntimeActor eventActor)
    {
        if (eventActor == null)
            return false;

        return relation switch
        {
            PartyComboSourceRelation.ControlledActor => eventActor.Role == ChainActorRole.Player,
            PartyComboSourceRelation.Self => eventActor.Role == owner.Role,
            PartyComboSourceRelation.OtherPartyMember => eventActor.Role != owner.Role,
            PartyComboSourceRelation.AnyPartyMember => party.GetActor(eventActor.Role) != null,
            _ => false,
        };
    }

    static bool MatchesOrigin(PassiveOriginFilter filter, PassiveEventOrigin origin)
    {
        return filter switch
        {
            PassiveOriginFilter.ExternalOnly => origin == PassiveEventOrigin.External,
            PassiveOriginFilter.NonPassive => origin != PassiveEventOrigin.Passive,
            PassiveOriginFilter.PassiveOnly => origin == PassiveEventOrigin.Passive,
            PassiveOriginFilter.Any => true,
            _ => false,
        };
    }

    static bool TargetHasRequiredStatus(PartyComboTriggerRule rule, in PassiveEventContext context)
    {
        if (context.Target == null)
            return false;

        CharacteContext target = CharacterContextModuleLookup.ResolveContext(context.Target);
        target?.ResolveReferences();
        StatusEffectController statusController = target != null ? target.StatusEffects : null;
        if (statusController == null)
            return false;

        int requiredStacks = Math.Max(1, rule.requiredStacks);
        for (int i = 0; i < statusController.ActiveEffects.Count; i++)
        {
            StatusEffectInstance instance = statusController.ActiveEffects[i];
            if (instance == null || instance.CurrentStacks < requiredStacks)
                continue;

            StatusEffectDef status = instance.Definition;
            if (rule.requiredStatus != null && status != rule.requiredStatus)
                continue;
            if (!string.IsNullOrWhiteSpace(rule.requiredStatusTag) && !HasTag(status, rule.requiredStatusTag))
                continue;

            return rule.requiredStatus != null || !string.IsNullOrWhiteSpace(rule.requiredStatusTag);
        }

        return false;
    }

    static bool HasTag(StatusEffectDef status, string requiredTag)
    {
        if (status == null || status.tags == null)
            return false;

        for (int i = 0; i < status.tags.Count; i++)
        {
            if (string.Equals(status.tags[i], requiredTag, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
