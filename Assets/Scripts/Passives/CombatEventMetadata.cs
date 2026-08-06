using UnityEngine;

public enum CombatSourceKind
{
    None,
    Weapon,
    Melee,
    Skill,
    Status,
    WeaponAffix
}

public readonly struct CombatEventMetadata
{
    public CombatEventMetadata(
        float requestedDamage = 0f,
        float resolvedDamage = 0f,
        float appliedDamage = 0f,
        float healthBeforeHit = 0f,
        float maxHealth = 0f,
        bool wasCritical = false,
        CharacterHitZone hitZone = CharacterHitZone.None,
        float staggerApplied = 0f,
        bool enteredChainReady = false,
        string weaponInstanceId = null,
        int ammoBefore = 0,
        int ammoAfter = 0,
        int maxMagazine = 0,
        bool ammoConsumed = false,
        bool isLastRound = false,
        CombatSourceKind sourceKind = CombatSourceKind.None,
        string weaponAffixId = null)
    {
        RequestedDamage = Mathf.Max(0f, requestedDamage);
        ResolvedDamage = Mathf.Max(0f, resolvedDamage);
        AppliedDamage = Mathf.Max(0f, appliedDamage);
        HealthBeforeHit = Mathf.Max(0f, healthBeforeHit);
        MaxHealth = Mathf.Max(0f, maxHealth);
        WasCritical = wasCritical;
        HitZone = hitZone;
        StaggerApplied = Mathf.Max(0f, staggerApplied);
        EnteredChainReady = enteredChainReady;
        WeaponInstanceId = weaponInstanceId;
        AmmoBefore = Mathf.Max(0, ammoBefore);
        AmmoAfter = Mathf.Max(0, ammoAfter);
        MaxMagazine = Mathf.Max(0, maxMagazine);
        AmmoConsumed = ammoConsumed;
        IsLastRound = isLastRound;
        SourceKind = sourceKind;
        WeaponAffixId = weaponAffixId;
    }

    public float RequestedDamage { get; }
    public float ResolvedDamage { get; }
    public float AppliedDamage { get; }
    public float HealthBeforeHit { get; }
    public float MaxHealth { get; }
    public float OverkillAmount => Mathf.Max(0f, ResolvedDamage - HealthBeforeHit);
    public bool WasCritical { get; }
    public CharacterHitZone HitZone { get; }
    public float StaggerApplied { get; }
    public bool EnteredChainReady { get; }
    public string WeaponInstanceId { get; }
    public int AmmoBefore { get; }
    public int AmmoAfter { get; }
    public int MaxMagazine { get; }
    public bool AmmoConsumed { get; }
    public bool IsLastRound { get; }
    public CombatSourceKind SourceKind { get; }
    public string WeaponAffixId { get; }
    public bool IsWeaponAffixGenerated => !string.IsNullOrWhiteSpace(WeaponAffixId);
}
