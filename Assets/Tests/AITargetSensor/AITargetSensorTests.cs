using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class AITargetSensorTests
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    readonly List<Object> created = new List<Object>();
    readonly List<Component> actors = new List<Component>();
    Component owner;
    Component sensor;
    static Type T(string name) => Type.GetType(name + ", Assembly-CSharp", true);
    static FieldInfo Field(Type type, string name)
    {
        for (; type != null; type = type.BaseType)
        {
            FieldInfo field = type.GetField(name, Flags);
            if (field != null) return field;
        }
        throw new MissingFieldException(name);
    }
    static void Set(object o, string name, object value) => Field(o.GetType(), name).SetValue(o, value);
    static object Get(object o, string name)
    {
        PropertyInfo property = o.GetType().GetProperty(name, Flags);
        return property != null ? property.GetValue(o) : Field(o.GetType(), name).GetValue(o);
    }
    static object Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, Flags).Invoke(o, args);
    U Track<U>(U value) where U : Object { created.Add(value); return value; }

    Component Actor(string type, Vector3 position, bool nested = false)
    {
        var go = Track(new GameObject(type));
        go.transform.position = position;
        go.layer = 30;
        var ctx = go.AddComponent(T(type));
        GameObject infoGo = go;
        if (nested)
        {
            infoGo = new GameObject("AI_System");
            infoGo.transform.SetParent(go.transform, false);
        }
        var info = infoGo.AddComponent(T("AITargetInfo"));
        Set(ctx, "TargetInfo", info);
        var aim = new GameObject("Aim").transform;
        aim.SetParent(go.transform, false);
        aim.localPosition = Vector3.up * .5f;
        Set(info, "aimPoint", aim);
        actors.Add(ctx);
        T("CharacterContextRegistry").GetMethod("Register").Invoke(null, new object[] {ctx});
        return ctx;
    }

    [SetUp]
    public void Setup()
    {
        owner = Actor("EnemyContext", new Vector3(0, 0, 20000));
        sensor = owner.gameObject.AddComponent(T("AITargetSensor"));
        Call(sensor, "Awake");
        Set(sensor, "targetLayers", (LayerMask)(1 << 30));
        Set(sensor, "obstacleLayers", (LayerMask)(1 << 29));
        Set(sensor, "currentTargetStickiness", 0f);
    }

    [TearDown]
    public void Cleanup()
    {
        if (sensor != null) Call(sensor, "OnDisable");
        foreach (Component actor in actors)
            if (actor != null) T("CharacterContextRegistry").GetMethod("Unregister").Invoke(null, new object[]{actor});
        actors.Clear();
        for (int i = created.Count - 1; i >= 0; i--)
            if (created[i] != null) Object.DestroyImmediate(created[i]);
        created.Clear();
    }

    Component Target(float distance = 3f, bool nested = false) => Actor("AllyContext", owner.transform.position + Vector3.forward * distance, nested);
    Transform Current => (Transform)Get(sensor, "CurrentTarget");
    void Scan() => Call(sensor, "ForceScan");
    void Time(float now) => Set(sensor, "clock", now);
    void Wall()
    {
        var wall = Track(new GameObject("Wall"));
        wall.layer = 29;
        wall.transform.position = owner.transform.position + new Vector3(0, .5f, 1.5f);
        wall.AddComponent<BoxCollider>().size = new Vector3(10, 3, .2f);
        Physics.SyncTransforms();
    }

    object Taunt(Component source)
    {
        var controller = owner.GetComponent(T("StatusEffectController")) ?? owner.gameObject.AddComponent(T("StatusEffectController"));
        Set(owner, "StatusEffects", controller);
        var def = Track(ScriptableObject.CreateInstance(T("StatusEffectDef")));
        ((IList)Get(def, "tags")).Add("Taunt");
        ConstructorInfo ctor = T("StatusEffectInstance").GetConstructors()[0];
        ParameterInfo[] parameters = ctor.GetParameters();
        var args = new object[parameters.Length];
        for (int i = 0; i < args.Length; i++)
            args[i] = parameters[i].ParameterType.IsValueType ? Activator.CreateInstance(parameters[i].ParameterType) : null;
        args[0] = def; args[1] = source.gameObject; args[2] = 1;
        object effect = ctor.Invoke(args);
        ((IList)Get(controller, "ActiveEffects")).Add(effect);
        return effect;
    }

    [TestCase(false)] [TestCase(true)]
    public void DiscoversColliderlessActorWithRootOrNestedInfo(bool nested)
    {
        Component target = Target(3, nested);
        Scan();
        Assert.AreEqual(target, Get(sensor, "CurrentTargetContext"));
        Assert.AreEqual(((Component)Get(target, "TargetInfo")).transform, Current);
    }

    [Test]
    public void DiscoversBestActorBeyondSixtyFourEntries()
    {
        for (int i = 0; i < 70; i++) Target(10 + i * .01f);
        Component nearest = Target(1);
        Scan();
        Assert.AreEqual(nearest, Get(sensor, "CurrentTargetContext"));
    }

    [Test]
    public void SharedContainerDoesNotMakeOpponentsTheSameActor()
    {
        Component target = Target();
        var parent = Track(new GameObject("Container"));
        owner.transform.SetParent(parent.transform);
        target.transform.SetParent(parent.transform);
        Scan();
        Assert.AreEqual(target, Get(sensor, "CurrentTargetContext"));
    }

    [Test]
    public void DoesNotAcquirePropsOrFriendlyActors()
    {
        var prop = Track(new GameObject("Prop"));
        prop.layer = 30;
        prop.transform.position = owner.transform.position + Vector3.forward;
        prop.AddComponent<BoxCollider>();
        Actor("EnemyContext", prop.transform.position);
        Scan();
        Assert.IsNull(Current);
    }

    [TestCase(false)] [TestCase(true)]
    public void InactiveTargetIsImmediatelyInvalid(bool nested)
    {
        Component target = Target(3, nested);
        Scan();
        target.gameObject.SetActive(false);
        Assert.IsNull(Current);
    }

    [Test]
    public void UntargetableTargetCannotRemainInGrace()
    {
        Component target = Target(); Scan();
        Call(Get(target, "TargetInfo"), "SetTargetable", false);
        Assert.IsNull(Current);
    }

    [Test]
    public void BlockedTargetUsesMemoryThenExpires()
    {
        Target(); Scan(); Wall(); Time(.2f); Scan();
        Assert.IsNotNull(Current);
        Assert.IsFalse((bool)Get(sensor, "HasLineOfSight"));
        Assert.AreEqual("Memory", Get(Get(sensor, "Snapshot"), "Reason").ToString());
        Time(2); Scan(); Assert.IsNull(Current);
    }

    [Test]
    public void WallBlocksInitialAcquisition()
    {
        Target(); Wall(); Scan(); Assert.IsNull(Current);
    }

    [Test]
    public void TauntBehindWallWinsWithoutFakingSight()
    {
        Component target = Target(); Wall(); Taunt(target); Scan();
        Assert.AreEqual(target, Get(sensor, "CurrentTargetContext"));
        Assert.IsFalse((bool)Get(sensor, "HasLineOfSight"));
        Assert.AreEqual(float.NegativeInfinity, Get(sensor, "LastSeenTime"));
        Assert.AreEqual("Taunt", Get(Get(sensor, "Snapshot"), "Reason").ToString());
    }

    [TestCase(-3f)] [TestCase(50f)]
    public void TauntBypassesFovAndRadius(float distance)
    {
        Component target = Target(distance);
        Set(sensor, "useFieldOfView", true); Set(sensor, "fieldOfView", 90f);
        Taunt(target); Scan();
        Assert.AreEqual(target, Get(sensor, "CurrentTargetContext"));
        Assert.IsNull(Get(sensor, "VisibleTarget"));
    }

    [Test]
    public void LatestTauntWinsAndFallsBackWhenRemoved()
    {
        Component first = Target(3), second = Target(4);
        Taunt(first); object latest = Taunt(second); Scan();
        Assert.AreEqual(second, Get(sensor, "CurrentTargetContext"));
        ((IList)Get(Get(owner, "StatusEffects"), "ActiveEffects")).Remove(latest);
        Scan(); Assert.AreEqual(first, Get(sensor, "CurrentTargetContext"));
    }

    [Test]
    public void RecycledSourceDoesNotInheritOldTaunt()
    {
        Component target = Target(50); Taunt(target); Scan();
        Set(target, "_lifeGeneration", (int)Get(target, "LifeGeneration") + 1);
        Scan(); Assert.IsNull(Current);
    }

    [Test]
    public void SensorDisableClearsTargetAndThreat()
    {
        Component target = Target(); Scan();
        sensor.GetType().GetMethod("RegisterThreat", new[]{typeof(GameObject),typeof(float)}).Invoke(sensor,new object[]{target.gameObject,50f});
        Call(sensor, "OnDisable"); Assert.IsNull(Current);
        var memory = Get(sensor, "memory");
        Assert.AreEqual(0, ((IDictionary)Get(memory, "threats")).Count);
    }

    [Test]
    public void OrdinaryRetargetHonorsLockThenSwitches()
    {
        Component first = Target(); Scan();
        Component challenger = Target(4);
        Set(Get(challenger, "TargetInfo"), "baseTargetPriority", 100f);
        Time(.5f); Call(sensor, "Scan", false);
        Assert.AreEqual(first, Get(sensor, "CurrentTargetContext"));
        Time(2); Call(sensor, "Scan", false);
        Assert.AreEqual(challenger, Get(sensor, "CurrentTargetContext"));
    }

    [Test]
    public void ThresholdPreventsSmallScoreOscillationsEvenOnForceScan()
    {
        Component first = Target(); Scan();
        Component challenger = Target(3);
        Set(Get(challenger, "TargetInfo"), "baseTargetPriority", 1f);
        Time(10); Scan();
        Assert.AreEqual(first, Get(sensor, "CurrentTargetContext"));
    }

    [Test]
    public void EqualScoreRetainsCurrentTarget()
    {
        Component first = Target(); Scan(); Target(); Time(10); Scan();
        Assert.AreEqual(first, Get(sensor, "CurrentTargetContext"));
    }

    [Test]
    public void UnrelatedLifecycleEventDoesNotBypassCurrentLock()
    {
        Call(sensor, "OnEnable"); Component first = Target(); Scan();
        Component challenger = Target(4);
        Set(Get(challenger, "TargetInfo"), "baseTargetPriority", 100f);
        Component unrelated = Target(50);
        Time(.1f);
        T("CharacterContextRegistry").GetMethod("Unregister").Invoke(null,new object[]{unrelated});
        Assert.AreEqual(first, Get(sensor, "CurrentTargetContext"));
    }

    [Test]
    public void UnregisterClearsTargetAndThreatBeforeNextScanInterval()
    {
        Component target = Target(); Call(sensor, "OnEnable"); Scan();
        T("CharacterContextRegistry").GetMethod("Unregister").Invoke(null,new object[]{target});
        Assert.IsNull(Current);
    }

    [Test]
    public void DeadTargetIsNotRetained()
    {
        Component target = Target(); Scan();
        Call(Get(target, "TargetInfo"), "SetAlive", false);
        Assert.IsNull(Current);
    }

    [Test]
    public void DisabledContextIsNotRetained()
    {
        Component target = Target(); Scan();
        ((Behaviour)target).enabled = false;
        Assert.IsNull(Current);
    }

    [Test]
    public void RefreshingEarlierTauntMakesItNewest()
    {
        Component first = Target(3), second = Target(4);
        object old = Taunt(first); Taunt(second); Scan();
        Assert.AreEqual(second, Get(sensor, "CurrentTargetContext"));
        Call(old, "MarkReapplied"); Scan();
        Assert.AreEqual(first, Get(sensor, "CurrentTargetContext"));
    }

    [Test]
    public void RecycledActorDoesNotInheritThreat()
    {
        Component target = Target(); Scan();
        sensor.GetType().GetMethod("RegisterThreat",new[]{typeof(GameObject),typeof(float)}).Invoke(sensor,new object[]{target.gameObject,50f});
        Set(target, "_lifeGeneration", (int)Get(target, "LifeGeneration") + 1);
        Scan();
        Assert.AreEqual(0, ((IDictionary)Get(Get(sensor, "memory"), "threats")).Count);
    }

    [Test]
    public void CallbackObservesCompleteSnapshotAndRescanDoesNotRecurse()
    {
        Component target = Target(); int count = 0; int depth = 0;
        Action<Transform, Transform> handler = (oldTarget, newTarget) =>
        {
            depth++; Assert.AreEqual(1, depth); count++;
            Assert.AreEqual(target, Get(sensor, "CurrentTargetContext"));
            Assert.IsTrue((bool)Get(sensor, "HasLineOfSight"));
            Assert.Greater((float)Get(sensor, "TargetDistance"), 0f);
            Scan(); depth--;
        };
        EventInfo change = sensor.GetType().GetEvent("OnTargetChanged");
        change.AddEventHandler(sensor, handler);
        try { Scan(); Assert.AreEqual(1, count); }
        finally { change.RemoveEventHandler(sensor, handler); }
    }
}
