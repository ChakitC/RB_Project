using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Wrappers;
using Opsive.GraphDesigner.Runtime.Variables;
using Object = UnityEngine.Object;

/// <summary>Typed bridge called by the isolated Edit/Play Mode test assemblies.</summary>
public sealed partial class AICombatP0Checks
{
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    readonly List<Object> owned = new List<Object>();
    NavMeshDataInstance mesh;
    int pauseToken;
    int slowToken;
    readonly Vector3 origin = new Vector3(0f, 0f, 24000f);
    EnemyContext owner;
    AllyContext opponent;
    AITargetSensor sensor;
    SharedVariable<AICombatState> state;
    SharedVariable<GameObject> target;
    SharedVariable<CharacteContext> context;
    readonly GlobalTimeScaleManager originalGlobal = Object.FindAnyObjectByType<GlobalTimeScaleManager>();
    readonly TimeSlowManager originalSlow = Object.FindAnyObjectByType<TimeSlowManager>();
    T Own<T>(T value) where T : Object { owned.Add(value); return value; }
    static void Set(object o, string name, object value)
    {
        for (Type t = o.GetType(); t != null; t = t.BaseType)
        {
            FieldInfo f = t.GetField(name, Hidden);
            if (f == null) continue;
            f.SetValue(o, value); return;
        }
        throw new MissingFieldException(name);
    }
    T Bind<T>(T task) where T : Task
    {
        Set(task, "m_GameObject", owner.gameObject);
        Set(task, "m_Transform", owner.transform);
        var field = task.GetType().GetField("context");
        if (field != null) field.SetValue(task, context);
        if (task is AICombatConditional c) { c.combatState = state; c.target = target; }
        if (task is AICombatAction a) { a.combatState = state; a.target = target; }
        if (task is AiShoot shoot) shoot.target = target;
        return task;
    }
    T Actor<T>(Vector3 position, bool nested) where T : CharacteContext
    {
        var go = Own(new GameObject(typeof(T).Name + " P0 check"));
        go.SetActive(false); go.layer = 30; go.transform.position = position;
        T ctx = go.AddComponent<T>();
        ctx.baseStats = Own(ScriptableObject.CreateInstance<CharacterStats>());
        ctx.baseStats.maxHP = 100f;
        ctx.baseStats.speed = 4f;
        var peers = nested ? new GameObject("Gameplay peers") : go;
        if (nested) peers.transform.SetParent(go.transform, false);
        ctx.TargetInfo = peers.AddComponent<AITargetInfo>();
        var aim = new GameObject("Aim").transform;
        aim.SetParent(go.transform, false); aim.localPosition = Vector3.up;
        Set(ctx.TargetInfo, "aimPoint", aim);
        ctx.stateHub = peers.AddComponent<StateHub>();
        if (!Application.isPlaying) typeof(StateHub).GetMethod("Awake", Hidden).Invoke(ctx.stateHub, null);
        return ctx;
    }
    void Setup(bool navigation = false, bool nested = false)
    {
        if (navigation)
        {
            var sources = new List<NavMeshBuildSource> { new NavMeshBuildSource
                { shape = NavMeshBuildSourceShape.Box, transform = Matrix4x4.TRS(origin + Vector3.down * .1f, Quaternion.identity, Vector3.one),
                    size = new Vector3(60f, .2f, 60f), area = 0 }, new NavMeshBuildSource
                { shape = NavMeshBuildSourceShape.Box, transform = Matrix4x4.TRS(origin + Vector3.right * 100f + Vector3.down * .1f, Quaternion.identity, Vector3.one),
                    size = new Vector3(10f, .2f, 10f), area = 0 } };
            NavMeshData data = Own(NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByIndex(0), sources,
                new Bounds(origin + Vector3.right * 40f, new Vector3(140f, 5f, 65f)), Vector3.zero, Quaternion.identity));
            mesh = NavMesh.AddNavMeshData(data);
        }
        owner = Actor<EnemyContext>(origin, nested);
        opponent = Actor<AllyContext>(origin + Vector3.forward * 6f, !nested);
        sensor = owner.TargetInfo.gameObject.AddComponent<AITargetSensor>();
        Set(sensor, "targetLayers", (LayerMask)(1 << 30));
        Set(sensor, "obstacleLayers", (LayerMask)(1 << 29));
        Set(sensor, "gracePeriod", .1f);
        if (!Application.isPlaying) typeof(AITargetSensor).GetMethod("Awake", Hidden).Invoke(sensor, null);
        if (navigation)
        {
            owner.NavigationAgent = owner.gameObject.AddComponent<NavMeshAgent>();
            owner.NavigationAgent.speed = 4f;
            owner.NavigationAgent.stoppingDistance = .1f;
        }
        owner.gameObject.SetActive(true); opponent.gameObject.SetActive(true);
        owner.ResolveReferences(); opponent.ResolveReferences();
        CharacterContextRegistry.Register(owner); CharacterContextRegistry.Register(opponent);
        sensor.ForceScan();
        state = new SharedVariable<AICombatState>();
        target = new SharedVariable<GameObject> { Value = sensor.CurrentTarget.gameObject };
        context = new SharedVariable<CharacteContext> { Name = "Ctx", Scope = SharedVariable.SharingScope.Graph };
        var initialize = Bind(new InitializeAIContext()); initialize.OnBehaviorTreeStarted();
        Assert.AreEqual(TaskStatus.Success, initialize.OnUpdate());
        var canAct = Bind(new CanAct()); canAct.OnBehaviorTreeStarted(); canAct.OnStart();
    }
    void Weapon()
    {
        owner.WeaponSystem = owner.TargetInfo.gameObject.AddComponent<WeaponSystem>();
        owner.WeaponSystem.ctx = owner;
        var gun = Own(ScriptableObject.CreateInstance<GunConfig>());
        gun.maxMagazine = 10; gun.magazine = 10; gun.maxReserveAmmo = 10; gun.reloadTime = .15f; gun.magazineReload = true;
        Set(owner.WeaponSystem, "currentWeapon", gun);
        owner.WeaponSystem.magazine = 10;
        owner.WeaponSystem.maxMagazine = 10;
        owner.WeaponSystem.reserveAmmo = 5;
        WeaponAimTestSource.Bind(owner.WeaponSystem, owner.transform);
    }
    public void TearDown()
    {
        if (owner != null) CharacterContextRegistry.Unregister(owner);
        if (opponent != null) CharacterContextRegistry.Unregister(opponent);
        if (pauseToken != 0) GlobalTimeScaleManager.Instance.ReleasePauseToken(pauseToken);
        if (slowToken != 0) TimeSlowManager.Instance.EndPresentationSlow(slowToken);
        if (mesh.valid) mesh.Remove();
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
    public void ConditionalsArePureAndReevaluateIdentically()
    {
        Setup(); Weapon();
        state.Value.FailNavigation("Navigation stuck");
        state.Value.NavigationTarget = sensor.ReadSnapshot.Target;
        AICombatConditional[] conditions = { Bind(new CanAct()), Bind(new CanShootTarget()),
            Bind(new IsCurrentTargetVisible()), Bind(new HasLastSeenPosition()), Bind(new IsNavigationStuck()) };
        AITargetSnapshot before = sensor.ReadSnapshot;
        foreach (var condition in conditions)
        {
            condition.OnStart();
            Assert.AreEqual(condition.OnUpdate(), condition.OnReevaluateUpdate(), condition.GetType().Name);
        }
        Assert.IsFalse(owner.WeaponSystem.IsFiringHeld);
        Assert.IsFalse(owner.WeaponSystem.IsReloading);
        Assert.AreEqual(before.Target, sensor.ReadSnapshot.Target);
        Assert.AreEqual(before.LastSeenTime, sensor.ReadSnapshot.LastSeenTime);
        Assert.AreEqual("Navigation stuck", state.Value.NavigationFailure);
    }
    public void RangeAndAmmoAdmission()
    {
        Setup(); Weapon();
        var condition = Bind(new CanShootTarget { minRange = 5f, maxRange = 7f }); condition.OnStart();
        Assert.AreEqual(TaskStatus.Success, condition.OnUpdate());
        Assert.IsTrue(AICombatTaskUtility.InRange(5f, 5f, 7f));
        Assert.IsTrue(AICombatTaskUtility.InRange(7f, 5f, 7f));
        Assert.IsFalse(AICombatTaskUtility.InRange(4.99f, 5f, 7f));
        Assert.IsTrue(AICombatTaskUtility.InRange(4.5f, 5f, 7f, .5f));
        owner.WeaponSystem.magazine = 0;
        Assert.AreEqual(TaskStatus.Failure, condition.OnUpdate());
        condition.allowReloadCycle = true;
        Assert.AreEqual(TaskStatus.Success, condition.OnUpdate());
        owner.WeaponSystem.reserveAmmo = 0;
        Set(owner, "forceInfiniteReserveAmmo", false);
        Assert.AreEqual(TaskStatus.Failure, condition.OnUpdate());
        Set(owner.WeaponSystem, "isReloading", true);
        Assert.AreEqual(TaskStatus.Success, condition.OnUpdate());
        Assert.IsTrue(AICombatTaskUtility.CanAct(owner), "Reload alone must keep AI eligible.");
        condition.allowReloadCycle = false;
        Assert.AreEqual(TaskStatus.Failure, condition.OnUpdate());
        Set(owner.WeaponSystem, "isReloading", false); owner.WeaponSystem.magazine = 5;
        Assert.AreEqual(TaskStatus.Success, condition.OnUpdate());
        owner.stateHub.SetExternalControlState(ControlBlockFlags.Move, true);
        Assert.IsFalse(AICombatTaskUtility.CanAct(owner));
    }
    public void PureSightQueryUsesObstacleAndOwnerRules()
    {
        Setup();
        AITargetSnapshot s = sensor.ReadSnapshot;
        Assert.IsTrue(sensor.HasLineOfSightFrom(origin, s.Target, s.AimPosition));
        var wall = Own(new GameObject("P0 wall")); wall.layer = 29;
        wall.transform.position = origin + new Vector3(0f, 1f, 3f);
        wall.AddComponent<BoxCollider>().size = new Vector3(8f, 3f, .4f);
        Physics.SyncTransforms();
        Assert.IsFalse(sensor.HasLineOfSightFrom(origin, s.Target, s.AimPosition));
        Assert.IsTrue(sensor.HasLineOfSightFrom(origin + Vector3.right * 10f, s.Target, s.AimPosition));
        Assert.AreEqual(s.LastSeenTime, sensor.ReadSnapshot.LastSeenTime);
        Assert.AreEqual(s.Target, sensor.ReadSnapshot.Target);
    }
    public void ObservationConsumptionAndPooledLife()
    {
        Setup();
        AITargetHandle handle = new AITargetHandle(opponent);
        var observation = new AITargetSnapshot(default, default, handle, origin, origin, 1f, 0f, 0f, false, false, AITargetSelectionReason.None);
        state.Value.SearchActive = true; state.Value.SearchTarget = handle; state.Value.SearchObservationTime = 1f;
        state.Value.ConsumeSearch();
        Assert.IsTrue(state.Value.IsConsumed(observation));
        observation = new AITargetSnapshot(default, default, handle, origin, origin, 2f, 0f, 0f, false, false, AITargetSelectionReason.None);
        Assert.IsFalse(state.Value.IsConsumed(observation));
        Set(opponent, "_lifeGeneration", opponent.LifeGeneration + 1);
        Assert.IsFalse(handle.IsLive);
        Assert.IsFalse(AICombatTaskUtility.IsEligible(owner, handle));
    }
    public void StateQuotaAndTargetReplacement()
    {
        Setup();
        var data = state.Value;
        data.BeginNavigation("CombatRange", new AITargetHandle(opponent), origin + Vector3.right);
        data.RecoveryAttempts = 2;
        data.BeginNavigation("CombatRange", new AITargetHandle(opponent), origin + Vector3.right * 2f);
        Assert.AreEqual(2, data.RecoveryAttempts, "Repath must not reset the logical goal's quota.");
        data.HasFiringPosition = true; data.FiringTarget = new AITargetHandle(opponent);
        data.ObserveTarget(default);
        Assert.IsFalse(data.HasFiringPosition); Assert.IsFalse(data.NavigationActive);
        Assert.IsNull(data.NavigationFailure);
        var first = data;
        context = new SharedVariable<CharacteContext> { Name = "Ctx", Scope = SharedVariable.SharingScope.Graph };
        var initialize = Bind(new InitializeAIContext()); initialize.OnBehaviorTreeStarted();
        Assert.AreEqual(TaskStatus.Success, initialize.OnUpdate());
        var canAct = Bind(new CanAct()); canAct.OnBehaviorTreeStarted(); canAct.OnStart();
        Assert.AreNotSame(first, state.Value);
    }
    public void TargetEligibilityRejectsDeadDownAndUntargetable()
    {
        Setup(); Weapon();
        var condition = Bind(new CanShootTarget()); condition.OnStart();
        Assert.AreEqual(TaskStatus.Success, condition.OnUpdate());
        opponent.TargetInfo.SetTargetable(false);
        Assert.AreEqual(TaskStatus.Failure, condition.OnReevaluateUpdate());
        opponent.TargetInfo.SetTargetable(true); sensor.ForceScan();
        opponent.TargetInfo.SetAlive(false);
        Assert.AreEqual(TaskStatus.Failure, condition.OnUpdate());
        opponent.TargetInfo.SetAlive(true); sensor.ForceScan();
        opponent.HealthSystem = opponent.TargetInfo.gameObject.AddComponent<HealthSystem>();
        Set(opponent.HealthSystem, "<LifeState>k__BackingField", LifeStateId.Down);
        Assert.AreEqual(TaskStatus.Failure, condition.OnUpdate());
        Set(opponent.HealthSystem, "<LifeState>k__BackingField", LifeStateId.Dead);
        Assert.AreEqual(TaskStatus.Failure, condition.OnReevaluateUpdate());
    }
    public void GraphRoundTripAndPrefabBindings()
    {
        var graph = AssetDatabase.LoadAssetAtPath<Subtree>(AICombatP0GraphAuthoring.GraphPath);
        Assert.IsTrue(graph.Deserialize(true));
        Assert.AreEqual(36, graph.TreeLogicNodes.Length);
        Assert.IsEmpty(AIContextGraphAuthoring.Validate(graph));
        foreach (var variable in graph.SharedVariables) Assert.AreEqual(SharedVariable.SharingScope.Graph, variable.Scope);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/GameEnemy/Enemy_M_GR_01 Variant.prefab");
        Assert.IsNotNull(prefab.GetComponentInChildren<Opsive.BehaviorDesigner.Runtime.BehaviorTree>(true));
        Assert.IsNotNull(prefab.GetComponentInChildren<CharacteContext>(true));
        Assert.IsNotNull(prefab.GetComponentInChildren<AITargetSensor>(true));
    }
    public IEnumerator NavigationAndRecovery()
    {
        Setup(true, true); yield return null;
        Assert.IsTrue(owner.NavigationAgent.isOnNavMesh);
        var find = Bind(new FindFiringPosition()); find.OnStart();
        Assert.AreEqual(TaskStatus.Success, find.OnUpdate()); find.OnEnd();
        var move = Bind(new MoveToFiringPosition()); move.OnStart();
        TaskStatus result = TaskStatus.Running;
        for (int i = 0; i < 240 && result == TaskStatus.Running; i++) { result = move.OnUpdate(); yield return null; }
        Assert.AreEqual(TaskStatus.Success, result); move.OnEnd();
        state.Value.BeginNavigation("Stuck", sensor.ReadSnapshot.Target, origin + Vector3.right * 10f);
        var navigation = new AICombatNavigation(owner, state.Value);
        Assert.IsTrue(navigation.Command(state.Value.Destination));
        owner.NavigationAgent.isStopped = true;
        pauseToken = GlobalTimeScaleManager.Instance.AcquirePauseToken();
        for (int i = 0; i < 4; i++) { Assert.AreEqual(TaskStatus.Running, navigation.Tick(state.Value.Destination)); yield return null; }
        GlobalTimeScaleManager.Instance.ReleasePauseToken(pauseToken); pauseToken = 0;
        owner.stateHub.SetExternalControlState(ControlBlockFlags.Move, true);
        yield return new UnityEngine.WaitForSeconds(1.6f);
        Assert.AreEqual(TaskStatus.Running, navigation.Tick(state.Value.Destination));
        owner.stateHub.SetExternalControlState(ControlBlockFlags.None, false);
        // The stuck policy uses elapsed game time; a frame cap can finish before 1.5s in fast Editor runs.
        float stuckDeadline = Time.time + 3f;
        while (Time.time < stuckDeadline && state.Value.NavigationFailure == null) { navigation.Tick(state.Value.Destination); yield return null; }
        Assert.AreEqual("Navigation stuck", state.Value.NavigationFailure);
        navigation.Cleanup();
        state.Value.RecoveryAttempts = 2;
        Vector3 before = owner.transform.position;
        var recover = Bind(new RecoverNavigation()); recover.OnStart(); result = TaskStatus.Running;
        for (int i = 0; i < 120 && result == TaskStatus.Running; i++) { result = recover.OnUpdate(); yield return null; }
        Assert.AreEqual(TaskStatus.Failure, result); recover.OnEnd();
        Assert.Less(Vector3.Distance(before, owner.transform.position), .1f, "Exhausted recovery cannot teleport.");
    }
    public IEnumerator ReloadAndAbort()
    {
        Setup(true); Weapon(); yield return null;
        WeaponAimTestSource.Bind(owner.WeaponSystem, owner.transform);
        var shoot = Bind(new AiShoot { faceTarget = true, returnSuccessWhenTargetLost = false });
        shoot.OnStart();
        Set(owner.WeaponSystem, "isReloading", true);
        Assert.AreEqual(TaskStatus.Running, shoot.OnUpdate());
        Assert.IsFalse(owner.WeaponSystem.IsFiringHeld);
        Set(owner.WeaponSystem, "isReloading", false);
        Assert.AreEqual(TaskStatus.Running, shoot.OnUpdate());
        Assert.IsTrue(owner.stateHub.DesiredFireHeld);
        int token = owner.MovementMotor.AcquireAgentSuspension(owner.NavigationAgent);
        shoot.OnEnd();
        Assert.IsFalse(owner.stateHub.DesiredFireHeld);
        Assert.IsFalse(owner.NavigationAgent.updatePosition);
        Assert.IsFalse(owner.NavigationAgent.updateRotation);
        owner.MovementMotor.ReleaseAgentSuspension(token);
        owner.WeaponSystem.magazine = 0; owner.WeaponSystem.reserveAmmo = 0; Set(owner, "forceInfiniteReserveAmmo", false);
        shoot.OnStart();
        Assert.AreEqual(TaskStatus.Failure, shoot.OnUpdate()); shoot.OnEnd();
    }
    public IEnumerator LastObservationIsFrozenAndConsumedOnce()
    {
        Setup(true); yield return null;
        AITargetHandle handle = new AITargetHandle(opponent);
        Vector3 observed = opponent.transform.position;
        Set(sensor, "snapshot", new AITargetSnapshot(default, default, handle, observed, observed,
            sensor.ObservationClock, 0f, 0f, false, false, AITargetSelectionReason.None));
        sensor.enabled = false; // Reinstall the committed observation after its disable cleanup.
        Set(sensor, "snapshot", new AITargetSnapshot(default, default, handle, observed, observed,
            sensor.ObservationClock, 0f, 0f, false, false, AITargetSelectionReason.None));
        var condition = Bind(new HasLastSeenPosition()); condition.OnStart();
        Assert.AreEqual(TaskStatus.Success, condition.OnUpdate());
        var investigate = Bind(new InvestigateLastSeenPosition()); investigate.OnStart();
        opponent.transform.position += Vector3.right * 10f;
        Assert.AreEqual(observed, state.Value.SearchCenter);
        TaskStatus result = TaskStatus.Running;
        // Navigation advances in seconds; a frame budget can expire before arrival in an uncapped Editor.
        float deadline = Time.realtimeSinceStartup + 10f;
        while (result == TaskStatus.Running && Time.realtimeSinceStartup < deadline)
        {
            result = investigate.OnUpdate();
            yield return null;
        }
        Assert.AreEqual(TaskStatus.Success, result); investigate.OnEnd();
        var search = Bind(new SearchArea { maxPoints = 0 }); search.OnStart();
        Assert.AreEqual(TaskStatus.Success, search.OnUpdate()); search.OnEnd();
        Assert.AreEqual(TaskStatus.Failure, condition.OnUpdate());
        Assert.AreEqual(observed, sensor.ReadSnapshot.LastSeenPosition);
    }

    public IEnumerator LiveTreeSharesAssetButNotActorState()
    {
        Setup(true); Weapon(); yield return null;
        WeaponAimTestSource.Bind(owner.WeaponSystem, owner.transform);
        var graph = AssetDatabase.LoadAssetAtPath<Subtree>(AICombatP0GraphAuthoring.GraphPath);
        var firstTree = owner.gameObject.AddComponent<Opsive.BehaviorDesigner.Runtime.Wrappers.BehaviorTree>();
        firstTree.StartWhenEnabled = false; firstTree.Subgraph = graph;
        var second = Actor<EnemyContext>(origin + Vector3.right * 2f, true);
        second.NavigationAgent = second.gameObject.AddComponent<NavMeshAgent>(); second.NavigationAgent.speed = 4f;
        var secondSensor = second.TargetInfo.gameObject.AddComponent<AITargetSensor>();
        Set(secondSensor, "targetLayers", (LayerMask)(1 << 30)); Set(secondSensor, "obstacleLayers", (LayerMask)(1 << 29));
        var secondTree = second.gameObject.AddComponent<Opsive.BehaviorDesigner.Runtime.Wrappers.BehaviorTree>();
        secondTree.StartWhenEnabled = false; secondTree.Subgraph = graph;
        second.gameObject.SetActive(true); second.ResolveReferences(); secondSensor.ForceScan();
        yield return null;
        Assert.IsTrue(firstTree.StartBehavior()); Assert.IsTrue(secondTree.StartBehavior());
        bool sawOrbit = false, sawFire = false;
        for (int i = 0; i < 600 && !sawFire; i++)
        {
            var data = firstTree.GetVariable<AICombatState>("CombatState").Value;
            sawOrbit |= data != null && data.NavigationGoal == "Orbit";
            sawFire |= owner.stateHub.DesiredFireHeld;
            yield return null;
        }
        var firstState = firstTree.GetVariable<AICombatState>("CombatState").Value;
        var secondState = secondTree.GetVariable<AICombatState>("CombatState").Value;
        Assert.IsNotNull(firstState); Assert.IsNotNull(secondState); Assert.AreNotSame(firstState, secondState);
        Assert.AreEqual(new AITargetHandle(owner), firstState.Owner); Assert.AreEqual(new AITargetHandle(second), secondState.Owner);
        Assert.IsTrue(sawOrbit, "Actual tree must run its orbit branch.");
        Assert.IsTrue(sawFire, "Actual tree must reach the authored burst after orbit.");
        owner.stateHub.SetExternalControlState(ControlBlockFlags.Move | ControlBlockFlags.Rotate | ControlBlockFlags.Shoot, true);
        yield return null; yield return null;
        Assert.IsFalse(owner.stateHub.DesiredFireHeld, "CanAct abort must release held fire.");
        owner.stateHub.SetExternalControlState(ControlBlockFlags.None, false);
        var wall = Own(new GameObject("Live tree sight wall")); wall.layer = 29;
        wall.transform.position = opponent.transform.position + Vector3.up * 1f;
        wall.AddComponent<BoxCollider>().size = new Vector3(10f, 3f, 10f);
        Physics.SyncTransforms(); sensor.ForceScan();
        for (int i = 0; i < 30; i++) yield return null;
        Assert.IsFalse(owner.stateHub.DesiredFireHeld, "Occluded target cannot be fired at.");
        firstTree.StopBehavior(false); secondTree.StopBehavior(false);
        CharacterContextRegistry.Unregister(second);
    }

    public IEnumerator FiringCandidateFailuresAndRetarget()
    {
        Setup(true); yield return null;
        var find = Bind(new FindFiringPosition()); find.OnStart();
        Assert.AreEqual(TaskStatus.Success, find.OnUpdate());
        Vector3 first = state.Value.FiringPosition;
        Assert.AreEqual(TaskStatus.Success, find.OnUpdate());
        Assert.AreEqual(first, state.Value.FiringPosition, "Equal geometry must choose the same candidate.");
        var wall = Own(new GameObject("Enclosed target")); wall.layer = 29;
        wall.transform.position = opponent.transform.position + Vector3.up;
        wall.AddComponent<BoxCollider>().size = new Vector3(8f, 3f, 8f);
        Physics.SyncTransforms(); sensor.ForceScan();
        Assert.AreEqual(TaskStatus.Failure, find.OnUpdate());
        Assert.IsFalse(state.Value.HasFiringPosition);
        Object.DestroyImmediate(wall); sensor.ForceScan();
        Assert.AreEqual(TaskStatus.Success, find.OnUpdate());
        var move = Bind(new MoveToFiringPosition()); move.OnStart();
        opponent.gameObject.SetActive(false);
        Assert.AreEqual(TaskStatus.Failure, move.OnUpdate()); move.OnEnd();
        opponent.gameObject.SetActive(true); sensor.ForceScan();
        Assert.AreEqual(TaskStatus.Failure, move.OnUpdate(), "Old task cannot act on a pooled target's new life.");
        var path = new NavMeshPath();
        Assert.IsFalse(AICombatNavigation.TryPath(owner.NavigationAgent, origin + Vector3.forward * 100f, path, out _));
        Assert.IsTrue(owner.NavigationAgent.CalculatePath(origin + Vector3.right * 100f, path));
        Assert.AreEqual(NavMeshPathStatus.PathPartial, path.status);
        Assert.IsFalse(AICombatNavigation.TryPath(owner.NavigationAgent, origin + Vector3.right * 100f, path, out _), "Partial routes must fail.");
        find.OnEnd();
    }

    public IEnumerator RealReloadCompletesWithoutHeldFire()
    {
        Setup(true); Weapon(); yield return null;
        owner.WeaponSystem.Equip(owner.WeaponSystem.CurrentWeapon);
        WeaponAimTestSource.Bind(owner.WeaponSystem, owner.transform);
        owner.WeaponSystem.magazine = 0; owner.WeaponSystem.reserveAmmo = 5;
        var shoot = Bind(new AiShoot { faceTarget = true, returnSuccessWhenTargetLost = false }); shoot.OnStart();
        Assert.AreEqual(TaskStatus.Running, shoot.OnUpdate());
        Assert.IsTrue(owner.WeaponSystem.IsReloading);
        while (owner.WeaponSystem.IsReloading)
        {
            Assert.IsFalse(owner.stateHub.DesiredFireHeld);
            Assert.AreEqual(TaskStatus.Running, shoot.OnUpdate());
            yield return null;
        }
        Assert.Greater(owner.WeaponSystem.CurrentAmmo, 0);
        Assert.AreEqual(TaskStatus.Running, shoot.OnUpdate());
        Assert.IsTrue(owner.stateHub.DesiredFireHeld);
        shoot.OnBehaviorTreeStopped(false);
        Assert.IsFalse(owner.stateHub.DesiredFireHeld);
    }
    public IEnumerator WorldSlowPauseAndMovementHandoff()
    {
        Setup(true); yield return null;
        slowToken = TimeSlowManager.Instance.BeginPresentationSlow(.2f);
        yield return null;
        Assert.That(AICombatTaskUtility.Delta(owner), Is.EqualTo(Time.deltaTime * .2f).Within(.0001f));
        owner.PushWorldSlowExemption();
        Assert.That(AICombatTaskUtility.Delta(owner), Is.EqualTo(Time.deltaTime).Within(.0001f));
        owner.PopWorldSlowExemption();
        pauseToken = GlobalTimeScaleManager.Instance.AcquirePauseToken();
        Assert.AreEqual(0f, AICombatTaskUtility.Delta(owner));
        GlobalTimeScaleManager.Instance.ReleasePauseToken(pauseToken); pauseToken = 0;
        TimeSlowManager.Instance.EndPresentationSlow(slowToken); slowToken = 0;
        state.Value.BeginNavigation("Handoff", sensor.ReadSnapshot.Target, origin + Vector3.right * 10f);
        var navigation = new AICombatNavigation(owner, state.Value);
        Assert.IsTrue(navigation.Command(state.Value.Destination));
        int token = owner.MovementMotor.AcquireAgentSuspension(owner.NavigationAgent);
        navigation.Cleanup();
        Assert.IsFalse(owner.NavigationAgent.updatePosition);
        Assert.IsFalse(owner.NavigationAgent.updateRotation);
        Assert.IsTrue(owner.NavigationAgent.enabled);
        owner.MovementMotor.ReleaseAgentSuspension(token);
    }
    public IEnumerator ExistingMovementOwnershipRegression()
    {
        yield return null;
        var fixture = new CharacterMovementOwnershipTests();
        System.Action[] scenarios = {
            () => fixture.OverlappingOwnersRestoreOnlyAfterLastRelease(0, 1, 2),
            () => fixture.OverlappingOwnersRestoreOnlyAfterLastRelease(0, 2, 1),
            () => fixture.OverlappingOwnersRestoreOnlyAfterLastRelease(1, 0, 2),
            () => fixture.OverlappingOwnersRestoreOnlyAfterLastRelease(1, 2, 0),
            () => fixture.OverlappingOwnersRestoreOnlyAfterLastRelease(2, 0, 1),
            () => fixture.OverlappingOwnersRestoreOnlyAfterLastRelease(2, 1, 0),
            fixture.OriginalStoppedAndAxisSettingsSurvive,
            fixture.StopOnlyPauseDoesNotTakeTransformAxes,
            fixture.AutonomyRequestWaitsForOtherOwners,
            () => fixture.DeferredRestoreSurvivesDisabledOrOffMeshAgent(false),
            () => fixture.DeferredRestoreSurvivesDisabledOrOffMeshAgent(true),
            fixture.InitiallyDisabledAgentCanBeSuspendedBeforeActivation,
            fixture.PoolingAndStaleReleaseDoNotReleaseNewOwner,
            fixture.RebindingNavAdapterRestoresCapturedAgent,
            fixture.GameplayAdaptersShareOneNavigationBaseline,
            fixture.RuntimeContextCreatesOneMotorOutsideReplaceableModel,
            () => fixture.RootMotionYOwnershipFollowsPolicyAndAdapterLifetime(false),
            () => fixture.RootMotionYOwnershipFollowsPolicyAndAdapterLifetime(true)
        };
        foreach (var scenario in scenarios)
        {
            try { fixture.SetUp(); scenario(); }
            finally { fixture.TearDown(); }
        }
    }
}
