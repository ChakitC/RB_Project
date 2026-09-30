#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class CharacterMovementIntegrationTests
{
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    readonly List<GameObject> objects = new();
    readonly Vector3 origin = new(2600f, 700f, 2600f);

    [TearDown]
    public void TearDown()
    {
        for (int i = objects.Count - 1; i >= 0; i--)
            if (objects[i]) Object.DestroyImmediate(objects[i]);
        objects.Clear();
        Physics.SyncTransforms();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void KnockbackMovesWholeActorAndReturnsBlockedDistance(bool controller)
    {
        var context = Actor(controller);
        var module = Child(context.transform, "Knockback").AddComponent<CharacterKnockbackMotor>();
        Set(module, "ctx", context);
        Set(module, "characterController", context.cc);
        Wall();
        Physics.SyncTransforms();
        var applied = (Vector3)Call(module, "ApplyDelta", Vector3.forward * 8f);
        Assert.That(applied.z, Is.InRange(1.3f, 1.7f));
        Assert.That(context.transform.position - origin, Is.EqualTo(applied));
        Assert.That(module.transform.localPosition, Is.EqualTo(Vector3.zero));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void GravityStopsAtGroundAndPreservesChildOffset(bool controller)
    {
        var context = Actor(controller);
        var module = Child(context.transform, "Gravity").AddComponent<CharacterVerticalMotor>();
        Set(module, "ctx", context);
        Set(module, "characterController", context.cc);
        var floor = Create("Floor", origin + Vector3.down * 2.5f).AddComponent<BoxCollider>();
        floor.size = new Vector3(20f, 1f, 20f);
        Physics.SyncTransforms();
        Call(module, "ApplyVerticalDelta", -8f);
        Assert.That(context.transform.position.y - origin.y, Is.InRange(-2.1f, -1.8f));
        Assert.That(module.transform.localPosition, Is.EqualTo(Vector3.zero));
    }

    [Test]
    public void KnockbackRetainsAuthoredCollisionMask()
    {
        var context = Actor(false);
        var module = Child(context.transform, "Knockback").AddComponent<CharacterKnockbackMotor>();
        Set(module, "ctx", context);
        Set(module, "collisionMask", (LayerMask)0);
        Wall();
        Physics.SyncTransforms();
        Assert.That((Vector3)Call(module, "ApplyDelta", Vector3.forward * 8f), Is.EqualTo(Vector3.forward * 8f));
    }

    [Test]
    public void WalkingWithNestedControllerMovesActorRoot()
    {
        var root = Create("Player", origin);
        var cc = AddController(Child(root.transform, "Body"));
        var context = root.AddComponent<PlayerContext>();
        context.cc = cc;
        context.EnsureMovementMotor();
        var walk = Child(root.transform, "Locomotion").AddComponent<PlayerMovementCC>();
        Set(walk, "_characteContext", context);
        Set(walk, "_characterController", cc);
        Wall();
        Physics.SyncTransforms();
        Call(walk, "ApplyMovement", Vector3.forward * 8f);
        Assert.That(root.transform.position.z - origin.z, Is.InRange(1.3f, 1.7f));
        Assert.That(cc.transform.localPosition, Is.EqualTo(Vector3.zero));
    }

    [TestCase(.1f, 10)]
    [TestCase(.5f, 2)]
    [TestCase(.00025f, 20)]
    public void ControllerTravelMatchesDirectMoveAtWalkAndDashStepSizes(float step, int count)
    {
        var context = Actor(true);
        var direct = AddController(Create("Direct controller", origin + Vector3.right * 10f));
        Vector3 directBefore = direct.transform.position;
        var motor = context.EnsureMovementMotor();
        for (int i = 0; i < count; i++)
        {
            direct.Move(Vector3.forward * step);
            motor.Move(Vector3.forward * step, CharacterMovementSettings.Default);
        }
        Assert.That(Vector3.Distance(context.transform.position - origin,
            direct.transform.position - directBefore), Is.LessThan(.0001f));
    }

    [Test]
    public void MissingBodyStopsAndReportsAuthoringOnce()
    {
        var root = Create("Missing body", origin);
        var motor = root.AddComponent<CharacterMovementMotor>();
        UnityEngine.TestTools.LogAssert.Expect(LogType.Warning,
            "[CharacterMovementMotor] 'Missing body' needs an enabled CharacterController or a solid CharacterPositionCollider for movement.");
        Assert.That(motor.Move(Vector3.forward, CharacterMovementSettings.Default), Is.EqualTo(Vector3.zero));
        Assert.That(motor.Move(Vector3.forward, CharacterMovementSettings.Default), Is.EqualTo(Vector3.zero));
    }

    [Test]
    public void LookupAndSuspensionInspectionDoNotCreateMotor()
    {
        var root = Create("Uninitialized actor", origin);
        var context = root.AddComponent<EnemyContext>();
        context.ResolveReferences();
        var driver = root.AddComponent<AgentMoveDriver>();
        Set(driver, "ctx", context);
        Assert.That(CharacterMovementMotor.Resolve(context, root.transform), Is.Null);
        Assert.That(driver.IsAgentTransformSuspended, Is.False);
        Assert.That(root.GetComponent<CharacterMovementMotor>(), Is.Null);
        var standalone = Create("Standalone lookup", origin);
        Assert.That(CharacterMovementMotor.Resolve(null, standalone.transform), Is.Null);
        Assert.That(standalone.GetComponent<CharacterMovementMotor>(), Is.Null);
        var motor = context.EnsureMovementMotor();
        Assert.That(CharacterMovementMotor.Resolve(context, root.transform), Is.SameAs(motor));
        Assert.That(context.EnsureMovementMotor(), Is.SameAs(motor));
        Assert.That(root.GetComponents<CharacterMovementMotor>().Length, Is.EqualTo(1));
    }

    [Test]
    public void BoundMovementReadsReplacementBodyFromContext()
    {
        var context = Actor(false);
        var motor = context.MovementMotor;
        Object.DestroyImmediate(context.ColliderRefs.CharacterPositionCollider);
        var cc = AddController(Child(context.transform, "Replacement controller"));
        context.cc = cc;
        Wall();
        Physics.SyncTransforms();
        var applied = motor.Move(Vector3.forward * 8f, CharacterMovementSettings.Default);
        Assert.That(applied.z, Is.InRange(1.3f, 1.7f));
        Assert.That(cc.transform.localPosition, Is.EqualTo(Vector3.zero));
    }

    [Test]
    public void ContextRefreshPreservesExplicitActorRootBinding()
    {
        var root = Create("Actor root", origin);
        var context = Child(root.transform, "Nested context").AddComponent<EnemyContext>();
        context.cc = AddController(Child(root.transform, "Body"));
        var motor = context.EnsureMovementMotor();
        motor.Configure(context, root.transform);
        context.ResolveReferences();
        context.EnsureMovementMotor();
        Physics.SyncTransforms();
        motor.Move(Vector3.forward, CharacterMovementSettings.Default);
        Assert.That(root.transform.position, Is.EqualTo(origin + Vector3.forward));
        Assert.That(context.transform.localPosition, Is.EqualTo(Vector3.zero));
    }

    [TestCase("Assets/Prefab/Player/Player.prefab")]
    [TestCase("Assets/Prefab/Player/Ally_Helper.prefab")]
    [TestCase("Assets/Prefab/Player/Ally_Stryker.prefab")]
    [TestCase("Assets/Prefab/GameEnemy/Enemy_E_GR_01 Variant.prefab")]
    public void ProductionPrefabResolvesOneMotorAndMovesWholeActor(string path)
    {
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var clone = Object.Instantiate(prefab, origin, Quaternion.identity);
        objects.Add(clone);
        clone.SetActive(true);
        var context = clone.GetComponentInChildren<CharacteContext>(true);
        context.ResolveReferences();
        var motor = context.EnsureMovementMotor();
        context.ResolveReferences();
        Assert.That(context.MovementMotor, Is.SameAs(motor));
        Assert.That(context.GetComponents<CharacterMovementMotor>().Length, Is.EqualTo(1));
        var settings = CharacterMovementSettings.Default;
        settings.Slide = true;
        Physics.SyncTransforms();
        Vector3 before = context.transform.position;
        Vector3 applied = motor.Move(Vector3.forward * .25f, settings);
        Assert.That(applied.z, Is.EqualTo(.25f).Within(.005f), path);
        Assert.That(context.transform.position - before, Is.EqualTo(applied));
    }

    EnemyContext Actor(bool controller)
    {
        var root = Create("Actor", origin);
        var context = root.AddComponent<EnemyContext>();
        var bodyObject = Child(root.transform, "Body");
        if (controller) context.cc = AddController(bodyObject);
        else
        {
            var body = bodyObject.AddComponent<CapsuleCollider>();
            body.center = Vector3.up;
            body.radius = .4f;
            body.height = 2f;
            context.ColliderRefs = root.AddComponent<CharacterColliderRefs>();
            context.ColliderRefs.CharacterPositionCollider = body;
        }
        context.EnsureMovementMotor();
        return context;
    }
    CharacterController AddController(GameObject obj)
    {
        var body = obj.AddComponent<CharacterController>();
        body.center = Vector3.up;
        body.radius = .4f;
        body.height = 2f;
        body.skinWidth = .03f;
        body.minMoveDistance = 0f;
        return body;
    }
    void Wall()
    {
        var wall = Create("Wall", origin + Vector3.forward * 2f + Vector3.up).AddComponent<BoxCollider>();
        wall.size = new Vector3(20f, 4f, .1f);
    }
    GameObject Child(Transform root, string name)
    {
        var result = Create(name, root.position);
        result.transform.SetParent(root, true);
        return result;
    }
    GameObject Create(string name, Vector3 position)
    {
        var obj = new GameObject(name);
        obj.transform.position = position;
        objects.Add(obj);
        return obj;
    }
    static void Set(object target, string field, object value) => target.GetType().GetField(field, Hidden).SetValue(target, value);
    static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Hidden).Invoke(target, args);
}
#endif
