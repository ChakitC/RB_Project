using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Deploys one or more projectile-absorbing barriers. Reusable across skills: what a barrier
/// attaches to is authored through <see cref="BarrierAnchorMode"/>, and its size, lifetime, and
/// HP all come from the cast's resolved stats rather than being hard-coded to any one skill.
/// </summary>
[HideMonoScript]
public sealed class BarrierSkillPayloadDef : SkillPayloadDef
{
    [PropertyOrder(-20)]
    [SerializeField, BoxGroup("Barrier"), AssetsOnly]
    [Tooltip("Prefab whose root carries BarrierRuntime. Must live on the Barrier physics layer.")]
    private GameObject barrierPrefab;

    [SerializeField, BoxGroup("Barrier")]
    private BarrierAnchorMode anchorMode = BarrierAnchorMode.Caster;

    [SerializeField, BoxGroup("Radius"), ToggleLeft]
    [Tooltip("Take the barrier radius from the skill's Area Of Effect radius.")]
    private bool useSkillAreaRadius = true;

    [SerializeField, BoxGroup("Radius"), MinValue(0.1f)]
    [Tooltip("Radius used when Use Skill Area Radius is off, or when the skill resolves to 0.")]
    private float fixedRadius = 3f;

    [SerializeField, BoxGroup("Lifetime"), ToggleLeft]
    [Tooltip("Take the barrier lifetime from the skill's Effect Duration, so it matches what it protects.")]
    private bool useSkillEffectDuration = true;

    [SerializeField, BoxGroup("Lifetime"), MinValue(0.1f)]
    private float fixedLifetime = 10f;

    [SerializeField, BoxGroup("Health"), MinValue(0f)]
    [Tooltip("Flat barrier HP before the anchor share is added.")]
    private float baseHealth;

    [SerializeField, BoxGroup("Health"), MinValue(0f)]
    [Tooltip("Share of the anchor's max HP granted as barrier HP. 0.75 = 75% of the anchor's max HP.")]
    private float anchorMaxHealthShare = 0.75f;

    public GameObject BarrierPrefab => barrierPrefab;
    public BarrierAnchorMode AnchorMode => anchorMode;
    public bool UseSkillAreaRadius => useSkillAreaRadius;
    public float FixedRadius => Mathf.Max(0.1f, fixedRadius);
    public bool UseSkillEffectDuration => useSkillEffectDuration;
    public float FixedLifetime => Mathf.Max(0.1f, fixedLifetime);
    public float BaseHealth => Mathf.Max(0f, baseHealth);
    public float AnchorMaxHealthShare => Mathf.Max(0f, anchorMaxHealthShare);

    public override void CollectValidationIssues(List<string> issues)
    {
        if (issues == null)
            return;

        if (barrierPrefab == null)
        {
            issues.Add("Barrier payload has no barrier prefab configured.");
        }
        else
        {
            if (barrierPrefab.GetComponent<BarrierRuntime>() == null)
                issues.Add("Barrier prefab must have BarrierRuntime on its root.");

            if (barrierPrefab.GetComponentInChildren<SphereCollider>(true) == null)
                issues.Add("Barrier prefab needs a SphereCollider for projectiles to hit.");
        }

        if (baseHealth <= 0f && anchorMaxHealthShare <= 0f)
            issues.Add("Barrier resolves to zero HP. Set Base Health or Anchor Max Health Share.");

        if (!useSkillAreaRadius && fixedRadius <= 0f)
            issues.Add("Barrier radius must be greater than zero.");

        if (!useSkillEffectDuration && fixedLifetime <= 0f)
            issues.Add("Barrier lifetime must be greater than zero.");
    }

    public override SkillExecutionResult ExecuteWithResult(SkillCastContext context)
    {
        if (context == null || context.CasterRoot == null)
        {
            return SkillExecutionResult.Failed(
                SkillExecutionFailureReason.MissingRuntimeContext,
                "Barrier payload executed without a caster root.");
        }

        if (barrierPrefab == null)
        {
            return SkillExecutionResult.Failed(
                SkillExecutionFailureReason.MissingAuthoringData,
                "Barrier payload has no barrier prefab configured.");
        }

        CharacteContext caster = context.CasterRoot.GetComponent<CharacteContext>();
        if (caster == null)
        {
            return SkillExecutionResult.Failed(
                SkillExecutionFailureReason.MissingRuntimeContext,
                "Barrier payload could not resolve the caster context.");
        }

        caster.ResolveReferences();

        float radius = ResolveRadius(context);
        float lifetime = ResolveLifetime(context);
        if (radius <= 0f || lifetime <= 0f)
        {
            return SkillExecutionResult.Failed(
                SkillExecutionFailureReason.MissingAuthoringData,
                $"Barrier resolved to radius {radius} / lifetime {lifetime}.");
        }

        var requests = new List<BarrierSpawnRequest>(1);
        if (!TryBuildRequests(context, caster, radius, lifetime, requests, out SkillExecutionResult failure))
            return failure;

        int created = 0;
        for (int i = 0; i < requests.Count; i++)
        {
            if (TrySpawnBarrier(requests[i]))
                created++;
        }

        if (created > 0)
            return SkillExecutionResult.Succeeded;

        return SkillExecutionResult.Failed(
            SkillExecutionFailureReason.NoEffect,
            "No barrier could be created for the resolved anchors.");
    }

    bool TryBuildRequests(
        SkillCastContext context,
        CharacteContext caster,
        float radius,
        float lifetime,
        List<BarrierSpawnRequest> requests,
        out SkillExecutionResult failure)
    {
        failure = SkillExecutionResult.Succeeded;

        switch (anchorMode)
        {
            case BarrierAnchorMode.SpawnedEntitiesFromCurrentCast:
            {
                IReadOnlyList<SummonedEntityRuntime> spawned =
                    context.ExecutionState != null
                        ? context.ExecutionState.SpawnedSummons
                        : null;

                if (spawned == null || spawned.Count == 0)
                {
                    failure = SkillExecutionResult.Failed(
                        SkillExecutionFailureReason.NoEffect,
                        "Barrier anchors on this cast's spawned entities, but the cast spawned none.");
                    return false;
                }

                for (int i = 0; i < spawned.Count; i++)
                {
                    SummonedEntityRuntime summon = spawned[i];
                    if (summon == null || !summon.IsActive)
                        continue;

                    Transform anchor = summon.SummonContext != null
                        ? summon.SummonContext.transform
                        : summon.transform;

                    requests.Add(new BarrierSpawnRequest
                    {
                        Owner = caster,
                        AnchorMode = anchorMode,
                        Anchor = anchor,
                        AnchorSummon = summon,
                        FallbackPosition = anchor.position,
                        Radius = radius,
                        Lifetime = lifetime,
                        MaxHealth = ResolveHealth(ResolveAnchorMaxHealth(summon)),
                    });
                }

                if (requests.Count == 0)
                {
                    failure = SkillExecutionResult.Failed(
                        SkillExecutionFailureReason.NoEffect,
                        "Every entity spawned by this cast was already inactive.");
                    return false;
                }

                return true;
            }

            case BarrierAnchorMode.CastPosition:
            {
                Vector3 position = context.CastOrigin != null
                    ? context.CastOrigin.position
                    : caster.transform.position;

                requests.Add(new BarrierSpawnRequest
                {
                    Owner = caster,
                    AnchorMode = BarrierAnchorMode.CastPosition,
                    Anchor = null,
                    FallbackPosition = position,
                    Radius = radius,
                    Lifetime = lifetime,
                    MaxHealth = ResolveHealth(ResolveCasterMaxHealth(caster)),
                });
                return true;
            }

            default:
            {
                requests.Add(new BarrierSpawnRequest
                {
                    Owner = caster,
                    AnchorMode = BarrierAnchorMode.Caster,
                    Anchor = caster.transform,
                    FallbackPosition = caster.transform.position,
                    Radius = radius,
                    Lifetime = lifetime,
                    MaxHealth = ResolveHealth(ResolveCasterMaxHealth(caster)),
                });
                return true;
            }
        }
    }

    bool TrySpawnBarrier(BarrierSpawnRequest request)
    {
        if (request == null || request.MaxHealth <= 0f)
            return false;

        GameObject instance = Object.Instantiate(
            barrierPrefab,
            request.Anchor != null ? request.Anchor.position : request.FallbackPosition,
            Quaternion.identity);

        if (instance == null)
            return false;

        BarrierRuntime runtime = instance.GetComponent<BarrierRuntime>();
        if (runtime == null || !runtime.Initialize(request))
        {
            Object.Destroy(instance);
            return false;
        }

        return true;
    }

    float ResolveRadius(SkillCastContext context)
    {
        if (useSkillAreaRadius && context.SkillStats != null && context.SkillStats.areaRadius > 0f)
            return context.SkillStats.areaRadius;

        return FixedRadius;
    }

    float ResolveLifetime(SkillCastContext context)
    {
        if (useSkillEffectDuration && context.SkillStats != null && context.SkillStats.effectDuration > 0f)
            return context.SkillStats.effectDuration;

        return FixedLifetime;
    }

    float ResolveHealth(float anchorMaxHealth)
    {
        return Mathf.Max(0f, BaseHealth + Mathf.Max(0f, anchorMaxHealth) * AnchorMaxHealthShare);
    }

    static float ResolveAnchorMaxHealth(SummonedEntityRuntime summon)
    {
        if (summon == null)
            return 0f;

        // Prefer the summon's live hub, which already includes the snapshot the summon spawned with.
        CharacteContext context = summon.SummonContext;
        if (context != null)
        {
            context.ResolveReferences();
            if (context.StatsHub != null)
                return Mathf.Max(0f, context.StatsHub.GetMaximumHealth());
        }

        return summon.MaxHealth;
    }

    static float ResolveCasterMaxHealth(CharacteContext caster)
    {
        if (caster == null || caster.StatsHub == null)
            return 0f;

        return Mathf.Max(0f, caster.StatsHub.GetMaximumHealth());
    }
}
