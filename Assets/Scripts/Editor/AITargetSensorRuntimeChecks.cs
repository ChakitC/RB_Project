using Opsive.GraphDesigner.Runtime.Variables;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Instrumented runtime checks invoked by the dedicated Play Mode test assembly.</summary>
public sealed class AITargetSensorRuntimeChecks
{
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    readonly List<Object> owned = new List<Object>();
    int pauseToken;
    readonly TimeSlowManager originalSlow = Object.FindAnyObjectByType<TimeSlowManager>();
    readonly GlobalTimeScaleManager originalGlobal = Object.FindAnyObjectByType<GlobalTimeScaleManager>();
    T Own<T>(T value) where T : Object { owned.Add(value); return value; }
    static void Set(object target, string name, object value) => target.GetType().GetField(name, Hidden).SetValue(target, value);
    static float Clock(AITargetSensor sensor) => (float)typeof(AITargetSensor).GetField("clock", Hidden).GetValue(sensor);

    public void TearDown()
    {
        if (pauseToken != 0) GlobalTimeScaleManager.Instance.ReleasePauseToken(pauseToken);
        for (int i = owned.Count - 1; i >= 0; i--) if (owned[i]) Object.DestroyImmediate(owned[i]);
        owned.Clear();
        if (originalSlow == null)
        {
            var manager = Object.FindAnyObjectByType<TimeSlowManager>();
            if (manager != null) Object.DestroyImmediate(manager.gameObject);
        }
        if (originalGlobal == null)
        {
            var manager = Object.FindAnyObjectByType<GlobalTimeScaleManager>();
            if (manager != null) Object.DestroyImmediate(manager.gameObject);
        }
    }

    T Actor<T>(Vector3 position) where T : CharacteContext
    {
        var go = Own(new GameObject(typeof(T).Name + " sensor check"));
        go.SetActive(false);
        go.layer = 30;
        go.transform.position = position;
        T ctx = go.AddComponent<T>();
        ctx.baseStats = Own(ScriptableObject.CreateInstance<CharacterStats>());
        ctx.baseStats.maxHP = 100;
        ctx.HealthSystem = go.AddComponent<HealthSystem>();
        ctx.HealthSystem.CTX = ctx;
        ctx.stateHub = go.AddComponent<StateHub>();
        ctx.TargetInfo = go.AddComponent<AITargetInfo>();
        var aim = new GameObject("Aim").transform;
        aim.SetParent(go.transform, false);
        aim.localPosition = Vector3.up;
        Set(ctx.TargetInfo, "aimPoint", aim);
        return ctx;
    }

    AITargetSensor Sensor(CharacteContext ctx)
    {
        var sensor = ctx.gameObject.AddComponent<AITargetSensor>();
        Set(sensor, "targetLayers", (LayerMask)(1 << 30));
        Set(sensor, "obstacleLayers", (LayerMask)(1 << 29));
        Set(sensor, "scanInterval", .02f);
        Set(sensor, "gracePeriod", .08f);
        Set(sensor, "targetingProfile", null);
        return sensor;
    }

    public IEnumerator RuntimePerceptionAndFiring()
    {
        var enemy = Actor<EnemyContext>(new Vector3(0, 0, 21000));
        var ally = Actor<AllyContext>(enemy.transform.position + Vector3.forward * 3);
        var summon = Actor<SummonContext>(enemy.transform.position + Vector3.right * 3);
        AITargetSensor enemySensor = Sensor(enemy), allySensor = Sensor(ally), summonSensor = Sensor(summon);
        enemy.gameObject.SetActive(true); ally.gameObject.SetActive(true); summon.gameObject.SetActive(true);
        yield return null;
        enemySensor.ForceScan(); allySensor.ForceScan(); summonSensor.ForceScan();
        Assert.IsNotNull(enemySensor.CurrentTargetContext);
        Assert.AreEqual(enemy, allySensor.CurrentTargetContext);
        Assert.AreEqual(enemy, summonSensor.CurrentTargetContext);

        var blackboard = new HasEnemyFromSensor
        {
            context = new SharedVariable<CharacteContext> { Name = "Ctx", Scope = SharedVariable.SharingScope.Graph, Value = enemy },
            currentEnemy = new Opsive.GraphDesigner.Runtime.Variables.SharedVariable<GameObject>(),
            InCombat = new Opsive.GraphDesigner.Runtime.Variables.SharedVariable<bool>()
        };
        Set(blackboard, "sensor", enemySensor);
        typeof(HasEnemyFromSensor).GetMethod("SubscribeToSensor", Hidden).Invoke(blackboard, null);
        typeof(HasEnemyFromSensor).GetMethod("SyncOutputsFromSensor", Hidden).Invoke(blackboard, null);
        Assert.IsTrue(blackboard.InCombat.Value);

        var wall = Own(new GameObject("Sight blocker"));
        wall.layer = 29;
        wall.transform.position = enemy.transform.position + new Vector3(0, 1, 1.5f);
        wall.AddComponent<BoxCollider>().size = new Vector3(10, 4, .2f);
        Physics.SyncTransforms();
        allySensor.ForceScan();
        Assert.AreEqual(AITargetSelectionReason.Memory, allySensor.Snapshot.Reason);
        Assert.IsFalse(allySensor.HasLineOfSight);
        yield return new WaitForSecondsRealtime(.12f);
        allySensor.ForceScan(); Assert.IsNull(allySensor.CurrentTarget);

        var effect = Own(ScriptableObject.CreateInstance<StatusEffectDef>());
        effect.tags.Add(StatusEffectTags.Taunt);
        if (enemy.StatusEffects == null) enemy.StatusEffects = enemy.gameObject.AddComponent<StatusEffectController>();
        enemy.StatusEffects.ApplyEffect(effect, ally.gameObject);
        Assert.IsTrue(enemySensor.OnTauntApplied(ally.transform));
        Assert.AreEqual(ally, enemySensor.CurrentTargetContext);
        Assert.AreEqual(enemySensor.CurrentTarget.gameObject, blackboard.currentEnemy.Value);
        Assert.IsFalse(enemySensor.HasLineOfSight);
        var shoot = new AiShoot();
        Set(shoot, "targetSensor", enemySensor); Set(shoot, "actorTransform", enemy.transform);
        bool canFire = (bool)typeof(AiShoot).GetMethod("HasValidFiringPosition", Hidden).Invoke(shoot,
            new object[]{enemySensor.Snapshot.AimPosition, enemySensor.Snapshot});
        Assert.IsFalse(canFire, "The production firing guard must reject an occluded taunter.");

        wall.SetActive(false); Physics.SyncTransforms(); enemySensor.ForceScan();
        Assert.IsTrue(enemySensor.HasLineOfSight);
        ally.gameObject.SetActive(false);
        Assert.AreNotEqual(ally, enemySensor.CurrentTargetContext);
        ally.transform.position += Vector3.forward * 100;
        ally.gameObject.SetActive(true); yield return null;
        enemySensor.ForceScan();
        Assert.AreNotEqual(ally, enemySensor.CurrentTargetContext, "Old taunt must not reacquire a new life out of range.");
        summon.HealthSystem.Down();
        Assert.IsNull(enemySensor.CurrentTarget, "Downed actor cannot remain a combat target.");
        enemySensor.ForceScan(); Assert.IsFalse(blackboard.InCombat.Value);
        var room = UnityEngine.SceneManagement.SceneManager.CreateScene("AI sensor temporary room");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(summon.gameObject, room);
        yield return UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(room);
        Assert.IsNull(enemySensor.CurrentTarget);
        typeof(HasEnemyFromSensor).GetMethod("UnsubscribeFromSensor", Hidden).Invoke(blackboard, null);
    }

    public IEnumerator RuntimeClockAndScanCosts()
    {
        var enemy = Actor<EnemyContext>(new Vector3(0, 0, 22000));
        var ally = Actor<AllyContext>(enemy.transform.position + Vector3.forward * 3);
        var sensor = Sensor(enemy);
        enemy.gameObject.SetActive(true); ally.gameObject.SetActive(true);
        yield return null;
        sensor.ForceScan();
        float before = Clock(sensor);
        pauseToken = GlobalTimeScaleManager.Instance.AcquirePauseToken();
        yield return new WaitForSecondsRealtime(.05f);
        sensor.ForceScan(); Assert.AreEqual(before, Clock(sensor), .005f);
        GlobalTimeScaleManager.Instance.ReleasePauseToken(pauseToken); pauseToken = 0;
        GlobalTimeScaleManager.Instance.RequestHitLag(.15f, .01f);
        before = Clock(sensor);
        yield return new WaitForSecondsRealtime(.05f);
        sensor.ForceScan(); Assert.Greater(Clock(sensor) - before, .02f);

        var time = TimeSlowManager.Instance;
        bool wasEnabled = time.enabled;
        PropertyInfo scale = typeof(TimeSlowManager).GetProperty("WorldTimeScale");
        float oldScale = time.WorldTimeScale;
        try
        {
            time.enabled = false; scale.SetValue(time, .2f);
            before = Clock(sensor);
            float real = Time.unscaledTime;
            yield return new WaitForSecondsRealtime(.06f);
            sensor.ForceScan();
            Assert.AreEqual((Time.unscaledTime - real) * .2f, Clock(sensor) - before, .012f);
            enemy.PushWorldSlowExemption();
            before = Clock(sensor); real = Time.unscaledTime;
            yield return new WaitForSecondsRealtime(.06f);
            sensor.ForceScan();
            Assert.AreEqual(Time.unscaledTime - real, Clock(sensor) - before, .015f);
            enemy.PopWorldSlowExemption();
        }
        finally { scale.SetValue(time, oldScale); time.enabled = wasEnabled; }

        var sensors = new List<AITargetSensor> {sensor};
        foreach (int count in new[]{10,50,100})
        {
            while (sensors.Count < count)
            {
                var actor = Actor<EnemyContext>(enemy.transform.position + Vector3.left * (sensors.Count % 5));
                var next = Sensor(actor); actor.gameObject.SetActive(true); sensors.Add(next);
            }
            yield return null;
            for(int warm = 0; warm < 5; warm++) foreach(var item in sensors) item.ForceScan();
            var watch = new System.Diagnostics.Stopwatch();
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            watch.Start();
            for(int pass = 0; pass < 10; pass++) foreach(var item in sensors) item.ForceScan();
            watch.Stop();
            long bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
            Debug.Log($"AI sensor benchmark: {count} sensors, {count*10} scans, {watch.Elapsed.TotalMilliseconds:F3} ms, {bytes} bytes.");
            Assert.AreEqual(0, bytes, "Warm scan path must not allocate managed memory.");
        }
    }

    public IEnumerator RuntimeAuthoredPrefabs()
    {
        var container = Own(new GameObject("Authored AI sensor check"));
        container.SetActive(false);
        string[] paths = {"Assets/Prefab/GameEnemy/Enemy_E_GR_01 Variant.prefab", "Assets/Prefab/Player/Ally_Stryker.prefab", "Assets/Prefab/Player/MinigunTerret_Summon.prefab"};
        var sensors = new List<AITargetSensor>();
        for(int i = 0; i < paths.Length; i++)
        {
            var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]), container.transform);
            root.transform.position = new Vector3(i * 3, 0, 23000);
            // Keep the actual context, health, target references and sensor; suspend autonomous actions for this probe.
            foreach(var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (!(behaviour is CharacteContext) && !(behaviour is AITargetSensor) && !(behaviour is AITargetInfo))
                    behaviour.enabled = false;
            foreach(var agent in root.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true)) agent.enabled = false;
            sensors.Add(root.GetComponentInChildren<AITargetSensor>(true));
        }
        container.SetActive(true);
        yield return null;
        foreach(var sensor in sensors)
        {
            sensor.ForceScan();
            Assert.IsNotNull(sensor.CurrentTargetContext, sensor.name + " must resolve authored peers and opposing root layers.");
        }
        yield return null;
    }
}
