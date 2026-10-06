using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Opsive.BehaviorDesigner.Runtime;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class AllyEnemyPrefabGroupingFixture
{
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    readonly List<Object> owned = new();
    static readonly string[] Paths = {
        "Assets/Prefab/Player/Ally_Helper.prefab", "Assets/Prefab/Player/Ally_Stryker.prefab",
        "Assets/Prefab/GameEnemy/Enemy_Base.prefab", "Assets/Prefab/GameEnemy/Enemy_B_GR_01 Variant.prefab",
        "Assets/Prefab/GameEnemy/Enemy_E_GR_01 Variant.prefab", "Assets/Prefab/GameEnemy/Enemy_M_GR_01 Variant.prefab",
        "Assets/Prefab/GameEnemy/Enemy_M_GR_02 Variant.prefab" };
    static readonly string[] Groups = { "GamePlayStats_System", "Movement_System", "Combat_System",
        "CharacterVisual_System", "Character_Equipment", "AI_System" };
    T Own<T>(T value) where T : Object { owned.Add(value); return value; }
    static object Field(object obj, string name) => obj.GetType().GetField(name, Hidden).GetValue(obj);
    static void Set(object obj, string name, object value) => obj.GetType().GetField(name, Hidden).SetValue(obj, value);
    static object Call(object obj, string name) => obj.GetType().GetMethod(name, Hidden).Invoke(obj, null);

    CharacteContext Create(int index)
    {
        var parent = Own(new GameObject("Grouping fixture"));
        parent.SetActive(false);
        var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Paths[index]), parent.transform);
        var ctx = root.GetComponent<CharacteContext>();
        ctx.ResolveReferences();
        return ctx;
    }

    public void Cleanup()
    {
        for (int i = owned.Count - 1; i >= 0; i--)
            if (owned[i]) Object.DestroyImmediate(owned[i]);
        owned.Clear();
    }

    public void Layout(int index)
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(Paths[index]);
        bool ally = index < 2;
        var systems = root.transform.Find("------------ " + (ally ? "Ally" : "Enemy") + "_System ------------");
        Assert.That(systems, Is.Not.Null);
        Assert.That(systems.Cast<Transform>().Select(t => t.name), Is.EqualTo(Groups));
        string[] rootTypes = ally
            ? new[]{"Transform","AllyContext","NavMeshAgent","CharacterController","Rigidbody","Animator","CharacterMovementMotor","AITargetInfo"}
            : new[]{"Transform","EnemyContext","NavMeshAgent","Rigidbody","Animator","CharacterMovementMotor","AITargetInfo"};
        Assert.That(root.GetComponents<Component>().Select(c => c.GetType().Name), Is.EquivalentTo(rootTypes));
        Assert.That(root.GetComponentsInChildren<Component>(true).All(c => c != null), Is.True);
        foreach (Transform group in systems)
        {
            Assert.That(group.localPosition, Is.EqualTo(Vector3.zero));
            Assert.That(group.localRotation, Is.EqualTo(Quaternion.identity));
            Assert.That(group.localScale, Is.EqualTo(Vector3.one));
        }
        Assert.That(root.GetComponentsInChildren<CharacterMovementMotor>(true).Length, Is.EqualTo(1));
        Assert.That(root.GetComponentsInChildren<StateHub>(true).Length, Is.EqualTo(1));
        Assert.That(root.GetComponentsInChildren<DefensiveBlockAttack>(true).Length, Is.EqualTo(ally ? 0 : 1));
        Assert.That(root.GetComponentsInChildren<DefensiveBlockController>(true).Length, Is.EqualTo(ally ? 1 : 0));
        if (index > 2)
        {
            Assert.That(PrefabUtility.GetPrefabAssetType(root), Is.EqualTo(PrefabAssetType.Variant));
            Assert.That(AssetDatabase.GetAssetPath(PrefabUtility.GetCorrespondingObjectFromSource(root)), Is.EqualTo(Paths[2]));
        }
        foreach (var tree in root.GetComponentsInChildren<BehaviorTree>(true))
            Assert.That(tree.gameObject, Is.SameAs(systems.Find("AI_System").gameObject));
        var ctx = Create(index);
        Assert.That(ctx.stateHub.gameObject.name, Is.EqualTo(Groups[0]));
        Assert.That(ctx.AnimBrain.gameObject.name, Is.EqualTo(Groups[3]));
        Assert.That(ctx.AnimBrain.GetComponent<Animancer.AnimancerComponent>(), Is.Not.Null);
        Assert.That(ctx.stateHub.GetComponent<StateHubDebugView>(), Is.Not.Null);
        Assert.That(ctx.Equipment.gameObject.name, Is.EqualTo(Groups[4]));
        Assert.That(ctx.AgentMoveDriver.gameObject.name, Is.EqualTo(Groups[1]));
        Assert.That(ctx.SkillManager.gameObject.name, Is.EqualTo(Groups[2]));
        foreach (var field in ctx.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            if (field.GetValue(ctx) is Component peer)
                Assert.That(CharacterContextModuleLookup.ResolveContext(peer), Is.SameAs(ctx), field.Name);
    }

    public void InactiveContextAndAgentResolution(int index)
    {
        var ctx = Create(index);
        var expected = new Component[]{ctx.stateHub, ctx.StatsHub, ctx.HealthSystem, ctx.AITargetSensor,
            ctx.AgentMoveDriver, ctx.AnimBrain, ctx.AnimDriver, ctx.SkillManager, ctx.Equipment};
        var neighbour = new GameObject("Other actor"); neighbour.transform.SetParent(ctx.transform.parent, false);
        var foreign = neighbour.AddComponent<EnemyContext>(); neighbour.AddComponent<StateHub>();
        foreign.ResolveReferences();
        foreach (var field in ctx.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public))
            if (typeof(Component).IsAssignableFrom(field.FieldType)) field.SetValue(ctx, null);
        ctx.ResolveReferences();
        foreach (var peer in expected)
            Assert.That(ctx.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public).Any(f => ReferenceEquals(f.GetValue(ctx), peer)), Is.True, peer.GetType().Name);
        Set(ctx.AgentMoveDriver, "ctx", null);
        Set(ctx.AgentMoveDriver, "stateHub", null);
        Call(ctx.AgentMoveDriver, "Awake");
        Assert.That(Field(ctx.AgentMoveDriver, "stateHub"), Is.SameAs(ctx.stateHub));
        Assert.That(Field(ctx.AgentMoveDriver, "ctx"), Is.SameAs(ctx));
        if (ctx is EnemyContext enemy)
        {
            Assert.That(enemy.dropper, Is.Not.Null);
            Assert.That(enemy.StaggerMeter, Is.Not.Null);
            foreach (string name in new[]{"animBrain","animDriver","healthSystem","navMeshAgent","behaviorTree"}) Set(enemy.StaggerMeter, name, null);
            Call(enemy.StaggerMeter, "ResolveRefs");
            Assert.That(Field(enemy.StaggerMeter, "animBrain"), Is.SameAs(ctx.AnimBrain));
            Assert.That(Field(enemy.StaggerMeter, "animDriver"), Is.SameAs(ctx.AnimDriver));
            Assert.That(Field(enemy.StaggerMeter, "navMeshAgent"), Is.SameAs(ctx.NavigationAgent));
            if (index > 2) Assert.That(Field(enemy.StaggerMeter, "behaviorTree"), Is.SameAs(ctx.GetComponentInChildren<BehaviorTree>(true)));
        }
    }

    public void RootAndSiblingEnemyLevelNotifyOnlyOwner()
    {
        foreach (bool nested in new[]{false, true})
        {
            var parent = Own(new GameObject("Level actors")); parent.SetActive(false);
            var root = new GameObject("Owner"); root.transform.SetParent(parent.transform, false);
            var ctx = root.AddComponent<EnemyContext>();
            var stats = new GameObject("Stats"); stats.transform.SetParent(root.transform, false);
            ctx.StatsHub = stats.AddComponent<StatsHub>();
            var levelHost = root;
            if (nested) { levelHost = new GameObject("Level"); levelHost.transform.SetParent(root.transform, false); }
            var level = levelHost.AddComponent<EnemyLevelSystem>();
            var foreignHost = new GameObject("Neighbour"); foreignHost.transform.SetParent(parent.transform, false);
            var foreign = foreignHost.AddComponent<EnemyContext>(); foreign.StatsHub = foreignHost.AddComponent<StatsHub>();
            int ownerDirty = 0, foreignDirty = 0;
            ctx.StatsHub.StatsDirty += () => ownerDirty++;
            foreign.StatsHub.StatsDirty += () => foreignDirty++;
            level.SetLevel(11);
            Assert.That(ownerDirty, Is.EqualTo(1)); Assert.That(foreignDirty, Is.Zero);
            Assert.That(ctx.EnemyLevelSystem, Is.SameAs(level));
            level.SetLevel(11); Assert.That(ownerDirty, Is.EqualTo(1));
        }
    }

    public void DropUsesOwnerPoseAndVariantLevel()
    {
        var ctx = (EnemyContext)Create(3);
        ctx.transform.position = new Vector3(12, 3, 7);
        ctx.dropper.transform.localPosition = new Vector3(30, 4, 9);
        Assert.That(typeof(EnemyDropper).GetProperty("DropPosition", Hidden).GetValue(ctx.dropper), Is.EqualTo(ctx.transform.position));
        ctx.EnemyLevelSystem.SetLevel(17);
        Assert.That(Call(ctx.dropper, "ResolveEnemyLevel"), Is.EqualTo(17));
    }

    public void HelperCachesNestedBehaviorTree()
    {
        var helper = (AllyContext)Create(0);
        var host = Own(new GameObject("Helper manager")); host.SetActive(false);
        var manager = host.AddComponent<AllyHelperManager>();
        Set(manager, "allyHelper", helper.gameObject);
        Call(manager, "CacheHelperReferences");
        Assert.That(Field(manager, "allyBehaviorTree"), Is.SameAs(helper.BehaviorTree));
        Assert.That(Field(manager, "allyAnimBrain"), Is.SameAs(helper.AnimBrain));
        Assert.That(Field(manager, "allyHealthSystem"), Is.SameAs(helper.HealthSystem));
    }

    public void AuthoringUsesExistingCombatGroup()
    {
        var method = typeof(DefensiveBlockProductionAuthoring).GetMethod("CombatHost", BindingFlags.Static | BindingFlags.NonPublic);
        foreach (int index in new[]{0,1,2,3})
        {
            var ctx = Create(index);
            Assert.That(((GameObject)method.Invoke(null, new object[]{ctx,index < 2 ? "Ally" : "Enemy"})).name, Is.EqualTo("Combat_System"));
        }
    }

    public IEnumerator RuntimeProductionBindingsAndReactivation()
    {
        // All production actors load their authored systems, models and AI in an isolated owned hierarchy.
        if (!Object.FindAnyObjectByType<TimeSlowManager>()) Own(new GameObject("Grouping time")).AddComponent<TimeSlowManager>();
        for (int index = 0; index < Paths.Length; index++)
        {
            var ctx = Create(index);
            var originalMotor = ctx.MovementMotor;
            var originalState = ctx.stateHub;
            if (ctx is AllyContext)
                ctx.CharacterLoad.ConfigureRuntimeDefinitionOverride(AssetDatabase.LoadAssetAtPath<CharacterStats>(
                    "Assets/Scripts/CharacterStats/Asosiation/ChaDef.Aires.asset"));
            // No test NavMesh is available here; navigation behavior is covered by AI/movement suites.
            ctx.NavigationAgent.enabled = false;
            if (ctx.VerticalMotor) ctx.VerticalMotor.enabled = false;
            foreach (var tree in ctx.GetComponentsInChildren<BehaviorTree>(true)) tree.enabled = false;
            ctx.transform.parent.gameObject.SetActive(true);
            yield return null;
            yield return null;
            Assert.That(ctx.stateHub, Is.Not.Null);
            Assert.That(ctx.AnimBrain, Is.Not.Null);
            Assert.That(ctx.AnimDriver, Is.Not.Null);
            Assert.That(ctx.HealthSystem, Is.Not.Null);
            Assert.That(Field(ctx.AgentMoveDriver, "stateHub"), Is.SameAs(ctx.stateHub));
            Assert.That(ctx.SkillManager.gameObject.name, Is.EqualTo("Combat_System"));
            if (ctx is AllyContext ally)
            {
                Assert.That(ally.BehaviorTree.gameObject.name, Is.EqualTo("AI_System"));
                Assert.That(ctx.Visibility, Is.Not.Null);
                ctx.Visibility.SetHiddenImmediate(); yield return null;
                Assert.That(ctx.gameObject.activeSelf, Is.True);
                ctx.Visibility.SetVisibleImmediate();
            }
            if (ctx is EnemyContext enemy && index > 2)
            {
                Assert.That(enemy.EnemyLevelSystem.gameObject.name, Is.EqualTo("GamePlayStats_System"));
                enemy.EnemyLevelSystem.SetLevel(17);
                Assert.That(Call(enemy.dropper, "ResolveEnemyLevel"), Is.EqualTo(17));
                if (enemy.SpecialShootPoints)
                {
                    var points = enemy.SpecialShootPoints;
                    Assert.That(points.gameObject.name, Is.EqualTo("Combat_System"));
                    Assert.That(points.Context, Is.SameAs(enemy));
                    Assert.That(points.Meter, Is.SameAs(enemy.StaggerMeter));
                    Assert.That(points.OwnerDamageable, Is.SameAs(enemy.HealthSystem));
                    Assert.That(points.TryStartRound(), Is.True, enemy.name);
                    Assert.That(points.ActivePoints.Count, Is.GreaterThan(0), enemy.name);
                    float deadline = Time.realtimeSinceStartup + points.Profile.telegraphDuration + 3f;
                    while (points.Phase == SpecialShootPointPhase.Telegraph && Time.realtimeSinceStartup < deadline)
                        yield return null;
                    Assert.That(points.Phase, Is.EqualTo(SpecialShootPointPhase.Active), enemy.name);
                    Assert.That(points.ActivePoints.All(p => p.IsBound && p.IsHittable), Is.True, enemy.name);
                    points.CancelRound(startCooldown: false);
                    Assert.That(points.IsRoundActive, Is.False, enemy.name);
                }
            }
            ctx.gameObject.SetActive(false);
            ctx.gameObject.SetActive(true);
            yield return null;
            Assert.That(ctx.MovementMotor, Is.SameAs(originalMotor), ctx.name);
            Assert.That(ctx.stateHub, Is.SameAs(originalState), ctx.name);
            Assert.That(ctx.GetComponents<CharacterMovementMotor>().Length, Is.EqualTo(1), ctx.name);
            Assert.That(ctx.stateHub.GetComponents<StateHubDebugView>().Length, Is.EqualTo(1), ctx.name);
            // Enemy_Base's existing model definition instantiates a complete M_GR_01 actor.
            // Count the owner's modules and verify binding identity; that nested actor has its own motor.
            foreach (var motor in ctx.GetComponentsInChildren<CharacterMovementMotor>(true).Where(m => m != originalMotor))
            {
                var owner = CharacterContextModuleLookup.ResolveContext(motor);
                Assert.That(owner, Is.Not.Null, motor.name);
                Assert.That(owner, Is.Not.SameAs(ctx), motor.name);
                Assert.That(owner.MovementMotor, Is.SameAs(motor), motor.name);
            }
            ctx.transform.parent.gameObject.SetActive(false);
        }
    }
}
