using UnityEngine;

public readonly struct CombatAttributionSnapshot
{
    public CombatAttributionSnapshot(
        GameObject physicalActor,
        GameObject creditedActor,
        CombatEventBus creditedEventBus,
        StatusEffectController creditedStatusOwner)
    {
        PhysicalActor = physicalActor;
        CreditedActor = creditedActor;
        CreditedEventBus = creditedEventBus;
        CreditedStatusOwner = creditedStatusOwner;
    }

    public GameObject PhysicalActor { get; }
    public GameObject CreditedActor { get; }
    public CombatEventBus CreditedEventBus { get; }
    public StatusEffectController CreditedStatusOwner { get; }
    public bool HasPhysicalActor => PhysicalActor != null;
    public bool HasCredit => CreditedActor != null || CreditedEventBus != null || CreditedStatusOwner != null;

    public static CombatAttributionSnapshot FromPhysicalActor(GameObject physicalActor)
    {
        if (physicalActor == null)
            return default;

        SummonContext summonContext = physicalActor.GetComponentInParent<SummonContext>();
        if (summonContext != null)
        {
            summonContext.ResolveReferences();
            if (summonContext.SummonedRuntime != null)
                return summonContext.SummonedRuntime.Attribution;
        }

        SummonedEntityRuntime summon = physicalActor.GetComponentInParent<SummonedEntityRuntime>();
        return summon != null
            ? summon.Attribution
            : new CombatAttributionSnapshot(physicalActor, null, null, null);
    }
}
