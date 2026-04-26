using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SupportInstance
{
    public SupportGemDefinition def;
    public int level = 1;

    public bool CanSupport(SkillGemDefinition skill)
        => def != null && def.CanSupport(skill);

    public void Apply(FinalSkillStats stats)
    {
        if (def == null || stats == null) return;

        // พื้นฐาน
        ApplyModifiers(stats, def.statModifiers);

        // เลเวลเพิ่ม (ถ้ามี)
        if (def.perLevelScaling != null)
        {
            foreach (var scaling in def.perLevelScaling)
            {
                if (scaling == null) continue;
                if (level < scaling.requiredLevel) continue;
                if (scaling.extraModifiers == null) continue;

                ApplyModifiers(stats, scaling.extraModifiers);
            }
        }
    }

    private void ApplyModifiers(FinalSkillStats stats, List<StatModifier> mods)
    {
        if (mods == null) return;

        foreach (var mod in mods)
        {
            if (mod == null) continue;

            switch (mod.stat)
            {
                case StatType.Damage:
                    stats.damage = (stats.damage + mod.add) * mod.mul;
                    break;

                case StatType.ManaCost:
                    stats.manaCost = (stats.manaCost + mod.add) * mod.mul;
                    break;

                case StatType.AreaRadius:
                    stats.areaRadius = (stats.areaRadius + mod.add) * mod.mul;
                    break;

                case StatType.ProjectileCount:
                    stats.projectileCount =
                        Mathf.RoundToInt((stats.projectileCount + mod.add) * mod.mul);
                    break;

                case StatType.CastTime:
                    stats.castTime = (stats.castTime + mod.add) * mod.mul;
                    break;

                case StatType.Cooldown:
                    stats.cooldown = (stats.cooldown + mod.add) * mod.mul;
                    break;

                case StatType.CritChance:
                    stats.critChance = (stats.critChance + mod.add) * mod.mul;
                    break;

                case StatType.StaggerPower:
                    stats.staggerPower = (stats.staggerPower + mod.add) * mod.mul;
                    break;
            }
        }
    }
    
    private float ApplyFloat(float current, StatModifier mod)
    {
        // สูตรเดียวกับที่นายใช้ใน switch:
        // (ค่าเดิม + add) * mul
        return (current + mod.add) * mod.mul;
    }

    private int ApplyInt(int current, StatModifier mod)
    {
        return Mathf.RoundToInt((current + mod.add) * mod.mul);
    }

    public virtual void OnCast(SkillInstance skill, FinalSkillStats stats)
    {
        // ถ้ามี effect ตอน cast ค่อยใส่ทีหลังได้
    }
}
