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

    public int SkillLevelDelta { get; private set; }
    public bool IsEmpty => SkillLevelDelta == 0 && _modifiers.Count == 0;

    public void AddNode(SkillUpgradeNodeData node)
    {
        if (node == null)
            return;

        SkillLevelDelta += node.skillLevelDelta;
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
               stat == StatType.StaggerPower;
    }

    static float Apply(float value, Aggregate aggregate)
    {
        float multiplier = float.IsFinite(aggregate.multiply) ? aggregate.multiply : 1f;
        float addition = float.IsFinite(aggregate.add) ? aggregate.add : 0f;
        return (value + addition) * multiplier;
    }
}
