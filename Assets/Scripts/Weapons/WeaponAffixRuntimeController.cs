using System.Collections.Generic;
using UnityEngine;

public class WeaponAffixRuntimeController : MonoBehaviour, IStatModifierProvider, IWeaponRuntimeEffectHandler, IWeaponAffixPreDamageRuntime
{
    [Header("Refs")]
    [SerializeField] private WeaponSystem weaponSystem;
    [SerializeField] private StatsHub statsHub;
    [SerializeField] private StatusEffectController statusEffectController;
    [SerializeField] private WeaponAffixDatabase affixDatabase;

    readonly List<IWeaponAffixRuntime> runtimes = new();
    readonly List<bool> runtimeDisabled = new();
    CombatEventBus subscribedEventBus;
    CharacteContext ctx;
    bool hasTimers;

    public IWeaponAffixPreDamageRuntime PreDamageRuntime => runtimes.Count > 0 ? this : null;

    public event System.Action StatModifiersChanged;
    public event System.Action<string, WeaponAffixProcEvent, int> FeedbackPublished;

    void Awake()
    {
        TryGetComponent(out ctx);
        ctx?.ResolveReferences();
        if (!weaponSystem) TryGetComponent(out weaponSystem);
        if (!statsHub) statsHub = ctx != null ? ctx.StatsHub : null;
        if (!statsHub) TryGetComponent(out statsHub);
        if (!statusEffectController) TryGetComponent(out statusEffectController);
    }

    void OnEnable()
    {
        SubscribeCombatEvents();
        statsHub?.RebuildModifierProviders();
        NotifyStatModifiersChanged();
    }

    void OnDisable()
    {
        UnsubscribeCombatEvents();
        DisposeRuntimes();
        statsHub?.RebuildModifierProviders();
        NotifyStatModifiersChanged();
    }

    public void NotifyWeaponEquipped()
    {
        BuildRuntimes();
        NotifyStatModifiersChanged();
    }

    void Update()
    {
        if (!hasTimers) return;
        hasTimers = false;
        for (int i = 0; i < runtimes.Count; i++)
        {
            if (runtimeDisabled[i] || runtimes[i] is not IWeaponAffixTimerRuntime timer) continue;
            try { hasTimers |= timer.Tick(Time.deltaTime); }
            catch (System.Exception exception) { DisableRuntime(i, exception, "timer"); }
        }
    }

    public void ModifyShot(ref WeaponShotBuildContext context)
    {
        for (int i = 0; i < runtimes.Count; i++)
        {
            if (runtimeDisabled[i] || runtimes[i] is not IWeaponAffixPreShotRuntime hook) continue;
            try { hook.ModifyShot(ref context); }
            catch (System.Exception exception) { DisableRuntime(i, exception, "pre-shot"); }
        }
    }

    public void ModifyDamage(ref WeaponDamageBuildContext context)
    {
        if (weaponSystem == null || weaponSystem.CurrentWeaponInstance == null ||
            context.WeaponInstanceId != weaponSystem.CurrentWeaponInstance.instanceId) return;
        for (int i = 0; i < runtimes.Count; i++)
        {
            if (runtimeDisabled[i] || runtimes[i] is not IWeaponAffixPreDamageRuntime hook) continue;
            try { hook.ModifyDamage(ref context); }
            catch (System.Exception exception) { DisableRuntime(i, exception, "pre-damage"); }
        }
    }

    public void HandleShotFired()
    {
    }

    public void HandleReloadCompleted()
    {
    }

    void NotifyStatModifiersChanged()
    {
        var handler = StatModifiersChanged;
        if (handler != null)
            handler.Invoke();
        else
            statsHub?.MarkDirty();
    }

    public void AppendStatModifiers(List<RuntimeStatModifier> buffer)
    {
        if (buffer == null || affixDatabase == null || weaponSystem == null)
            return;

        var instance = weaponSystem.CurrentWeaponInstance;
        if (instance == null)
            return;

        for (int i = 0; i < runtimes.Count; i++)
        {
            if (runtimeDisabled[i] || runtimes[i] is not IWeaponAffixStatRuntime statRuntime) continue;
            try { statRuntime.AppendStatModifiers(buffer); }
            catch (System.Exception exception) { DisableRuntime(i, exception, "stats"); }
        }

    }

    bool TryResolveAffix(RolledAffixData rolledAffix, out WeaponAffixDefinition definition)
    {
        definition = null;

        if (rolledAffix == null || string.IsNullOrWhiteSpace(rolledAffix.affixId) || affixDatabase == null)
            return false;

        definition = affixDatabase.GetById(rolledAffix.affixId);
        return definition != null;
    }

    void SubscribeCombatEvents()
    {
        CombatEventBus bus = ctx != null ? ctx.CombatEventBus : null;
        if (subscribedEventBus == bus) return;
        UnsubscribeCombatEvents();
        subscribedEventBus = bus;
        if (subscribedEventBus != null) subscribedEventBus.EventPublished += OnCombatEvent;
    }

    void UnsubscribeCombatEvents()
    {
        if (subscribedEventBus != null) subscribedEventBus.EventPublished -= OnCombatEvent;
        subscribedEventBus = null;
    }

    void OnCombatEvent(PassiveEventContext context)
    {
        var instance = weaponSystem != null ? weaponSystem.CurrentWeaponInstance : null;
        if (instance == null || context.Metadata.WeaponInstanceId != instance.instanceId ||
            context.Metadata.IsWeaponAffixGenerated) return;
        for (int i = 0; i < runtimes.Count; i++)
        {
            if (runtimeDisabled[i] || runtimes[i] is not IWeaponAffixCombatEventRuntime hook) continue;
            try { hook.OnCombatEvent(in context); if (runtimes[i] is IWeaponAffixTimerRuntime) hasTimers = true; }
            catch (System.Exception exception) { DisableRuntime(i, exception, "combat-event"); }
        }
    }

    void BuildRuntimes()
    {
        DisposeRuntimes();
        SubscribeCombatEvents();
        var instance = weaponSystem != null ? weaponSystem.CurrentWeaponInstance : null;
        if (instance == null || affixDatabase == null) return;
        AddRuntime(instance, instance.mainAffix);
        if (instance.subAffixes != null)
            for (int i = 0; i < instance.subAffixes.Count; i++) AddRuntime(instance, instance.subAffixes[i]);
        hasTimers = true;
    }

    void AddRuntime(WeaponInstanceData instance, RolledAffixData roll)
    {
        if (!TryResolveAffix(roll, out var definition) || definition.rootBehavior == null) return;
        var runtimeContext = new WeaponAffixRuntimeContext
        {
            Character = ctx, WeaponSystem = weaponSystem, StatsHub = statsHub,
            CombatEventBus = subscribedEventBus, WeaponInstance = instance, Definition = definition,
            Roll = roll, PersistentState = instance.GetOrCreateAffixState(definition.affixId),
            PublishFeedback = PublishFeedback
        };
        try
        {
            var runtime = definition.rootBehavior.CreateRuntime(runtimeContext);
            if (runtime == null) return;
            runtimes.Add(runtime);
            runtimeDisabled.Add(false);
            runtime.OnEquip();
        }
        catch (System.Exception exception)
        {
            Debug.LogException(new System.InvalidOperationException($"Affix '{definition.affixId}' failed during equip.", exception), this);
        }
    }

    void PublishFeedback(string affixId, WeaponAffixProcEvent procEvent, int value)
    {
        FeedbackPublished?.Invoke(affixId, procEvent, value);
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        Debug.Log($"[WeaponAffix] {affixId}: {procEvent} ({value})", this);
#endif
    }

    void DisposeRuntimes()
    {
        for (int i = 0; i < runtimes.Count; i++)
        {
            try { runtimes[i]?.OnUnequip(); runtimes[i]?.Dispose(); }
            catch (System.Exception exception) { Debug.LogException(exception, this); }
        }
        runtimes.Clear();
        runtimeDisabled.Clear();
        hasTimers = false;
    }

    void DisableRuntime(int index, System.Exception exception, string phase)
    {
        runtimeDisabled[index] = true;
        Debug.LogException(new System.InvalidOperationException(
            $"Affix '{runtimes[index]?.AffixId}' disabled after {phase} failure on weapon '{weaponSystem?.CurrentWeaponInstance?.instanceId}'.", exception), this);
    }
}
