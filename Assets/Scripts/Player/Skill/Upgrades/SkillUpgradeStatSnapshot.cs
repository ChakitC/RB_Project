using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class SkillUpgradeStatSnapshot
{
    struct Aggregate
    {
        public float add;
        public float multiply;
    }

    readonly Dictionary<StatType, Aggregate> _modifiers = new();
    readonly HashSet<string> _upgradeIds = new(StringComparer.Ordinal);

    public bool IsEmpty => _modifiers.Count == 0 && _upgradeIds.Count == 0;
    public IReadOnlyCollection<string> UpgradeIds => _upgradeIds;

    public void AddNode(SkillUpgradeNodeData node)
    {
        if (node == null)
            return;

        if (node.grantedUpgradeIds != null)
        {
            for (int i = 0; i < node.grantedUpgradeIds.Count; i++)
            {
                string id = node.grantedUpgradeIds[i];
                if (!string.IsNullOrWhiteSpace(id))
                    _upgradeIds.Add(id.Trim());
            }
        }

        if (node.statModifiers == null)
            return;

        for (int i = 0; i < node.statModifiers.Count; i++)
        {
            StatModifier modifier = node.statModifiers[i];
            if (modifier == null || !Supports(modifier.stat))
                continue;

            if (!_modifiers.TryGetValue(modifier.stat, out Aggregate aggregate))
                aggregate.multiply = 1f;

            aggregate.add += modifier.add;
            aggregate.multiply *= modifier.mul;
            _modifiers[modifier.stat] = aggregate;
        }
    }

    public bool HasUpgrade(string id) => !string.IsNullOrWhiteSpace(id) && _upgradeIds.Contains(id.Trim());

    public void Apply(FinalSkillStats stats)
    {
        if (stats == null)
            return;

        foreach (KeyValuePair<StatType, Aggregate> pair in _modifiers)
        {
            Aggregate aggregate = pair.Value;
            switch (pair.Key)
            {
                case StatType.Damage:
                    stats.damage = Apply(stats.damage, aggregate);
                    break;
                case StatType.AreaRadius:
                    stats.areaRadius = Apply(stats.areaRadius, aggregate);
                    break;
                case StatType.ProjectileCount:
                    stats.projectileCount = Mathf.RoundToInt(Apply(stats.projectileCount, aggregate));
                    break;
                case StatType.ManaCost:
                    stats.manaCost = Apply(stats.manaCost, aggregate);
                    break;
                case StatType.CastTime:
                    stats.castTime = Apply(stats.castTime, aggregate);
                    break;
                case StatType.Cooldown:
                    stats.cooldown = Apply(stats.cooldown, aggregate);
                    break;
                case StatType.CritChance:
                    stats.critChance = Apply(stats.critChance, aggregate);
                    break;
                case StatType.StaggerPower:
                    stats.staggerPower = Apply(stats.staggerPower, aggregate);
                    break;
                case StatType.EffectDuration:
                    stats.effectDuration = Apply(stats.effectDuration, aggregate);
                    break;
                case StatType.HealPower:
                    stats.healPower = Apply(stats.healPower, aggregate);
                    break;
            }
        }
    }

    public static bool Supports(StatType stat)
    {
        return stat == StatType.Damage ||
               stat == StatType.AreaRadius ||
               stat == StatType.ProjectileCount ||
               stat == StatType.ManaCost ||
               stat == StatType.CastTime ||
               stat == StatType.Cooldown ||
               stat == StatType.CritChance ||
               stat == StatType.StaggerPower ||
               stat == StatType.EffectDuration ||
               stat == StatType.HealPower;
    }

    static float Apply(float value, Aggregate aggregate)
    {
        float multiplier = float.IsFinite(aggregate.multiply) ? aggregate.multiply : 1f;
        float addition = float.IsFinite(aggregate.add) ? aggregate.add : 0f;
        return (value + addition) * multiplier;
    }
}
