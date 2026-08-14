using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AI;

public enum SummonFacingMode
{
    CasterForward,
    AimDirection,
    LookAwayFromCaster,
    PrefabRotation,
}

public enum SummonLayoutOrientation
{
    CasterForward,
    AimDirection,
    WorldAxes,
}

[HideMonoScript]
public sealed class SummonSkillPayloadDef : SkillPayloadDef
{
    [PropertyOrder(-20)]
    [SerializeField, BoxGroup("Summon"), AssetsOnly]
    private GameObject summonPrefab;

    [SerializeField, BoxGroup("Summon")]
    private SummonMobility mobility = SummonMobility.Stationary;

    [SerializeField, BoxGroup("Summon"), MinValue(1)]
    private int fixedCount = 1;

    [SerializeField, BoxGroup("Summon"), ToggleLeft]
    private bool useSkillProjectileCount = true;

    [SerializeField, BoxGroup("Summon"), MinValue(0.01f)]
    private float fixedLifetime = 5f;

    [SerializeField, BoxGroup("Summon"), ToggleLeft]
    private bool useSkillEffectDuration = true;

    [SerializeField, BoxGroup("Summon"), MinValue(1)]
    private int perSkillCap = 1;

    [SerializeField, BoxGroup("Summon"), Range(0f, 1f)]
    private float damageInheritance = 1f;

    [SerializeField, BoxGroup("Summon"), MinValue(0f)]
    private float despawnDelay = 0.25f;

    [SerializeField, BoxGroup("Snapshot"), ToggleLeft]
    private bool inheritHealPower = true;

    [SerializeField, BoxGroup("Snapshot"), ToggleLeft]
    private bool inheritAreaRadius = true;

    [SerializeField, BoxGroup("Snapshot"), ToggleLeft]
    private bool inheritEffectDuration = true;

    [SerializeField, BoxGroup("Snapshot"), MinValue(0f)]
    private float fixedHealPower;

    [SerializeField, BoxGroup("Snapshot"), MinValue(0f)]
    private float fixedAreaRadius;

    [SerializeField, BoxGroup("Snapshot"), MinValue(0f)]
    private float fixedEffectDuration;

    [SerializeField, BoxGroup("Placement")]
    private List<Vector3> spawnOffsets = new() { Vector3.zero };

    [SerializeField, BoxGroup("Placement")]
    private SummonLayoutOrientation layoutOrientation = SummonLayoutOrientation.CasterForward;

    [SerializeField, BoxGroup("Placement")]
    private SummonFacingMode facingMode = SummonFacingMode.CasterForward;

    [SerializeField, BoxGroup("Placement"), ToggleLeft]
    private bool resolveGround = true;

    [SerializeField, BoxGroup("Placement"), ToggleLeft]
    private bool requireNavMesh;

    [SerializeField, BoxGroup("Placement")]
    private LayerMask groundMask = ~0;

    [SerializeField, BoxGroup("Placement")]
    private LayerMask clearanceMask = ~0;

    [SerializeField, BoxGroup("Placement"), MinValue(0.1f)]
    private float groundRaycastHeight = 2f;

    [SerializeField, BoxGroup("Placement"), MinValue(0.1f)]
    private float groundRaycastDistance = 8f;

    [SerializeField, BoxGroup("Placement"), MinValue(0.1f)]
    private float navMeshSampleRadius = 2f;

    [SerializeField, BoxGroup("Placement"), MinValue(0f)]
    private float placementPadding = 0.05f;

    public GameObject SummonPrefab => summonPrefab;
    public SummonMobility Mobility => mobility;
    public int FixedCount => Mathf.Max(1, fixedCount);
    public bool UseSkillProjectileCount => useSkillProjectileCount;
    public float FixedLifetime => fixedLifetime;
    public bool UseSkillEffectDuration => useSkillEffectDuration;
    public int PerSkillCap => Mathf.Max(1, perSkillCap);
    public float DamageInheritance => Mathf.Max(0f, damageInheritance);
    public float DespawnDelay => Mathf.Max(0f, despawnDelay);
    public IReadOnlyList<Vector3> SpawnOffsets => spawnOffsets;
    public SummonLayoutOrientation LayoutOrientation => layoutOrientation;
    public SummonFacingMode FacingMode => facingMode;
    public bool ResolveGround => resolveGround;
    public bool RequireNavMesh => requireNavMesh || mobility == SummonMobility.Mobile;
    public LayerMask GroundMask => groundMask;
    public LayerMask ClearanceMask => clearanceMask;
    public float GroundRaycastHeight => Mathf.Max(0.1f, groundRaycastHeight);
    public float GroundRaycastDistance => Mathf.Max(0.1f, groundRaycastDistance);
    public float NavMeshSampleRadius => Mathf.Max(0.1f, navMeshSampleRadius);
    public float PlacementPadding => Mathf.Max(0f, placementPadding);

    public override void CollectValidationIssues(List<string> issues)
    {
        if (issues == null)
            return;

        if (summonPrefab == null)
        {
            issues.Add("Summon payload has no summon prefab configured.");
            return;
        }

        SummonContext prefabContext = summonPrefab.GetComponentInChildren<SummonContext>(true);
        if (prefabContext == null)
            issues.Add("Summon prefab is missing SummonContext.");
        else if (prefabContext.transform != summonPrefab.transform)
            issues.Add("SummonContext must be on the summon prefab root.");

        SummonedEntityRuntime prefabRuntime = summonPrefab.GetComponentInChildren<SummonedEntityRuntime>(true);
        if (prefabRuntime == null)
            issues.Add("Summon prefab is missing SummonedEntityRuntime.");
        else
        {
            Transform prefabRoot = summonPrefab.transform;
            Transform gameplayRoot = prefabRuntime.GameplayRoot != null
                ? prefabRuntime.GameplayRoot.transform
                : null;
            Transform presentationRoot = prefabRuntime.PresentationRoot;
            if (!IsSameOrChild(gameplayRoot, prefabRoot) ||
                !IsSameOrChild(presentationRoot, prefabRoot) ||
                gameplayRoot == presentationRoot ||
                presentationRoot.IsChildOf(gameplayRoot) ||
                gameplayRoot.IsChildOf(presentationRoot))
            {
                issues.Add("SummonedEntityRuntime requires separate Gameplay Root and Presentation Root references under the prefab root.");
            }
        }
        if (summonPrefab.GetComponentInChildren<SummonHealthSystem>(true) == null)
            issues.Add("Summon prefab is missing SummonHealthSystem.");
        if (summonPrefab.GetComponentInChildren<StateHub>(true) == null)
            issues.Add("Summon prefab is missing StateHub.");
        if (summonPrefab.GetComponentInChildren<StatsHub>(true) == null)
            issues.Add("Summon prefab is missing StatsHub.");
        if (summonPrefab.GetComponentInChildren<AITargetInfo>(true) == null)
            issues.Add("Summon prefab is missing AITargetInfo.");
        if (summonPrefab.GetComponentInChildren<CombatEventBus>(true) == null)
            issues.Add("Summon prefab is missing CombatEventBus.");

        if (mobility == SummonMobility.Mobile)
        {
            if (summonPrefab.GetComponentInChildren<NavMeshAgent>(true) == null)
                issues.Add("Mobile summon requires NavMeshAgent.");
            if (summonPrefab.GetComponentInChildren<AgentMoveDriver>(true) == null)
                issues.Add("Mobile summon requires AgentMoveDriver.");
        }
        else
        {
            if (!CharacterPlacementProbeUtility.TryGetFootprint(summonPrefab, mobility, out _, out string error))
                issues.Add(error);
        }

        if (fixedLifetime <= 0f && !useSkillEffectDuration)
            issues.Add("Summon lifetime must be greater than zero.");
        if (useSkillEffectDuration && fixedLifetime <= 0f && fixedEffectDuration <= 0f)
            issues.Add("Summon lifetime must resolve to a value greater than zero.");
        if (spawnOffsets == null || spawnOffsets.Count == 0)
            issues.Add("Summon payload requires at least one spawn offset.");
        if (!float.IsFinite(damageInheritance) || damageInheritance < 0f)
            issues.Add("Damage inheritance must be finite and non-negative.");
    }

    public override void Execute(SkillCastContext context)
    {
        if (context == null || context.CasterRoot == null || summonPrefab == null)
            return;

        if (context.CasterRoot.GetComponent<SummonContext>() != null)
        {
            Debug.LogWarning(
                $"[SummonSkillPayloadDef] Recursive summon rejected for skill '{context.SkillDef?.SkillDefinitionId}'.",
                this);
            return;
        }

        CharacteContext caster = context.CasterRoot.GetComponent<CharacteContext>();
        if (caster == null ||
            (caster.TargetIdentity != AITargetIdentity.Player && caster.TargetIdentity != AITargetIdentity.Companion))
            return;

        caster.ResolveReferences();
        if (caster.TargetInfo == null)
        {
            Debug.LogWarning($"[SummonSkillPayloadDef] Caster '{caster.name}' has no AITargetInfo. Summon rejected.", this);
            return;
        }

        string skillId = context.SkillDef != null ? context.SkillDef.SkillDefinitionId : null;
        if (string.IsNullOrWhiteSpace(skillId))
        {
            Debug.LogError("[SummonSkillPayloadDef] Summon skill requires a non-empty Skill ID.", this);
            return;
        }

        MapRunController map = UnityEngine.Object.FindFirstObjectByType<MapRunController>();
        if (map == null || !map.isActiveAndEnabled)
            return;

        if (!SummonController.TryEnsure(caster, map, out SummonController controller))
            return;

        FinalSkillStats stats = context.SkillStats;
        int requestedCount = useSkillProjectileCount && stats != null
            ? Mathf.Max(1, stats.projectileCount)
            : FixedCount;
        requestedCount = Mathf.Min(requestedCount, PerSkillCap);

        float lifetime = useSkillEffectDuration && stats != null && stats.effectDuration > 0f
            ? stats.effectDuration
            : (fixedEffectDuration > 0f ? fixedEffectDuration : FixedLifetime);
        if (lifetime <= 0f)
            return;

        float healPower = inheritHealPower && stats != null ? Mathf.Max(0f, stats.healPower) : fixedHealPower;
        float areaRadius = inheritAreaRadius && stats != null ? Mathf.Max(0f, stats.areaRadius) : fixedAreaRadius;
        float effectDuration = inheritEffectDuration && stats != null
            ? Mathf.Max(0f, stats.effectDuration)
            : fixedEffectDuration;

        var settings = new SummonPlacementSettings
        {
            ResolveGround = resolveGround,
            RequireNavMesh = RequireNavMesh,
            GroundMask = groundMask,
            ClearanceMask = clearanceMask,
            GroundRaycastHeight = GroundRaycastHeight,
            GroundRaycastDistance = GroundRaycastDistance,
            NavMeshSampleRadius = NavMeshSampleRadius,
            Padding = PlacementPadding,
        };

        var reserved = new List<SummonPlacementCandidate>(requestedCount);
        Vector3 placementOrigin = context.CastOrigin != null ? context.CastOrigin.position : caster.transform.position;
        Quaternion layoutRotation = ResolveLayoutRotation(context, caster);
        for (int i = 0; i < requestedCount; i++)
        {
            Vector3 offset = ResolveOffset(i);
            Quaternion rotation = ResolveRotation(context, caster, layoutRotation, offset);
            var request = new SummonSpawnContext
            {
                Caster = caster,
                Map = map,
                Definition = this,
                Prefab = summonPrefab,
                SkillId = skillId,
                Mobility = mobility,
                Lifetime = lifetime,
                DespawnDelay = DespawnDelay,
                InheritedDamage = stats != null ? Mathf.Max(0f, stats.damage) * DamageInheritance : 0f,
                HealPower = healPower,
                AreaRadius = areaRadius,
                EffectDuration = effectDuration,
                Position = placementOrigin,
                Rotation = rotation,
                PerSkillCap = PerSkillCap,
            };

            if (!SummonPlacementResolver.TryResolve(
                    request,
                    offset,
                    layoutRotation,
                    rotation,
                    settings,
                    reserved,
                    out SummonPlacementCandidate candidate,
                    out _))
            {
                continue;
            }

            request.Position = candidate.Position;
            request.Rotation = candidate.Rotation;
            if (!controller.TrySpawn(request, out _))
                continue;

            reserved.Add(candidate);
        }
    }

    Vector3 ResolveOffset(int index)
    {
        if (spawnOffsets != null && index < spawnOffsets.Count)
            return spawnOffsets[index];

        int extraIndex = index - (spawnOffsets?.Count ?? 0);
        int layer = extraIndex / 8 + 1;
        int indexInLayer = extraIndex % 8;
        float angle = indexInLayer * Mathf.PI * 2f / 8f;
        float radius = 1.25f * layer;
        return new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
    }

    Quaternion ResolveRotation(
        SkillCastContext context,
        CharacteContext caster,
        Quaternion layoutRotation,
        Vector3 offset)
    {
        switch (facingMode)
        {
            case SummonFacingMode.AimDirection:
                return LookRotation(context.AimDirection, caster.transform.rotation);
            case SummonFacingMode.LookAwayFromCaster:
                Vector3 away = Vector3.ProjectOnPlane(layoutRotation * offset, Vector3.up);
                if (away.sqrMagnitude <= 0.0001f)
                    away = Vector3.ProjectOnPlane(context.AimDirection, Vector3.up);
                return LookRotation(away, caster.transform.rotation);
            case SummonFacingMode.PrefabRotation:
                return summonPrefab.transform.rotation;
            default:
                return caster.transform.rotation;
        }
    }

    Quaternion ResolveLayoutRotation(SkillCastContext context, CharacteContext caster)
    {
        switch (layoutOrientation)
        {
            case SummonLayoutOrientation.AimDirection:
                return LookRotation(context.AimDirection, caster.transform.rotation);
            case SummonLayoutOrientation.WorldAxes:
                return Quaternion.identity;
            default:
                return caster.transform.rotation;
        }
    }

    static Quaternion LookRotation(Vector3 direction, Quaternion fallback)
    {
        Vector3 planar = Vector3.ProjectOnPlane(direction, Vector3.up);
        return planar.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(planar.normalized, Vector3.up)
            : fallback;
    }

    static bool IsSameOrChild(Transform candidate, Transform root)
    {
        return candidate != null && root != null &&
               (candidate == root || candidate.IsChildOf(root));
    }
}
