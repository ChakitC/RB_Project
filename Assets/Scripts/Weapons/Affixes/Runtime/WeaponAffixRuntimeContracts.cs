using System;
using System.Collections.Generic;
using UnityEngine;

public enum WeaponAffixRollValueKind { Float, Integer }
public enum WeaponAffixProcEvent { Activated, StackChanged, ChargeReady, Consumed, Expired }
public interface IWeaponAffixRollSource { float Range(float minimum, float maximum); }

public sealed class UnityWeaponAffixRollSource : IWeaponAffixRollSource
{
    public static readonly UnityWeaponAffixRollSource Instance = new();
    UnityWeaponAffixRollSource() { }
    public float Range(float minimum, float maximum) => UnityEngine.Random.Range(minimum, maximum);
}

[Serializable]
public struct WeaponAffixRollSpec
{
    public WeaponAffixRollValueKind valueKind;
    public float minimum;
    public float maximum;
    [Min(0)] public int displayPrecision;
    public bool lowerIsBetter;

    public float Roll()
    {
        return Roll(UnityWeaponAffixRollSource.Instance);
    }

    public float Roll(IWeaponAffixRollSource source)
    {
        float value = Mathf.Approximately(minimum, maximum) ? minimum : source.Range(minimum, maximum);
        return valueKind == WeaponAffixRollValueKind.Integer ? Mathf.Round(value) : value;
    }

    public bool IsValid => float.IsFinite(minimum) && float.IsFinite(maximum) && maximum >= minimum;
}

[Serializable]
public struct WeaponAffixTooltipData
{
    public string trigger;
    public string effect;
    public string restriction;
    public float duration;
    public int cap;
}

public sealed class WeaponAffixRuntimeContext
{
    public CharacteContext Character { get; internal set; }
    public WeaponSystem WeaponSystem { get; internal set; }
    public StatsHub StatsHub { get; internal set; }
    public CombatEventBus CombatEventBus { get; internal set; }
    public WeaponInstanceData WeaponInstance { get; internal set; }
    public WeaponAffixDefinition Definition { get; internal set; }
    public RolledAffixData Roll { get; internal set; }
    public WeaponAffixRuntimeStateData PersistentState { get; internal set; }
    public Action<string, WeaponAffixProcEvent, int> PublishFeedback { get; internal set; }
    public string WeaponInstanceId => WeaponInstance != null ? WeaponInstance.instanceId : null;
}

[CreateAssetMenu(menuName = "Game/Weapons/Affixes/Feedback Profile", fileName = "WeaponAffixFeedback")]
public sealed class WeaponAffixFeedbackProfile : ScriptableObject
{
    public AudioCue activatedCue;
    public GameObject activatedVfx;
}

public interface IWeaponAffixRuntime : IDisposable { string AffixId { get; } void OnEquip(); void OnUnequip(); }
public interface IWeaponAffixStatRuntime { void AppendStatModifiers(List<RuntimeStatModifier> buffer); }
public interface IWeaponAffixPreShotRuntime { void ModifyShot(ref WeaponShotBuildContext context); }
public interface IWeaponAffixPreDamageRuntime { void ModifyDamage(ref WeaponDamageBuildContext context); }
public interface IWeaponAffixCombatEventRuntime { void OnCombatEvent(in PassiveEventContext context); }
public interface IWeaponAffixTimerRuntime { bool Tick(float deltaTime); }

public struct WeaponDamageBuildContext
{
    public string WeaponInstanceId;
    public GameObject Target;
    public string AttackId;
    public float Damage;
    public float StaggerPower;
}

public readonly struct WeaponAffixImpactPayload
{
    public WeaponAffixImpactPayload(string affixId, float radius, float damage)
    {
        AffixId = affixId;
        Radius = Mathf.Max(0f, radius);
        Damage = Mathf.Max(0f, damage);
    }

    public string AffixId { get; }
    public float Radius { get; }
    public float Damage { get; }
    public bool IsValid => !string.IsNullOrWhiteSpace(AffixId) && Radius > 0f && Damage > 0f;
}

public abstract class WeaponAffixBehavior : ScriptableObject
{
    [SerializeField] private WeaponAffixRollSpec roll;
    [SerializeField] private WeaponAffixTooltipData tooltip;

    public WeaponAffixRollSpec RollSpec => roll;
    public WeaponAffixTooltipData Tooltip => tooltip;
    public virtual bool IsEligible(GunConfig weapon) => weapon != null;
    public virtual float RollPrimaryValue() => roll.Roll();
    public virtual float RollPrimaryValue(IWeaponAffixRollSource source) => roll.Roll(source);
    public abstract IWeaponAffixRuntime CreateRuntime(WeaponAffixRuntimeContext context);
}

public abstract class WeaponAffixRuntimeBase : IWeaponAffixRuntime
{
    protected readonly WeaponAffixRuntimeContext Context;
    public string AffixId => Context.Definition != null ? Context.Definition.affixId : string.Empty;
    protected WeaponAffixRuntimeBase(WeaponAffixRuntimeContext context) { Context = context; }
    public virtual void OnEquip() { }
    public virtual void OnUnequip() { }
    public virtual void Dispose() { }
}
