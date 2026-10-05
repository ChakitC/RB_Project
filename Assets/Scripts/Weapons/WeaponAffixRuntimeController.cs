using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Equipment adapter only. PassiveController owns all live rules, state and subscriptions.</summary>
public class WeaponAffixRuntimeController : MonoBehaviour, IStatModifierProvider, IWeaponRuntimeEffectHandler, IWeaponAffixPreDamageRuntime, IPassiveGrantProvider
{
    [Header("Refs")]
    [SerializeField] private WeaponSystem weaponSystem;
    [SerializeField] private StatsHub statsHub;
    [SerializeField] private StatusEffectController statusEffectController;
    [SerializeField] private WeaponAffixDatabase affixDatabase;
    CharacteContext ctx;
    WeaponPassiveDamageAdapter damageAdapter;
    public event Action StatModifiersChanged;
    public event Action<string, WeaponAffixProcEvent, int> FeedbackPublished;

    public IWeaponAffixPreDamageRuntime PreDamageRuntime
    {
        get
        {
            if (ctx?.PassiveController == null || !ctx.PassiveController.HasWeaponPhase(PassiveRuleTrigger.BeforeDamage)) return null;
            if (damageAdapter == null || damageAdapter.Life != ctx.LifeGeneration)
                damageAdapter = new WeaponPassiveDamageAdapter(ctx);
            return damageAdapter;
        }
    }
    void Awake() => ResolveReferences();
    void ResolveReferences()
    {
        if (ctx == null) ctx = CharacterContextModuleLookup.ResolveContext(gameObject);
        ctx?.ResolveReferences();
        if (ctx != null) { weaponSystem = ctx.WeaponSystem; statsHub = ctx.StatsHub; statusEffectController = ctx.StatusEffects; }
        if (weaponSystem == null) TryGetComponent(out weaponSystem);
    }
    void OnEnable() { ResolveReferences(); Refresh(); }
    void OnDisable() { damageAdapter = null; Refresh(); }
    void Refresh()
    {
        EnsurePassiveModules();
        ctx?.PassiveController?.RefreshPassiveLoadout();
        StatModifiersChanged?.Invoke();
        statsHub?.MarkDirty();
    }
    void EnsurePassiveModules()
    {
        var weapon = weaponSystem != null ? weaponSystem.CurrentWeaponInstance : null;
        if (!isActiveAndEnabled || ctx == null || weapon == null || affixDatabase == null) return;
        bool hasGrant = false, needsBus = false;
        Inspect(weapon.mainAffix);
        if (weapon.subAffixes != null) foreach (var roll in weapon.subAffixes) Inspect(roll);
        if (!hasGrant) return;
        if (needsBus && ctx.CombatEventBus == null)
        {
            ctx.CombatEventBus = ctx.gameObject.AddComponent<CombatEventBus>();
            weaponSystem.RefreshPassiveEventReference();
        }
        if (ctx.PassiveController == null) ctx.PassiveController = ctx.gameObject.AddComponent<PassiveController>();
        void Inspect(RolledAffixData roll)
        {
            if (roll == null || roll.IsEmpty) return;
            var recipe = affixDatabase.GetById(roll.affixId)?.passiveDefinition?.recipe;
            if (recipe == null) return;
            hasGrant = true;
            foreach (var rule in recipe.rules)
                if (rule != null && (rule.trigger == PassiveRuleTrigger.Combat || rule.trigger == PassiveRuleTrigger.BeforeShot ||
                    rule.trigger == PassiveRuleTrigger.ShotCommitted || rule.trigger == PassiveRuleTrigger.BeforeDamage)) needsBus = true;
        }
    }
    public void NotifyWeaponEquipped() { ResolveReferences(); Refresh(); }
    public void HandleShotFired() { }
    public void HandleReloadCompleted() { }
    // Kept for existing component/API references. Preparation is transactional in WeaponSystem.
    public void ModifyShot(ref WeaponShotBuildContext context) { }
    public void ModifyDamage(ref WeaponDamageBuildContext context) => PreDamageRuntime?.ModifyDamage(ref context);
    public void AppendStatModifiers(List<RuntimeStatModifier> buffer) { }
    public void PublishFeedback(string id, WeaponAffixProcEvent kind, int value) => FeedbackPublished?.Invoke(id, kind, value);

    public void AppendPassiveGrants(List<PassiveGrant> buffer)
    {
        ResolveReferences();
        var weapon = weaponSystem != null ? weaponSystem.CurrentWeaponInstance : null;
        if (!isActiveAndEnabled || weapon == null || affixDatabase == null) return;
        NormalizeGrantIds(weapon);
        Add(buffer, weapon, weapon.mainAffix);
        if (weapon.subAffixes != null) foreach (var roll in weapon.subAffixes) Add(buffer, weapon, roll);
    }
    void Add(List<PassiveGrant> buffer, WeaponInstanceData weapon, RolledAffixData roll)
    {
        if (roll == null || roll.IsEmpty) return;
        var definition = affixDatabase.GetById(roll.affixId);
        if (definition == null) return;
        if (definition.passiveDefinition == null)
        { Debug.LogError("[Passive] Unmigrated weapon affix: " + definition.affixId, definition); return; }
        var parameters = new Dictionary<string, float> { ["roll"] = roll.hasPrimaryValue ? roll.primaryValue : 0f };
        bool persistent = false;
        foreach (var variable in definition.passiveDefinition.recipe.variables) persistent |= variable.persistent;
        int life = ctx.LifeGeneration;
        buffer.Add(new PassiveGrant(definition.passiveDefinition, "weapon:" + weapon.instanceId, roll.grantId,
            options: new PassiveGrantOptions(weapon.instanceId,
                persistent ? new WeaponPassiveStateStore(weapon, roll.grantId, null) : null, parameters, false,
                (signal, value) => { if (ctx != null && ctx.LifeGeneration == life && Enum.TryParse(signal, out WeaponAffixProcEvent kind)) PublishFeedback(definition.affixId, kind, value); })));
    }
    public static void NormalizeGrantIds(WeaponInstanceData weapon)
    {
        if (weapon == null) return;
        var seen = new HashSet<string>();
        var claimedLegacy = new HashSet<string>();
        Normalize(weapon.mainAffix, "main");
        if (weapon.subAffixes != null) for (int i = 0; i < weapon.subAffixes.Count; i++) Normalize(weapon.subAffixes[i], "sub:" + i);
        void Normalize(RolledAffixData roll, string slot)
        {
            if (roll == null || roll.IsEmpty) return;
            if (string.IsNullOrWhiteSpace(roll.grantId)) roll.grantId = slot + ":" + roll.affixId;
            if (!seen.Add(roll.grantId)) { roll.grantId = Guid.NewGuid().ToString("N"); seen.Add(roll.grantId); }
            if (!claimedLegacy.Add(roll.affixId) || weapon.affixRuntimeStates == null) return;
            bool hasNew = weapon.affixRuntimeStates.Exists(s => s != null && s.affixId == roll.grantId);
            if (!hasNew) foreach (var state in weapon.affixRuntimeStates)
                if (state != null && state.affixId == roll.affixId) { state.affixId = roll.grantId; break; }
        }
    }
}
