using System;
using UnityEngine;

/// <summary>
/// Everything a Test Stage run tracks on top of the map itself: how far the stage has been cleared,
/// what level its enemies spawn at, how the run's XP budget is split, and the one-shot commit that
/// ends the run and returns to the Basement.
///
/// A config with no Stage Id is not a Test Stage, and every method here is inert for it.
/// </summary>
public sealed class StageRunProgressionService
{
    private readonly MonoBehaviour owner;
    private readonly Func<PartyRuntime> resolveParty;
    private readonly Action<string> log;

    private MapRunConfigSO runConfig;
    private int stageProgressCount;
    private int stageEnemyLevel = 1;
    private int regularXpRemaining;
    private int bossXpRemaining;
    private int regularEnemiesRemaining;
    private int bossEnemiesRemaining;
    private int completionXpReward;
    private bool bossCleared;
    private bool stageCompletionCommitted;
    private GameObject stageExitInstance;

    public StageRunProgressionService(MonoBehaviour owner, Func<PartyRuntime> resolveParty, Action<string> log)
    {
        this.owner = owner;
        this.resolveParty = resolveParty;
        this.log = log;
    }

    public int StageEnemyLevel => stageEnemyLevel;
    public bool IsTestStage => runConfig != null && runConfig.IsTestStage;
    public bool CanCompleteStageRun => IsTestStage && bossCleared && !stageCompletionCommitted;

    /// <summary>Resets the stage state for a freshly generated map and reloads the saved progress.</summary>
    public void BeginRun(MapRunConfigSO config, MapGraph graph)
    {
        runConfig = config;
        stageProgressCount = 0;
        stageEnemyLevel = 1;
        regularXpRemaining = 0;
        bossXpRemaining = 0;
        regularEnemiesRemaining = 0;
        bossEnemiesRemaining = 0;
        completionXpReward = 0;
        bossCleared = false;
        stageCompletionCommitted = false;
        stageExitInstance = null;

        if (!IsTestStage || graph == null)
            return;

        if (SaveManager.Instance != null)
        {
            int savedProgress = SaveManager.Instance.LoadStageProgress(runConfig.StageId, runConfig.LegacyStageIds);
            stageProgressCount = Mathf.Clamp(savedProgress, 0, runConfig.TargetRunCount);
        }

        stageEnemyLevel = runConfig.GetEnemyLevel(stageProgressCount);

        int regularSpawnCount = 0;
        int bossSpawnCount = 0;
        for (int i = 0; i < graph.Nodes.Count; i++)
        {
            EncounterDefinitionSO encounter = graph.Nodes[i]?.EncounterDefinition;
            if (encounter == null)
                continue;

            if (encounter.BossEncounter)
                bossSpawnCount += encounter.TotalSpawnCount;
            else
                regularSpawnCount += encounter.TotalSpawnCount;
        }

        int budget = runConfig.GetXpBudgetPerRun();
        int regularPool = Mathf.RoundToInt(budget * runConfig.RegularEnemyXpShare);
        int bossPool = Mathf.RoundToInt(budget * runConfig.BossXpShare);
        regularXpRemaining = regularPool;
        bossXpRemaining = bossPool;
        regularEnemiesRemaining = regularSpawnCount;
        bossEnemiesRemaining = bossSpawnCount;
        completionXpReward = Mathf.Max(0, budget - regularPool - bossPool);

        Log($"Stage '{runConfig.StageId}' progress={stageProgressCount}/{runConfig.TargetRunCount}, enemyLv={stageEnemyLevel}, XP budget={budget} (regular pool {regularPool}/{regularSpawnCount}, boss pool {bossPool}/{bossSpawnCount}, completion {completionXpReward}).");
    }

    public void ConfigureStageEnemy(MapRunController run, GameObject enemyObject, bool isBoss)
    {
        if (enemyObject == null || !IsTestStage)
            return;

        EnemyContext enemyContext = enemyObject.GetComponentInChildren<EnemyContext>(true);
        if (enemyContext == null)
        {
            Debug.LogWarning($"[MapRunController] Stage enemy '{enemyObject.name}' has no EnemyContext.", enemyObject);
            return;
        }

        EnemyLevelSystem enemyLevel = enemyContext.EnemyLevelSystem;
        if (enemyLevel == null)
            enemyLevel = enemyContext.GetComponent<EnemyLevelSystem>();
        if (enemyLevel == null)
            enemyLevel = enemyContext.gameObject.AddComponent<EnemyLevelSystem>();
        enemyContext.EnemyLevelSystem = enemyLevel;
        enemyLevel.SetLevel(stageEnemyLevel);

        EnemyHealth health = enemyContext.GetComponentInChildren<EnemyHealth>(true);
        if (health != null)
            health.ConfigureStageXp(run, AllocateEnemyXp(isBoss));
    }

    public void GrantStageEnemyXp(int amount)
    {
        if (!IsTestStage || amount <= 0)
            return;

        GrantXpToDeployedParty(amount);
    }

    /// <summary>Called when a room clears. The Boss room is what unlocks the Stage Exit portal.</summary>
    public void NotifyRoomCleared(MapRunController run, RoomController room, MapNode node)
    {
        if (!IsTestStage || node == null || node.Type != MapNodeType.Boss)
            return;

        bossCleared = true;
        SpawnStageExit(run, room);
    }

    /// <summary>
    /// Commits the completion only when both the save and the scene-load dependency are available.
    /// When either is missing nothing is granted, spent, or locked, so the Stage Exit stays usable.
    /// </summary>
    public bool TryCompleteStageRunAndReturn()
    {
        if (!CanCompleteStageRun)
            return false;

        SaveManager saveManager = SaveManager.Instance;
        if (saveManager == null)
        {
            Debug.LogError(
                "[MapRunController] SaveManager is missing; Stage Progress cannot be saved. " +
                "The Stage Exit stays usable.",
                owner);
            return false;
        }

        SceneLoaderSystem sceneLoader = SceneLoaderSystem.Instance;
        if (sceneLoader == null)
        {
            Debug.LogError(
                "[MapRunController] SceneLoaderSystem is missing; cannot return to Basement. " +
                "The Stage Exit stays usable.",
                owner);
            return false;
        }

        // Past this point the completion is committed exactly once: CanCompleteStageRun is false
        // for every later call, so XP and Stage Progress cannot be granted twice.
        stageCompletionCommitted = true;
        GrantXpToDeployedParty(completionXpReward);

        int nextProgress = Mathf.Min(runConfig.TargetRunCount, stageProgressCount + 1);
        saveManager.SaveStageProgress(runConfig.StageId, nextProgress);
        stageProgressCount = nextProgress;

        Log($"Completed '{runConfig.StageId}'. Progress {nextProgress}/{runConfig.TargetRunCount}.");
        sceneLoader.LoadBasement();
        return true;
    }

    void SpawnStageExit(MapRunController run, RoomController room)
    {
        if (stageExitInstance != null || room == null || runConfig.StageExitPrefab == null)
            return;

        Transform spawnPoint = room.GetStageExitSpawnPoint();
        Transform parent = room.RuntimeContent != null ? room.RuntimeContent.PersistentRoot : room.transform;
        stageExitInstance = UnityEngine.Object.Instantiate(
            runConfig.StageExitPrefab,
            spawnPoint.position,
            spawnPoint.rotation,
            parent);

        StageExitInteractable stageExit = stageExitInstance.GetComponentInChildren<StageExitInteractable>(true);
        if (stageExit == null)
            stageExit = stageExitInstance.AddComponent<StageExitInteractable>();
        stageExit.Configure(run);
    }

    /// <summary>
    /// Splits the remaining pool evenly over the spawns that are still to come, so an early kill
    /// cannot drain the budget and leave later enemies worthless.
    /// </summary>
    int AllocateEnemyXp(bool boss)
    {
        int enemiesRemaining = boss ? bossEnemiesRemaining : regularEnemiesRemaining;
        int xpRemaining = boss ? bossXpRemaining : regularXpRemaining;
        if (enemiesRemaining <= 0 || xpRemaining <= 0)
            return 0;

        int reward = Mathf.CeilToInt((float)xpRemaining / enemiesRemaining);
        if (boss)
        {
            bossXpRemaining = Mathf.Max(0, bossXpRemaining - reward);
            bossEnemiesRemaining = Mathf.Max(0, bossEnemiesRemaining - 1);
        }
        else
        {
            regularXpRemaining = Mathf.Max(0, regularXpRemaining - reward);
            regularEnemiesRemaining = Mathf.Max(0, regularEnemiesRemaining - 1);
        }

        return reward;
    }

    void GrantXpToDeployedParty(int amount)
    {
        if (amount <= 0)
            return;

        PartyRuntime party = resolveParty?.Invoke();
        if (party == null)
        {
            Debug.LogWarning("[MapRunController] No deployed PartyRuntime was found for Stage XP.", owner);
            return;
        }

        for (int i = 0; i < party.Actors.Count; i++)
        {
            PartyRuntimeActor actor = party.Actors[i];
            LevelSystem levelSystem = actor?.Context != null
                ? actor.Context.GetComponentInChildren<LevelSystem>(true)
                : null;
            if (levelSystem != null)
                levelSystem.AddXp(amount);
        }
    }

    void Log(string message)
    {
        log?.Invoke(message);
    }
}
