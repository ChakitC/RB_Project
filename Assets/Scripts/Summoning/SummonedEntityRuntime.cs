using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SummonedEntityRuntime : MonoBehaviour, IStatModifierProvider
{
    [Header("Roots")]
    [SerializeField] private GameObject gameplayRoot;
    [SerializeField] private Transform presentationRoot;

    [Header("Runtime")]
    [SerializeField] private SummonContext summonContext;
    [SerializeField] private CharacteContext owner;
    [SerializeField] private string skillId;
    [SerializeField] private SummonMobility mobility;
    [SerializeField] private float remainingLifetime;
    [SerializeField] private float remainingDespawnDelay;
    [SerializeField] private ulong spawnSequence;
    [SerializeField] private float inheritedDamage;
    [SerializeField] private float healPower;
    [SerializeField] private float areaRadius;
    [SerializeField] private float effectDuration;
    [SerializeField] private float maxHealth;
    [SerializeField] private List<string> upgradeIds = new();

    SummonController controller;
    SummonDespawnReason despawnReason;
    SummonLifecycleState lifecycleState = SummonLifecycleState.Destroyed;
    StatsHub statsHub;
    AITargetInfo targetInfo;
    bool initialized;

    public event Action<SummonDespawnReason> DespawnRequested;
    public GameObject GameplayRoot => gameplayRoot != null ? gameplayRoot : gameObject;
    public Transform PresentationRoot => presentationRoot != null ? presentationRoot : transform;
    public SummonContext SummonContext => summonContext;
    public CharacteContext Owner => owner;
    public string SkillId => skillId;
    public SummonMobility Mobility => mobility;
    public float RemainingLifetime => remainingLifetime;
    public ulong SpawnSequence => spawnSequence;
    public float HealPower => healPower;
    public float AreaRadius => areaRadius;
    public float EffectDuration => effectDuration;

    /// <summary>Snapshotted max HP from the summoning payload, or 0 when the prefab keeps its own.</summary>
    public float MaxHealth => maxHealth;

    public CombatAttributionSnapshot Attribution { get; private set; }

    /// <summary>
    /// Upgrade IDs the owner held at cast time. Summon-side modules gate on these instead of
    /// reaching back into the owner's live Active Skill selection.
    /// </summary>
    public bool HasUpgrade(string id)
    {
        if (string.IsNullOrWhiteSpace(id) || upgradeIds == null)
            return false;

        string trimmed = id.Trim();
        for (int i = 0; i < upgradeIds.Count; i++)
        {
            if (string.Equals(upgradeIds[i], trimmed, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
    public SummonLifecycleState LifecycleState => lifecycleState;
    public bool IsActive => lifecycleState == SummonLifecycleState.Active;

    public event Action StatModifiersChanged;

    void Awake()
    {
        if (summonContext == null)
            summonContext = GetComponentInParent<SummonContext>();

        if (summonContext != null)
            summonContext.ResolveReferences();

        if (statsHub == null && summonContext != null)
            statsHub = summonContext.StatsHub;
        if (statsHub == null)
            statsHub = GetComponentInChildren<StatsHub>(true);

        targetInfo = summonContext != null ? summonContext.TargetInfo : GetComponentInChildren<AITargetInfo>(true);
    }

    internal void BindContext(SummonContext context)
    {
        if (context != null)
            summonContext = context;
    }

    public void Initialize(SummonSpawnContext spawnContext, SummonController owningController)
    {
        if (spawnContext == null || spawnContext.Caster == null)
            throw new ArgumentNullException(nameof(spawnContext));

        controller = owningController;
        owner = spawnContext.Caster;
        skillId = spawnContext.SkillId;
        mobility = spawnContext.Mobility;
        remainingLifetime = Mathf.Max(0f, spawnContext.Lifetime);
        remainingDespawnDelay = Mathf.Max(0f, spawnContext.DespawnDelay);
        spawnSequence = spawnContext.SpawnSequence;
        inheritedDamage = Mathf.Max(0f, spawnContext.InheritedDamage);
        healPower = Mathf.Max(0f, spawnContext.HealPower);
        areaRadius = Mathf.Max(0f, spawnContext.AreaRadius);
        effectDuration = Mathf.Max(0f, spawnContext.EffectDuration);
        maxHealth = Mathf.Max(0f, spawnContext.MaxHealth);
        CaptureUpgradeIds(spawnContext.UpgradeIds);
        lifecycleState = SummonLifecycleState.Active;
        initialized = true;

        if (summonContext == null)
            summonContext = GetComponentInParent<SummonContext>();
        summonContext?.SetMobility(mobility);
        summonContext?.ResolveReferences();

        if (statsHub == null && summonContext != null)
            statsHub = summonContext.StatsHub;
        targetInfo = summonContext != null ? summonContext.TargetInfo : targetInfo;

        owner.ResolveReferences();
        Attribution = new CombatAttributionSnapshot(
            gameObject,
            owner.gameObject,
            owner.CombatEventBus,
            owner.StatusEffects);

        if (targetInfo != null && owner.TargetInfo != null)
            targetInfo.SetRuntimeTeamId(owner.TargetInfo.TeamId);

        statsHub?.RebuildModifierProviders();
        statsHub?.MarkDirty();
        StatModifiersChanged?.Invoke();
    }

    void Update()
    {
        if (!initialized || lifecycleState == SummonLifecycleState.Destroyed)
            return;

        float worldDeltaTime = TimeSlowManager.Instance.WorldDeltaTime;
        if (lifecycleState == SummonLifecycleState.Active)
        {
            remainingLifetime -= worldDeltaTime;
            if (remainingLifetime <= 0f)
                BeginDespawn(SummonDespawnReason.Expired);
        }

        if (lifecycleState != SummonLifecycleState.Despawning)
            return;

        remainingDespawnDelay -= worldDeltaTime;
        if (remainingDespawnDelay <= 0f)
            ForceDestroy(despawnReason);
    }

    public bool BeginDespawn(SummonDespawnReason reason)
    {
        if (lifecycleState != SummonLifecycleState.Active)
            return false;

        lifecycleState = SummonLifecycleState.Despawning;
        despawnReason = reason;
        remainingDespawnDelay = Mathf.Max(0f, remainingDespawnDelay);

        if (targetInfo != null)
        {
            targetInfo.SetAlive(false);
            targetInfo.SetTargetable(false);
        }

        DisableGameplayRoot();
        DespawnRequested?.Invoke(reason);
        NotifyPresentationReceivers(reason);

        if (remainingDespawnDelay <= 0f)
            ForceDestroy(reason);

        return true;
    }

    public void ForceDestroy(SummonDespawnReason reason)
    {
        if (lifecycleState == SummonLifecycleState.Destroyed)
            return;

        lifecycleState = SummonLifecycleState.Destroyed;
        despawnReason = reason;
        controller?.NotifySummonDestroyed(this);

        if (Application.isPlaying)
            Destroy(gameObject);
        else
            DestroyImmediate(gameObject);
    }

    void DisableGameplayRoot()
    {
        Transform actorRoot = summonContext != null ? summonContext.transform : transform;
        if (actorRoot == null)
            return;

        MonoBehaviour[] behaviours = actorRoot.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null || behaviour == this || IsPresentationTransform(behaviour.transform))
                continue;

            behaviour.enabled = false;
        }

        Collider[] colliders = actorRoot.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (collider != null && !IsPresentationTransform(collider.transform))
                collider.enabled = false;
        }

        GameObject gameplay = GameplayRoot;
        if (gameplay != null && gameplay.transform != transform &&
            !transform.IsChildOf(gameplay.transform) &&
            !IsPresentationTransform(gameplay.transform))
        {
            gameplay.SetActive(false);
        }
    }

    bool IsPresentationTransform(Transform candidate)
    {
        return candidate != null && PresentationRoot != null &&
               (candidate == PresentationRoot || candidate.IsChildOf(PresentationRoot));
    }

    void NotifyPresentationReceivers(SummonDespawnReason reason)
    {
        if (PresentationRoot == null)
            return;

        MonoBehaviour[] receivers = PresentationRoot.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < receivers.Length; i++)
        {
            if (receivers[i] is ISummonLifecycleReceiver receiver)
                receiver.OnSummonDespawnRequested(reason);
        }
    }

    void OnDestroy()
    {
        lifecycleState = SummonLifecycleState.Destroyed;
        controller?.NotifySummonDestroyed(this);
    }

    void CaptureUpgradeIds(IReadOnlyList<string> ids)
    {
        upgradeIds ??= new List<string>();
        upgradeIds.Clear();

        if (ids == null)
            return;

        for (int i = 0; i < ids.Count; i++)
        {
            string id = ids[i];
            if (string.IsNullOrWhiteSpace(id))
                continue;

            string trimmed = id.Trim();
            if (!upgradeIds.Contains(trimmed))
                upgradeIds.Add(trimmed);
        }
    }

    public void AppendStatModifiers(List<RuntimeStatModifier> buffer)
    {
        if (buffer == null || !initialized || lifecycleState == SummonLifecycleState.Destroyed)
            return;

        if (!Mathf.Approximately(inheritedDamage, 0f))
        {
            buffer.Add(new RuntimeStatModifier(
                StatType.Damage,
                ModifierOp.Flat,
                inheritedDamage,
                $"summon:{GetInstanceID()}:damage"));
        }

        if (maxHealth > 0f)
        {
            buffer.Add(new RuntimeStatModifier(
                StatType.MaxHP,
                ModifierOp.Flat,
                maxHealth,
                $"summon:{GetInstanceID()}:maxhp"));
        }
    }
}
