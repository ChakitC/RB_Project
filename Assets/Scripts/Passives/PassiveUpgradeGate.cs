public static class PassiveUpgradeGate
{
    public static bool IsRuleEnabled(TriggeredPassiveRule rule, SkillUpgradeStatSnapshot upgrades)
        => rule == null
           || string.IsNullOrWhiteSpace(rule.requiredUpgradeId)
           || (upgrades != null && upgrades.HasUpgrade(rule.requiredUpgradeId));
}
