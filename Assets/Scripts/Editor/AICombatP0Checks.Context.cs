using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;
using Opsive.BehaviorDesigner.Runtime.Tasks.Composites;
using Opsive.GraphDesigner.Runtime;
using Opsive.GraphDesigner.Runtime.Variables;
using Subtree = Opsive.BehaviorDesigner.Runtime.Wrappers.Subtree;

public sealed partial class AICombatP0Checks
{
    public void ContextCacheRestartAndPool()
    {
        Setup();
        var initialize = Bind(new InitializeAIContext());
        initialize.OnBehaviorTreeStarted();
        Assert.AreEqual(TaskStatus.Success, initialize.OnUpdate());
        Assert.AreSame(owner, context.Value);
        owner.AITargetSensor = null;
        Assert.AreEqual(TaskStatus.Success, initialize.OnUpdate());
        Assert.IsNull(owner.AITargetSensor, "Repeater iterations must not resolve modules again.");
        initialize.OnBehaviorTreeStopped(true);
        initialize.OnBehaviorTreeStarted();
        Assert.AreEqual(TaskStatus.Success, initialize.OnUpdate());
        Assert.IsNull(owner.AITargetSensor, "Pause preserves the same prepared lifetime.");
        initialize.OnBehaviorTreeStopped(false); initialize.OnBehaviorTreeStarted();
        Assert.AreEqual(TaskStatus.Success, initialize.OnUpdate());
        Assert.AreSame(sensor, owner.AITargetSensor);
        owner.AITargetSensor = null;
        int before = owner.LifeGeneration;
        owner.gameObject.SetActive(false); owner.gameObject.SetActive(true);
        // Edit Mode does not dispatch runtime OnEnable. Play Mode below covers the real pool path.
        typeof(CharacteContext).GetMethod("OnEnable", Hidden).Invoke(owner, null);
        Assert.AreNotEqual(before, owner.LifeGeneration);
        Assert.AreEqual(TaskStatus.Success, initialize.OnUpdate());
        Assert.AreSame(sensor, owner.AITargetSensor);
        var canAct = Bind(new CanAct()); canAct.OnBehaviorTreeStarted();
        context.Value = null;
        var previous = state.Value;
        canAct.OnStart(); Assert.AreEqual(TaskStatus.Failure, canAct.OnUpdate());
        Assert.AreSame(previous, state.Value, "No state reset until the initializer supplies Ctx.");
        initialize.OnUpdate(); canAct.OnStart();
        Assert.AreNotSame(previous, state.Value);
        previous = state.Value; canAct.OnStart(); canAct.OnUpdate();
        Assert.AreSame(previous, state.Value, "CanAct must preserve state within a lifetime.");
    }

    public void ContextFailuresNeverDiscoverOwner()
    {
        Setup();
        var ready = Bind(new CanAct());
        context.Value = null;
        ready.OnStart(); Assert.AreEqual(TaskStatus.Failure, ready.OnUpdate());
        Assert.IsNull(AIContextBinding.Read(null));
        context.Value = owner; context.Name = "Wrong";
        Assert.AreEqual(TaskStatus.Failure, ready.OnUpdate());
        context.Name = "Ctx"; context.Scope = SharedVariable.SharingScope.GameObject;
        Assert.AreEqual(TaskStatus.Failure, ready.OnUpdate());
        context.Scope = SharedVariable.SharingScope.Graph;
        owner.AITargetSensor = null;
        var engage = Bind(new CanEngageTarget { target = target }); engage.OnStart();
        Assert.AreEqual(TaskStatus.Failure, engage.OnUpdate());
        Assert.IsNull(owner.AITargetSensor, "The sensor component exists but consumers cannot discover it.");
        var cast = Bind(new TryCastSkillFromSlot()); cast.OnStart();
        Assert.AreEqual(TaskStatus.Failure, cast.OnUpdate()); cast.OnEnd();
        Assert.AreEqual(TaskStatus.Failure, Bind(new SkillReady()).OnUpdate());
        Assert.AreEqual(TaskStatus.Failure, Bind(new TriggerSpecialShootPointRound()).OnUpdate());
        Assert.AreEqual(TaskStatus.Failure, Bind(new IsSpecialShootPointRoundActive()).OnUpdate());
        Assert.AreEqual(TaskStatus.Failure, Bind(new CompareSpecialShootPointRoundOutcome()).OnUpdate());
        var orphan = Own(new GameObject("Orphan behavior owner"));
        var initialize = Bind(new InitializeAIContext());
        Set(initialize, "m_GameObject", orphan); Set(initialize, "m_Transform", orphan.transform);
        LogAssert.Expect(LogType.Error, "[InitializeAIContext] No active owning CharacteContext.");
        Assert.AreEqual(TaskStatus.Failure, initialize.OnUpdate());
        Assert.IsNull(context.Value);
        Assert.AreEqual(TaskStatus.Failure, initialize.OnUpdate());
    }

    static int SensorSubscriptions(AITargetSensor value) =>
        (typeof(AITargetSensor).GetField("OnTargetChanged", Hidden)?.GetValue(value) as Delegate)?
        .GetInvocationList().Count(d => d.Method.Name == "HandleSensorTargetChanged") ?? 0;

    public void ContextEventsRebindWithoutDuplicates()
    {
        Setup();
        var output = new SharedVariable<GameObject>();
        var hasTarget = Bind(new HasEnemyFromSensor { currentEnemy = output, InCombat = new SharedVariable<bool>() });
        context.Value = null; hasTarget.OnAwake(); hasTarget.OnBehaviorTreeStarted();
        Assert.AreEqual(0, SensorSubscriptions(sensor));
        context.Value = owner;
        for (int i = 0; i < 5; i++) { hasTarget.OnStart(); hasTarget.OnUpdate(); }
        Assert.AreEqual(1, SensorSubscriptions(sensor));
        var replacementObject = new GameObject("Replacement sensor");
        replacementObject.transform.SetParent(owner.transform, false);
        var replacement = replacementObject.AddComponent<AITargetSensor>();
        Set(replacement, "targetLayers", (LayerMask)(1 << 30));
        owner.AITargetSensor = replacement;
        typeof(AITargetSensor).GetMethod("Awake", Hidden).Invoke(replacement, null);
        replacement.ForceScan();
        hasTarget.OnUpdate();
        Assert.AreEqual(0, SensorSubscriptions(sensor));
        Assert.AreEqual(1, SensorSubscriptions(replacement));
        hasTarget.OnBehaviorTreeStopped(true);
        Assert.AreEqual(0, SensorSubscriptions(replacement));
        hasTarget.OnStart(); hasTarget.OnUpdate();
        Assert.AreEqual(1, SensorSubscriptions(replacement));
        hasTarget.OnDestroy(); Assert.AreEqual(0, SensorSubscriptions(replacement));
    }

    public void ContextValidatorRejectsUnsafeGraphs()
    {
        var graph = Own(ScriptableObject.CreateInstance<Subtree>());
        graph.SharedVariables = new SharedVariable[] { AIContextGraphAuthoring.Binding() };
        graph.TreeLogicNodes = new ITreeLogicNode[] {
            new Sequence { Index = 0, ParentIndex = ushort.MaxValue, SiblingIndex = ushort.MaxValue },
            new StackedAction { Index = 1, ParentIndex = 0, SiblingIndex = 2,
                Tasks = new Task[] { new AICombatWait { context = AIContextGraphAuthoring.Binding() } } },
            new StackedAction { Index = 2, ParentIndex = 0, SiblingIndex = ushort.MaxValue,
                Tasks = new Task[] { new InitializeAIContext { context = AIContextGraphAuthoring.Binding() } } }
        };
        Assert.IsTrue(AIContextGraphAuthoring.Validate(graph).Any(e => e.Contains("reachable before")));
        graph.SharedVariables = new SharedVariable[] { new SharedVariable<GameObject> { Name = "Ctx" } };
        Assert.IsTrue(AIContextGraphAuthoring.Validate(graph).Any(e => e.Contains("exactly one")));
        var wait = (AICombatWait)((StackedAction)graph.TreeLogicNodes[1]).Tasks[0];
        wait.context = null;
        Assert.IsTrue(AIContextGraphAuthoring.Validate(graph).Any(e => e.Contains("must bind")));
        foreach (var type in new[] { typeof(AllyContext), typeof(EnemyContext), typeof(SummonContext) })
            foreach (var name in new[] { "AITargetSensor", "AgentMoveDriver", "AimAxisDriver" })
                Assert.AreEqual(typeof(CharacteContext), type.GetField(name).DeclaringType);
    }

    public void ContextInitializerOrderSurvivesGraphSave()
    {
        var graph = Own(ScriptableObject.CreateInstance<Subtree>());
        var repeatGuid = Guid.NewGuid().ToString();
        var waitGuid = Guid.NewGuid().ToString();
        graph.TreeLogicNodes = new ITreeLogicNode[] {
            new Opsive.BehaviorDesigner.Runtime.Tasks.Decorators.Repeater {
                Index = 0, ParentIndex = ushort.MaxValue, SiblingIndex = ushort.MaxValue },
            new StackedAction { Index = 1, ParentIndex = 0, SiblingIndex = ushort.MaxValue,
                Tasks = new Task[] { new AICombatWait { duration = 8f } } }
        };
        graph.LogicNodeProperties = new[] {
            new LogicNodeProperties { GuidString = repeatGuid, ConnectionGuidStrings = new[] { waitGuid } },
            new LogicNodeProperties { GuidString = waitGuid, Position = new Vector2(-400f, 280f) }
        };
        graph.EventNodes = new IEventNode[] {
            new Opsive.BehaviorDesigner.Runtime.Tasks.Events.Start { Index = 0, ConnectedIndex = 0 } };
        graph.EventNodeProperties = new[] { new NodeProperties {
            GuidString = Guid.NewGuid().ToString(), ConnectionGuidStrings = new[] { repeatGuid } } };
        Assert.IsTrue(AIContextGraphAuthoring.Migrate(graph));
        var init = graph.TreeLogicNodes[2];
        var wait = graph.TreeLogicNodes[3];
        var initProps = graph.LogicNodeProperties[2];
        var waitProps = graph.LogicNodeProperties[3];
        Assert.Less(initProps.Position.x, waitProps.Position.x,
            "An existing behavior left of zero must not sort ahead of initialization on graph save.");
        // Reproduce the production graph after the initializer was laid out to the right.
        initProps.Position = waitProps.Position + Vector2.right * 260f;
        Assert.IsTrue(AIContextGraphAuthoring.Validate(graph).Any(e => e.Contains("leftmost")));
        graph.TreeLogicNodes = new[] { graph.TreeLogicNodes[0], graph.TreeLogicNodes[1], wait, init };
        graph.LogicNodeProperties = new[] { graph.LogicNodeProperties[0], graph.LogicNodeProperties[1], waitProps, initProps };
        wait.Index = 2; wait.SiblingIndex = 3;
        wait.Enabled = false;
        init.Index = 3; init.SiblingIndex = ushort.MaxValue;
        graph.DisabledLogicNodes = new ushort[] { 2 };
        var guids = graph.LogicNodeProperties.Select(p => p.GuidString).ToArray();
        Assert.IsTrue(AIContextGraphAuthoring.Validate(graph).Any(e => e.Contains("reachable before")));
        Assert.IsTrue(AIContextGraphAuthoring.Migrate(graph));
        Assert.IsFalse(AIContextGraphAuthoring.Migrate(graph), "Repair must be idempotent.");
        CollectionAssert.AreEqual(new ushort[] { 3 }, graph.DisabledLogicNodes);
        graph.Serialize(); Assert.IsTrue(graph.Deserialize(true));
        Assert.IsEmpty(AIContextGraphAuthoring.Validate(graph));
        CollectionAssert.AreEquivalent(guids, graph.LogicNodeProperties.Select(p => p.GuidString));
        Assert.IsInstanceOf<InitializeAIContext>(((StackedAction)graph.TreeLogicNodes[2]).Tasks[0]);
        Assert.AreEqual(8f, ((AICombatWait)((StackedAction)graph.TreeLogicNodes[3]).Tasks[0]).duration);
        Assert.IsFalse(graph.TreeLogicNodes[3].Enabled);
    }

    public void ContextSpecialPointBindings()
    {
        Setup();
        var moduleObject = new GameObject("Special point module");
        moduleObject.transform.SetParent(owner.transform, false);
        var controller = moduleObject.AddComponent<SpecialShootPointController>();
        owner.SpecialShootPoints = controller;
        Set(controller, "_phase", SpecialShootPointPhase.Active);
        Set(controller, "_currentRequestId", 7);
        var active = Bind(new IsSpecialShootPointRoundActive()); active.OnStart();
        Assert.AreEqual(TaskStatus.Success, active.OnUpdate());
        Assert.AreEqual(active.OnUpdate(), active.OnReevaluateUpdate());
        var outcome = Bind(new CompareSpecialShootPointRoundOutcome()); outcome.OnStart();
        Set(controller, "_lastOutcome", SpecialShootPointOutcome.Succeeded);
        Set(controller, "_lastOutcomeRequestId", 6);
        Assert.AreEqual(TaskStatus.Failure, outcome.OnUpdate());
        Set(controller, "_lastOutcomeRequestId", 7);
        Assert.AreEqual(TaskStatus.Success, outcome.OnUpdate());
        Assert.AreEqual(outcome.OnUpdate(), outcome.OnReevaluateUpdate());
        owner.SpecialShootPoints = null;
        Assert.AreEqual(TaskStatus.Failure, active.OnUpdate());
        Assert.AreEqual(TaskStatus.Failure, outcome.OnUpdate());
        Assert.IsNull(owner.SpecialShootPoints, "Task cannot rediscover an existing unbound module.");
    }

    public IEnumerator ContextFormationControlAndAbort()
    {
        Setup(true); yield return null;
        opponent.NavigationAgent = opponent.gameObject.AddComponent<UnityEngine.AI.NavMeshAgent>();
        opponent.NavigationAgent.speed = 4f;
        opponent.CharacterLoad = opponent.gameObject.AddComponent<CharacterContextPartyLoader>();
        opponent.CharacterLoad.ConfigurePartyIndex(1);
        opponent.ResolveReferences();
        var playerObject = Own(new GameObject("Formation player target"));
        playerObject.SetActive(false); playerObject.transform.position = origin;
        var playerContext = playerObject.AddComponent<PlayerContext>();
        playerContext.partyFormation = playerObject.AddComponent<PartyFormationController>();
        Set(playerContext.partyFormation, "playerContext", playerContext);
        context.Value = opponent;
        var inCombat = new SharedVariable<bool> { Value = true };
        var follow = Bind(new MoveToFormationSlotNavMesh { player = new SharedVariable<GameObject> { Value = playerObject }, InCombat = inCombat });
        float baseline = opponent.NavigationAgent.stoppingDistance;
        follow.OnStart(); Assert.AreEqual(TaskStatus.Failure, follow.OnUpdate());
        inCombat.Value = false;
        Assert.AreEqual(TaskStatus.Running, follow.OnUpdate());
        Assert.AreEqual(playerContext.partyFormation.StopDistance, opponent.NavigationAgent.stoppingDistance);
        follow.OnEnd();
        Assert.AreEqual(baseline, opponent.NavigationAgent.stoppingDistance);
        Assert.IsTrue(opponent.NavigationAgent.isStopped);
        follow.OnStart(); follow.OnUpdate();
        int token = opponent.MovementMotor.AcquireAgentSuspension(opponent.NavigationAgent);
        follow.OnUpdate(); follow.OnEnd();
        Assert.IsFalse(opponent.NavigationAgent.updatePosition);
        Assert.IsFalse(opponent.NavigationAgent.updateRotation);
        opponent.MovementMotor.ReleaseAgentSuspension(token);
        context.Value = null;
        follow.OnStart(); Assert.AreEqual(TaskStatus.Failure, follow.OnUpdate()); follow.OnEnd();
    }

    public IEnumerator ContextNestedTreesAndLifecycle()
    {
        Setup(true, true); yield return null;
        var child = AssetDatabase.LoadAssetAtPath<Subtree>(NestedChildPath);
        var parent = AssetDatabase.LoadAssetAtPath<Subtree>(NestedParentPath);
        Assert.IsNotNull(parent, "Prepare the native nested fixture before Play Mode.");
        parent.Deserialize(true); child.Deserialize(true);
        var first = owner.TargetInfo.gameObject.AddComponent<Opsive.BehaviorDesigner.Runtime.Wrappers.BehaviorTree>();
        first.StartWhenEnabled = false; first.Subgraph = parent;
        var second = opponent.gameObject.AddComponent<Opsive.BehaviorDesigner.Runtime.Wrappers.BehaviorTree>();
        second.StartWhenEnabled = false; second.Subgraph = parent;
        bool firstStarted = first.StartBehavior();
        bool secondStarted = second.StartBehavior();
        Assert.IsTrue(firstStarted, "First nested tree start: " + first.Status + "; nodes=" + first.TreeLogicNodes?.Length +
            "; parent=" + parent.TreeLogicNodes?.Length + "; child=" + child.TreeLogicNodes?.Length);
        Assert.IsTrue(secondStarted, "Second nested tree start: " + second.Status);
        yield return null; first.Tick(); second.Tick();
        Assert.AreSame(owner, first.GetVariable<CharacteContext>("Ctx").Value);
        Assert.AreSame(opponent, second.GetVariable<CharacteContext>("Ctx").Value);
        Assert.AreNotSame(first.GetVariable<CharacteContext>("Ctx"), second.GetVariable<CharacteContext>("Ctx"));
        var nestedWait = first.TreeLogicNodes.SelectMany(AIContextGraphAuthoring.Tasks).OfType<AICombatWait>().Single();
        Assert.AreSame(owner, nestedWait.context.Value, "Native nested override receives this actor's context.");
        first.StopBehavior(true); Assert.AreSame(owner, first.GetVariable<CharacteContext>("Ctx").Value);
        Assert.IsTrue(first.StartBehavior()); yield return null; first.Tick();
        Assert.AreSame(owner, nestedWait.context.Value);
        first.StopBehavior(false); Assert.IsTrue(first.StartBehavior()); yield return null; first.Tick();
        Assert.AreSame(owner, first.GetVariable<CharacteContext>("Ctx").Value);
        var wait = Bind(new AICombatWait { duration = 10f }); wait.OnStart();
        int generation = owner.LifeGeneration;
        owner.gameObject.SetActive(false); owner.gameObject.SetActive(true); yield return null;
        Assert.AreNotEqual(generation, owner.LifeGeneration);
        Assert.AreEqual(TaskStatus.Failure, wait.OnUpdate(), "A running action cannot cross pooled lifetimes.");
        first.StopBehavior(false); second.StopBehavior(false);
    }
    public const string NestedParentPath = "Assets/__AIContextValidationTemp/NestedParent.asset";
    public const string NestedChildPath = "Assets/__AIContextValidationTemp/NestedChild.asset";
    public static void PrepareNestedContextGraphFixture()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Author the native fixture before Play Mode.");
        if (!AssetDatabase.IsValidFolder("Assets/__AIContextValidationTemp"))
            AssetDatabase.CreateFolder("Assets", "__AIContextValidationTemp");
        AssetDatabase.DeleteAsset(NestedParentPath);
        AssetDatabase.DeleteAsset(NestedChildPath);
        var child = ScriptableObject.CreateInstance<Subtree>();
        AssetDatabase.CreateAsset(child, NestedChildPath);
        child.TreeLogicNodes = new ITreeLogicNode[] { new StackedAction { Index = 0, ParentIndex = ushort.MaxValue,
            SiblingIndex = ushort.MaxValue, Tasks = new Task[] { new AICombatWait { context = AIContextGraphAuthoring.Binding(), duration = 10f } } } };
        child.LogicNodeProperties = new[] { new LogicNodeProperties { GuidString = Guid.NewGuid().ToString() } };
        child.SharedVariables = new SharedVariable[] { AIContextGraphAuthoring.Binding() };
        child.EventNodes = new IEventNode[] { new Opsive.BehaviorDesigner.Runtime.Tasks.Events.Start { Index = 0, ConnectedIndex = 0, Enabled = true } };
        child.EventNodeProperties = new[] { new NodeProperties { GuidString = Guid.NewGuid().ToString(),
            ConnectionGuidStrings = new[] { child.LogicNodeProperties[0].GuidString } } };
        AIContextGraphAuthoring.Migrate(child); child.Serialize();
        var parent = ScriptableObject.CreateInstance<Subtree>();
        AssetDatabase.CreateAsset(parent, NestedParentPath);
        parent.TreeLogicNodes = new ITreeLogicNode[] {
            new Sequence { Index = 0, ParentIndex = ushort.MaxValue, SiblingIndex = ushort.MaxValue },
            new StackedAction { Index = 1, ParentIndex = 0, SiblingIndex = 2,
                Tasks = new Task[] { new InitializeAIContext { context = AIContextGraphAuthoring.Binding() } } },
            new SubtreeReference { Index = 2, ParentIndex = 0, SiblingIndex = ushort.MaxValue,
                SharedVariableOverrides = new[] { new SharedVariableOverride { Source = child.SharedVariables[0],
                    Override = AIContextGraphAuthoring.Binding(), SourceGraph = child } } }
        };
        Set(parent.TreeLogicNodes[2], "m_Subtrees", new Opsive.BehaviorDesigner.Runtime.Subtree[] { child });
        parent.LogicNodeProperties = parent.TreeLogicNodes.Select(n => new LogicNodeProperties { GuidString = Guid.NewGuid().ToString() }).ToArray();
        parent.LogicNodeProperties[1].Position = new Vector2(-260f, 140f);
        parent.LogicNodeProperties[2].Position = new Vector2(0f, 140f);
        parent.LogicNodeProperties[0].ConnectionGuidStrings = new[] { parent.LogicNodeProperties[1].GuidString, parent.LogicNodeProperties[2].GuidString };
        parent.SharedVariables = new SharedVariable[] { AIContextGraphAuthoring.Binding() };
        parent.EventNodes = new IEventNode[] { new Opsive.BehaviorDesigner.Runtime.Tasks.Events.Start { Index = 0, ConnectedIndex = 0, Enabled = true } };
        parent.EventNodeProperties = new[] { new NodeProperties { GuidString = Guid.NewGuid().ToString(),
            ConnectionGuidStrings = new[] { parent.LogicNodeProperties[0].GuidString } } };
        parent.Serialize();
        Assert.IsTrue(parent.Deserialize(true)); Assert.IsEmpty(AIContextGraphAuthoring.Validate(parent));

        EditorUtility.SetDirty(child); EditorUtility.SetDirty(parent); AssetDatabase.SaveAssets();
    }
    public static void CleanupNestedContextGraphFixture()
    {
        AssetDatabase.DeleteAsset(NestedParentPath);
        AssetDatabase.DeleteAsset(NestedChildPath);
        if (AssetDatabase.IsValidFolder("Assets/__AIContextValidationTemp") &&
            AssetDatabase.FindAssets("", new[] { "Assets/__AIContextValidationTemp" }).Length == 0)
            AssetDatabase.DeleteAsset("Assets/__AIContextValidationTemp");
    }
}
