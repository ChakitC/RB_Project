using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class EncounterDirector : MonoBehaviour
{
    [Tooltip("parent สำหรับ enemy ที่ spawn ระหว่าง encounter ถ้าไม่ใส่จะ spawn ที่ root scene")]
    [SerializeField] private Transform enemyParent;

    [Tooltip("เปิด log warning เมื่อ encounter setup ไม่ครบ เช่น ไม่มี enemy prefab")]
    [SerializeField] private bool logWarnings = true;

    private readonly HashSet<HealthSystem> trackedEnemies = new();
    private RoomController activeRoom;
    private Coroutine activeRoutine;
    private int aliveCount;
    private bool running;

    public bool IsRunning => running;
    public int AliveCount => aliveCount;

    public void StartEncounter(RoomController room, EncounterDefinitionSO encounter)
    {
        StopEncounter();

        activeRoom = room;
        running = true;
        activeRoutine = StartCoroutine(RunEncounter(encounter));
    }

    public void StopEncounter()
    {
        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
            activeRoutine = null;
        }

        foreach (HealthSystem health in trackedEnemies)
        {
            if (health != null)
                health.CharacterDead -= OnTrackedEnemyDead;
        }

        trackedEnemies.Clear();
        aliveCount = 0;
        running = false;
        activeRoom = null;
    }

    IEnumerator RunEncounter(EncounterDefinitionSO encounter)
    {
        if (encounter == null || encounter.Waves == null || encounter.Waves.Length == 0)
        {
            if (encounter == null || encounter.CompleteWhenNoEnemies)
                CompleteEncounter();

            yield break;
        }

        int spawnIndex = 0;
        for (int waveIndex = 0; waveIndex < encounter.Waves.Length; waveIndex++)
        {
            EncounterWave wave = encounter.Waves[waveIndex];
            if (wave == null)
                continue;

            if (wave.InitialDelay > 0f)
                yield return new WaitForSeconds(wave.InitialDelay);

            for (int i = 0; i < wave.SpawnCount; i++)
            {
                SpawnEnemy(wave, spawnIndex);
                spawnIndex++;

                if (wave.SpawnInterval > 0f && i < wave.SpawnCount - 1)
                    yield return new WaitForSeconds(wave.SpawnInterval);
            }

            if (wave.WaitForWaveClear)
                yield return new WaitUntil(() => aliveCount <= 0);
        }

        yield return new WaitUntil(() => aliveCount <= 0);
        CompleteEncounter();
    }

    void SpawnEnemy(EncounterWave wave, int spawnIndex)
    {
        if (activeRoom == null)
            return;

        GameObject prefab = wave.GetRandomEnemyPrefab();
        if (prefab == null)
        {
            Warn("Encounter wave has no enemy prefab.");
            return;
        }

        Transform spawnPoint = activeRoom.GetEnemySpawnPoint(spawnIndex);
        Transform spawnParent = activeRoom.RuntimeContent != null
            ? activeRoom.RuntimeContent.EncounterRoot
            : enemyParent;
        GameObject enemyObject = Instantiate(prefab, spawnPoint.position, spawnPoint.rotation);
        if (spawnParent != null)
            enemyObject.transform.SetParent(spawnParent, true);

        TrackEnemyObject(enemyObject);
    }

    void TrackEnemyObject(GameObject enemyObject)
    {
        if (enemyObject == null)
            return;

        bool trackedAny = false;
        CharacteContext[] contexts = enemyObject.GetComponentsInChildren<CharacteContext>(true);
        for (int i = 0; i < contexts.Length; i++)
        {
            CharacteContext ctx = contexts[i];
            if (ctx == null || ctx.TargetIdentity != AITargetIdentity.Enemy)
                continue;

            ctx.ResolveReferences();
            HealthSystem health = ctx.HealthSystem != null ? ctx.HealthSystem : ctx.GetComponentInChildren<HealthSystem>(true);
            if (TrackHealthSystem(health))
                trackedAny = true;
        }

        if (trackedAny)
            return;

        HealthSystem[] healthSystems = enemyObject.GetComponentsInChildren<HealthSystem>(true);
        for (int i = 0; i < healthSystems.Length; i++)
            TrackHealthSystem(healthSystems[i]);
    }

    bool TrackHealthSystem(HealthSystem health)
    {
        if (health == null || trackedEnemies.Contains(health) || health.IsDead)
            return false;

        trackedEnemies.Add(health);
        aliveCount++;
        health.CharacterDead += OnTrackedEnemyDead;
        return true;
    }

    void OnTrackedEnemyDead()
    {
        CleanupDeadTrackedEnemies();
    }

    void CleanupDeadTrackedEnemies()
    {
        var dead = new List<HealthSystem>();
        foreach (HealthSystem health in trackedEnemies)
        {
            if (health == null || health.IsDead)
                dead.Add(health);
        }

        for (int i = 0; i < dead.Count; i++)
        {
            HealthSystem health = dead[i];
            if (health != null)
                health.CharacterDead -= OnTrackedEnemyDead;

            if (trackedEnemies.Remove(health))
                aliveCount = Mathf.Max(0, aliveCount - 1);
        }
    }

    void CompleteEncounter()
    {
        CleanupDeadTrackedEnemies();
        running = false;
        activeRoutine = null;

        RoomController completedRoom = activeRoom;
        activeRoom = null;
        trackedEnemies.Clear();
        aliveCount = 0;

        completedRoom?.CompleteRoom();
    }

    void Warn(string message)
    {
        if (logWarnings)
            Debug.LogWarning($"[EncounterDirector] {message}", this);
    }
}
