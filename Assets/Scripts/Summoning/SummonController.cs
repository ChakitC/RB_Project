using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public sealed class SummonController : MonoBehaviour
{
    public const int TotalActiveCap = 12;

    [SerializeField] private CharacteContext caster;
    [SerializeField] private MapRunController map;

    readonly List<SummonedEntityRuntime> summons = new();
    ulong nextSpawnSequence;
    bool initialized;
    bool warnedMissingOwnerTargetInfo;

    public CharacteContext Caster => caster;
    public MapRunController Map => map;
    public IReadOnlyList<SummonedEntityRuntime> Summons => summons;

    public static bool TryEnsure(
        CharacteContext caster,
        MapRunController map,
        out SummonController controller)
    {
        controller = null;
        if (caster == null || map == null || !map.isActiveAndEnabled)
            return false;

        controller = caster.GetComponent<SummonController>();
        if (controller == null)
            controller = caster.gameObject.AddComponent<SummonController>();

        return controller.Initialize(caster, map);
    }

    public bool Initialize(CharacteContext owner, MapRunController activeMap)
    {
        if (owner == null || activeMap == null || !activeMap.isActiveAndEnabled)
            return false;

        if (initialized && caster == owner && map == activeMap)
            return true;

        Unsubscribe();
        caster = owner;
        map = activeMap;
        initialized = true;
        caster.ResolveReferences();

        map.RoomTransitionCommitted += HandleRoomTransitionCommitted;
        map.RoomTransitionRolledBack += HandleRoomTransitionRolledBack;
        if (caster.HealthSystem != null)
            caster.HealthSystem.CharacterDead += HandleOwnerDead;

        return true;
    }

    public bool TrySpawn(SummonSpawnContext request, out SummonedEntityRuntime runtime)
    {
        runtime = null;
        if (!initialized || map == null || !map.isActiveAndEnabled || request == null ||
            request.Caster != caster || request.Prefab == null || string.IsNullOrWhiteSpace(request.SkillId))
            return false;

        if (caster.TargetInfo == null)
        {
            WarnMissingOwnerTargetInfo();
            return false;
        }

        if (request.Caster.TargetIdentity != AITargetIdentity.Player &&
            request.Caster.TargetIdentity != AITargetIdentity.Companion)
            return false;

        Transform stagingRoot = map.GetOrCreateSummonStagingRoot();
        Transform worldRoot = map.GetOrCreateSummonWorldRoot();
        GameObject instance = Object.Instantiate(
            request.Prefab,
            request.Position,
            request.Rotation,
            stagingRoot);
        if (instance == null)
            return false;

        instance.SetActive(false);
        if (!TryValidatePrefab(instance, request.Mobility, out string validationError))
        {
            Debug.LogWarning(
                $"[SummonController] Summon prefab '{request.Prefab.name}' failed validation: {validationError}",
                request.Prefab);
            Object.Destroy(instance);
            return false;
        }

        EvictOldestForSkill(request.SkillId, request.PerSkillCap);
        if (CountActive() >= TotalActiveCap)
            EvictOldest(null);

        instance.transform.SetParent(worldRoot, true);
        SummonContext summonContext = instance.GetComponentInChildren<SummonContext>(true);
        runtime = instance.GetComponentInChildren<SummonedEntityRuntime>(true);
        if (summonContext == null || runtime == null)
        {
            Object.Destroy(instance);
            runtime = null;
            return false;
        }

        request.SpawnSequence = NextSpawnSequence();
        runtime.BindContext(summonContext);
        runtime.Initialize(request, this);
        instance.SetActive(true);
        summons.Add(runtime);
        return true;
    }

    public int CountActive()
    {
        int count = 0;
        for (int i = summons.Count - 1; i >= 0; i--)
        {
            SummonedEntityRuntime summon = summons[i];
            if (summon == null || summon.LifecycleState == SummonLifecycleState.Destroyed)
            {
                summons.RemoveAt(i);
                continue;
            }

            if (summon.IsActive)
                count++;
        }

        return count;
    }

    public int CountActiveForSkill(string skillId)
    {
        if (string.IsNullOrWhiteSpace(skillId))
            return 0;

        int count = 0;
        for (int i = summons.Count - 1; i >= 0; i--)
        {
            SummonedEntityRuntime summon = summons[i];
            if (summon == null || summon.LifecycleState == SummonLifecycleState.Destroyed)
            {
                summons.RemoveAt(i);
                continue;
            }

            if (summon.IsActive && string.Equals(summon.SkillId, skillId, System.StringComparison.Ordinal))
                count++;
        }

        return count;
    }

    void EvictOldestForSkill(string skillId, int perSkillCap)
    {
        if (perSkillCap <= 0)
            return;

        while (CountActiveForSkill(skillId) >= perSkillCap)
        {
            if (!EvictOldest(skillId))
                return;
        }
    }

    bool EvictOldest(string skillId)
    {
        SummonedEntityRuntime oldest = null;
        for (int i = 0; i < summons.Count; i++)
        {
            SummonedEntityRuntime candidate = summons[i];
            if (candidate == null || !candidate.IsActive)
                continue;
            if (skillId != null && !string.Equals(candidate.SkillId, skillId, System.StringComparison.Ordinal))
                continue;
            if (oldest == null || candidate.SpawnSequence < oldest.SpawnSequence)
                oldest = candidate;
        }

        if (oldest == null)
            return false;

        oldest.BeginDespawn(SummonDespawnReason.CapEvicted);
        return true;
    }

    ulong NextSpawnSequence()
    {
        nextSpawnSequence++;
        if (nextSpawnSequence == 0)
            nextSpawnSequence = 1;
        return nextSpawnSequence;
    }

    static bool TryValidatePrefab(GameObject prefab, SummonMobility mobility, out string error)
    {
        error = string.Empty;
        SummonContext context = prefab.GetComponentInChildren<SummonContext>(true);
        if (context == null)
        {
            error = "Summon prefab is missing SummonContext.";
            return false;
        }

        if (context.transform != prefab.transform)
        {
            error = "SummonContext must be on the summon prefab root.";
            return false;
        }

        SummonedEntityRuntime runtime = prefab.GetComponentInChildren<SummonedEntityRuntime>(true);
        if (runtime == null)
        {
            error = "Summon prefab is missing SummonedEntityRuntime.";
            return false;
        }

        if (runtime.GameplayRoot == null || runtime.PresentationRoot == null)
        {
            error = "SummonedEntityRuntime requires separate Gameplay Root and Presentation Root references.";
            return false;
        }

        if (!IsSameOrChild(runtime.GameplayRoot.transform, prefab.transform) ||
            !IsSameOrChild(runtime.PresentationRoot, prefab.transform) ||
            runtime.PresentationRoot == runtime.GameplayRoot.transform ||
            runtime.PresentationRoot.IsChildOf(runtime.GameplayRoot.transform) ||
            runtime.GameplayRoot.transform.IsChildOf(runtime.PresentationRoot))
        {
            error = "Summon gameplay and presentation roots must be separate descendants of the prefab root.";
            return false;
        }

        if (prefab.GetComponentInChildren<SummonHealthSystem>(true) == null ||
            prefab.GetComponentInChildren<StateHub>(true) == null ||
            prefab.GetComponentInChildren<StatsHub>(true) == null ||
            prefab.GetComponentInChildren<AITargetInfo>(true) == null ||
            prefab.GetComponentInChildren<CombatEventBus>(true) == null)
        {
            error = "Summon prefab is missing one or more required gameplay components.";
            return false;
        }

        if (mobility == SummonMobility.Mobile &&
            (prefab.GetComponentInChildren<NavMeshAgent>(true) == null ||
             prefab.GetComponentInChildren<AgentMoveDriver>(true) == null))
        {
            error = "Mobile summon requires NavMeshAgent and AgentMoveDriver.";
            return false;
        }

        if (!CharacterPlacementProbeUtility.TryGetFootprint(prefab, mobility, out _, out error))
            return false;

        return true;
    }

    static bool IsSameOrChild(Transform candidate, Transform root)
    {
        return candidate != null && root != null &&
               (candidate == root || candidate.IsChildOf(root));
    }

    void HandleRoomTransitionCommitted()
    {
        if (map == null || caster == null)
            return;

        int formationIndex = 0;
        for (int i = summons.Count - 1; i >= 0; i--)
        {
            SummonedEntityRuntime summon = summons[i];
            if (summon == null || !summon.IsActive)
                continue;

            if (summon.Mobility == SummonMobility.Stationary)
            {
                summon.BeginDespawn(SummonDespawnReason.RoomTransition);
                continue;
            }

            if (summon.SummonContext == null ||
                !map.TryWarpTransientActor(summon.SummonContext, caster, formationIndex, out _))
            {
                summon.BeginDespawn(SummonDespawnReason.RoomTransition);
                continue;
            }

            formationIndex++;
        }
    }

    void HandleRoomTransitionRolledBack()
    {
        // A rollback deliberately leaves summons untouched.
    }

    void HandleOwnerDead()
    {
        DespawnAll(SummonDespawnReason.OwnerDead, forceDestroy: false);
    }

    public void DespawnAll(SummonDespawnReason reason, bool forceDestroy)
    {
        for (int i = summons.Count - 1; i >= 0; i--)
        {
            SummonedEntityRuntime summon = summons[i];
            if (summon == null)
                continue;

            summon.BeginDespawn(reason);
            if (forceDestroy)
                summon.ForceDestroy(reason);
        }
    }

    internal void NotifySummonDestroyed(SummonedEntityRuntime summon)
    {
        if (summon != null)
            summons.Remove(summon);
    }

    void WarnMissingOwnerTargetInfo()
    {
        if (warnedMissingOwnerTargetInfo)
            return;

        warnedMissingOwnerTargetInfo = true;
        Debug.LogWarning(
            $"[SummonController] Caster '{caster.name}' has no AITargetInfo. Summon was rejected.",
            caster);
    }

    void OnDestroy()
    {
        DespawnAll(SummonDespawnReason.ControllerDestroyed, forceDestroy: true);
        Unsubscribe();
        initialized = false;
    }

    void Unsubscribe()
    {
        if (map != null)
        {
            map.RoomTransitionCommitted -= HandleRoomTransitionCommitted;
            map.RoomTransitionRolledBack -= HandleRoomTransitionRolledBack;
        }

        if (caster != null && caster.HealthSystem != null)
            caster.HealthSystem.CharacterDead -= HandleOwnerDead;
    }
}
