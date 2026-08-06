using System;
using System.Collections.Generic;
using UnityEngine;

public enum WeaponAffixRuntimeKind
{
    StaticStat, ReloadBuff, EchoChamber, BreachChamber, ImpactCore, KillClip,
    ExecutionFeed, LastRound, HeadHunter, FreshChamber, HotStreak, PressurePoint,
    ConservationRound, Overkill, BrokenGuard, MarkedQuarry, BloodMagazine
}

[CreateAssetMenu(menuName = "Game/Weapons/Affixes/Configured Behavior", fileName = "WeaponAffixBehavior")]
public sealed class ConfiguredWeaponAffixBehavior : WeaponAffixBehavior
{
    public WeaponAffixRuntimeKind kind;
    public StatType statType;
    public ModifierOp modifierOp = ModifierOp.AddPercent;
    public float duration = 4f;
    public int cap = 1;
    public int threshold = 1;
    public float fixedValue;
    public float secondaryValue;
    public int minimumBaseMagazine;
    public float procChance = 1f;
    public ProjectileConfig projectileConfig;
    public GameObject projectilePrefab;

    public override bool IsEligible(GunConfig weapon)
    {
        return base.IsEligible(weapon) && (minimumBaseMagazine <= 0 ||
            Mathf.Max(weapon.magazine, weapon.maxMagazine) >= minimumBaseMagazine);
    }

    public override IWeaponAffixRuntime CreateRuntime(WeaponAffixRuntimeContext context)
    {
        return new ConfiguredWeaponAffixRuntime(context, this);
    }
}

sealed class ConfiguredWeaponAffixRuntime : WeaponAffixRuntimeBase,
    IWeaponAffixStatRuntime, IWeaponAffixPreShotRuntime, IWeaponAffixPreDamageRuntime,
    IWeaponAffixCombatEventRuntime, IWeaponAffixTimerRuntime
{
    readonly ConfiguredWeaponAffixBehavior behavior;
    readonly float roll;
    readonly string modifierKey;
    WeaponAffixRuntimeStateEntry progress;
    int stacks;
    int charges;
    int targetId;
    float remaining;
    string lastAttackId;

    public ConfiguredWeaponAffixRuntime(WeaponAffixRuntimeContext context, ConfiguredWeaponAffixBehavior behavior) : base(context)
    {
        this.behavior = behavior;
        roll = context.Roll != null && context.Roll.hasPrimaryValue ? context.Roll.primaryValue : 0f;
        modifierKey = $"weapon:{context.WeaponInstanceId}:affix:{AffixId}";
        if (behavior.kind == WeaponAffixRuntimeKind.EchoChamber || behavior.kind == WeaponAffixRuntimeKind.BreachChamber ||
            behavior.kind == WeaponAffixRuntimeKind.ConservationRound)
            progress = GetEntry("progress");
    }

    WeaponAffixRuntimeStateEntry GetEntry(string key)
    {
        var entries = Context.PersistentState.entries;
        for (int i = 0; i < entries.Count; i++) if (entries[i] != null && entries[i].key == key) return entries[i];
        var entry = new WeaponAffixRuntimeStateEntry { key = key };
        entries.Add(entry);
        return entry;
    }

    public void AppendStatModifiers(List<RuntimeStatModifier> buffer)
    {
        switch (behavior.kind)
        {
            case WeaponAffixRuntimeKind.StaticStat:
            case WeaponAffixRuntimeKind.ImpactCore:
                buffer.Add(new RuntimeStatModifier(behavior.statType, behavior.modifierOp, roll, modifierKey));
                break;
            case WeaponAffixRuntimeKind.ReloadBuff:
            case WeaponAffixRuntimeKind.KillClip:
                if (remaining > 0f) buffer.Add(new RuntimeStatModifier(behavior.statType, behavior.modifierOp, roll, modifierKey));
                break;
            case WeaponAffixRuntimeKind.HeadHunter:
                if (stacks > 0) buffer.Add(new RuntimeStatModifier(StatType.CritMultiplier, ModifierOp.Flat, roll * stacks, modifierKey));
                break;
            case WeaponAffixRuntimeKind.HotStreak:
                if (stacks > 0) buffer.Add(new RuntimeStatModifier(StatType.FireInterval, ModifierOp.AddPercent, -roll * stacks, modifierKey));
                break;
            case WeaponAffixRuntimeKind.BloodMagazine:
                if (remaining > 0f) buffer.Add(new RuntimeStatModifier(StatType.Stability, ModifierOp.Flat, 15f, modifierKey));
                break;
        }
    }

    public void ModifyShot(ref WeaponShotBuildContext shot)
    {
        if (!shot.AmmoConsumed) return;
        switch (behavior.kind)
        {
            case WeaponAffixRuntimeKind.LastRound:
                if (shot.IsLastRound)
                {
                    shot.ImpactPayload = new WeaponAffixImpactPayload(AffixId, 2.5f, shot.Damage * roll * .01f);
                    shot.Damage *= 1.25f;
                }
                break;
            case WeaponAffixRuntimeKind.FreshChamber:
                if (charges > 0) { shot.Damage *= 1f + roll * 0.01f; charges--; Feedback(WeaponAffixProcEvent.Consumed, charges); }
                break;
            case WeaponAffixRuntimeKind.BrokenGuard:
                if (charges > 0) { shot.CritRate = 100f; shot.CritMultiplier += roll; charges = 0; Feedback(WeaponAffixProcEvent.Consumed, 0); }
                break;
            case WeaponAffixRuntimeKind.EchoChamber:
            case WeaponAffixRuntimeKind.BreachChamber:
                progress.intValue++;
                int needed = Mathf.Max(1, behavior.threshold);
                if (progress.intValue >= needed)
                {
                    progress.intValue = 0;
                    if (behavior.procChance <= 0f || UnityEngine.Random.value <= behavior.procChance)
                        Context.WeaponSystem.SpawnAffixProjectile(behavior.projectileConfig, behavior.projectilePrefab, behavior.fixedValue, behavior.secondaryValue);
                }
                break;
        }
    }

    public void ModifyDamage(ref WeaponDamageBuildContext damage)
    {
        if (behavior.kind == WeaponAffixRuntimeKind.PressurePoint && damage.Target != null && damage.Target.GetInstanceID() == targetId)
            damage.StaggerPower *= 1f + roll * stacks * 0.01f;
        else if (behavior.kind == WeaponAffixRuntimeKind.MarkedQuarry && stacks >= 5 && damage.Target != null && damage.Target.GetInstanceID() == targetId)
            damage.Damage *= 1f + roll * 0.01f;
    }

    public void OnCombatEvent(in PassiveEventContext evt)
    {
        bool hit = evt.Type == PassiveEventType.Hit && evt.Metadata.AppliedDamage > 0f;
        bool kill = evt.Type == PassiveEventType.Kill && evt.Metadata.AppliedDamage > 0f;
        int eventTarget = evt.TargetInstanceId;
        switch (behavior.kind)
        {
            case WeaponAffixRuntimeKind.ReloadBuff:
                if (evt.Type == PassiveEventType.Reload) ActivateTimer();
                break;
            case WeaponAffixRuntimeKind.KillClip:
                if (kill) ActivateTimer();
                break;
            case WeaponAffixRuntimeKind.ExecutionFeed:
                if (kill) RestoreMagazine(Mathf.CeilToInt(Context.WeaponSystem.MagazineSize * roll * .01f));
                break;
            case WeaponAffixRuntimeKind.HeadHunter:
                if (hit && OncePerAttack(evt.AttackId))
                {
                    if (evt.Metadata.HitZone == CharacterHitZone.Head) { stacks = Mathf.Min(5, stacks + 1); ActivateTimer(); }
                    else { stacks = 0; remaining = 0f; Dirty(); }
                }
                break;
            case WeaponAffixRuntimeKind.FreshChamber:
                if (evt.Type == PassiveEventType.Reload) { charges = 3; Feedback(WeaponAffixProcEvent.ChargeReady, charges); }
                break;
            case WeaponAffixRuntimeKind.HotStreak:
                if (kill) { stacks = Mathf.Min(3, stacks + 1); ActivateTimer(); }
                break;
            case WeaponAffixRuntimeKind.PressurePoint:
                if (hit && OncePerAttack(evt.AttackId)) TrackTarget(eventTarget, 5);
                break;
            case WeaponAffixRuntimeKind.ConservationRound:
                if (hit && OncePerAttack(evt.AttackId)) { progress.intValue++; if (progress.intValue >= Mathf.Max(1, Mathf.RoundToInt(roll))) { if (RestoreMagazine(1)) progress.intValue = 0; } }
                break;
            case WeaponAffixRuntimeKind.BrokenGuard:
                if (hit && evt.Metadata.EnteredChainReady) { charges = 1; remaining = 5f; Feedback(WeaponAffixProcEvent.ChargeReady, 1); }
                break;
            case WeaponAffixRuntimeKind.MarkedQuarry:
                if (hit && OncePerAttack(evt.AttackId)) TrackTarget(eventTarget, 5);
                break;
            case WeaponAffixRuntimeKind.BloodMagazine:
                if (kill && OncePerAttack(evt.AttackId) && evt.Metadata.AmmoAfter <= Mathf.FloorToInt(evt.Metadata.MaxMagazine * .25f))
                { RestoreMagazine(Mathf.CeilToInt(evt.Metadata.MaxMagazine * roll * .01f)); ActivateTimer(); }
                break;
            case WeaponAffixRuntimeKind.Overkill:
                if (kill && evt.Metadata.MaxHealth > 0f &&
                    evt.Metadata.OverkillAmount >= evt.Metadata.MaxHealth * .2f && evt.Target != null)
                {
                    float explosionDamage = Mathf.Min(
                        evt.Metadata.OverkillAmount * roll * .01f,
                        evt.Metadata.ResolvedDamage);
                    WeaponAffixAreaDamage.Apply(
                        Context.Character,
                        Context.CombatEventBus,
                        evt.Target.transform.position,
                        3f,
                        explosionDamage,
                        Context.WeaponInstanceId,
                        AffixId,
                        evt.AttackId,
                        evt.ChainId,
                        evt.Depth);
                }
                break;
        }
    }

    void TrackTarget(int id, int max)
    {
        if (targetId != id) { targetId = id; stacks = 0; }
        stacks = Mathf.Min(max, stacks + 1); remaining = behavior.duration > 0f ? behavior.duration : 3f; Feedback(WeaponAffixProcEvent.StackChanged, stacks);
    }
    bool OncePerAttack(string attackId) { if (string.IsNullOrEmpty(attackId) || attackId == lastAttackId) return false; lastAttackId = attackId; return true; }
    bool RestoreMagazine(int amount) => Context.WeaponSystem != null && Context.WeaponSystem.RestoreMagazineFromAffix(Mathf.Max(1, amount));
    void ActivateTimer() { remaining = Mathf.Max(.01f, behavior.duration); Dirty(); Feedback(WeaponAffixProcEvent.Activated, stacks); }
    void Dirty() { Context.StatsHub?.MarkDirty(); }
    void Feedback(WeaponAffixProcEvent procEvent, int value) { Context.PublishFeedback?.Invoke(AffixId, procEvent, value); }

    public bool Tick(float deltaTime)
    {
        if (remaining <= 0f) return false;
        remaining = Mathf.Max(0f, remaining - Mathf.Max(0f, deltaTime));
        if (remaining <= 0f) { stacks = 0; charges = behavior.kind == WeaponAffixRuntimeKind.BrokenGuard ? 0 : charges; Dirty(); Feedback(WeaponAffixProcEvent.Expired, 0); }
        return remaining > 0f;
    }

    public override void OnUnequip() { stacks = charges = targetId = 0; remaining = 0f; Dirty(); }
}
