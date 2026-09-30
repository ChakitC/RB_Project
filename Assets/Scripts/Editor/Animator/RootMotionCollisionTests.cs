#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class RootMotionCollisionTests
{
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    readonly List<GameObject> objects = new();
    readonly Vector3 origin = new(1800f, 500f, 1800f);
    GameObject actor;
    AllyContext context;
    RootMotionNavMeshDriver driver;

    [SetUp]
    public void SetUp()
    {
        actor = Create("Root motion actor", origin);
        // Gameplay stays inactive while its authored collision body participates in physics.
        GameObject modules = Create("Inactive context and driver", origin);
        modules.transform.SetParent(actor.transform, true);
        modules.SetActive(false);
        context = modules.AddComponent<AllyContext>();
        driver = modules.AddComponent<RootMotionNavMeshDriver>();
        driver.Configure(null, null, null, context, actor.transform);
        SetPolicy(ignoreCharacters: false);
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = objects.Count - 1; i >= 0; i--)
            if (objects[i] != null)
                Object.DestroyImmediate(objects[i]);
        objects.Clear();
        Physics.SyncTransforms();
    }

    [TestCase(false)]
    [TestCase(true)]
    public void OrdinaryRootMotionStopsAtThinWallWithRootOrNestedBody(bool nested)
    {
        AddBody(nested);
        AddWall(2f);
        Move(new Vector3(0f, 0f, 8f));
        Assert.That(Travel.z, Is.InRange(1.3f, 1.6f));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void AllyControllerStopsAtWallWithoutSolidAnimatedBody(bool nested)
    {
        CharacterController controller = AddController(nested);
        Vector3 originalLocal = controller.transform.localPosition;
        // Aires/Roma use a trigger body. It must not replace the locomotion controller.
        AddBody(true).isTrigger = true;
        AddWall(2f);
        Move(new Vector3(0f, 0f, 8f));
        Assert.That(Travel.z, Is.InRange(1.3f, 1.7f));
        if (nested)
            Assert.That(controller.transform.localPosition, Is.EqualTo(originalLocal));
    }

    [Test]
    public void DownwardAnimationStillMovesForwardAlongFloor()
    {
        AddBody(false);
        AddBox("Floor", new Vector3(0f, -.5f, 0f), new Vector3(20f, 1f, 20f));
        Move(new Vector3(0f, -.1f, 1f));
        Assert.That(Travel.z, Is.GreaterThan(.95f));
        Assert.That(Travel.y, Is.GreaterThanOrEqualTo(-.002f));
    }

    [Test]
    public void SlidingStopsAtSecondWallInCorner()
    {
        AddBody(false);
        AddWall(2f);
        AddBox("Side wall", new Vector3(2f, 1f, 0f), new Vector3(.1f, 4f, 20f));
        Move(new Vector3(5f, 0f, 3f));
        Assert.That(Travel.x, Is.InRange(1f, 1.6f));
        Assert.That(Travel.z, Is.InRange(1f, 1.6f));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void IgnoringCharactersStillStopsAtWorldAndRestoresControllerMask(bool useController)
    {
        CharacterController controller = useController ? AddController(false) : null;
        if (!useController) AddBody(false);
        var other = AddBox("Other character", new Vector3(0f, 1f, 1.5f), new Vector3(1f, 2f, .2f));
        other.gameObject.layer = LayerMask.NameToLayer("Enemy");
        AddWall(4f);
        if (controller != null) controller.excludeLayers = 1 << LayerMask.NameToLayer("UI");
        int previousMask = controller != null ? controller.excludeLayers.value : 0;
        SetPolicy(ignoreCharacters: true);
        Move(new Vector3(0f, 0f, 8f));
        Assert.That(Travel.z, Is.InRange(3.3f, 3.7f));
        if (controller != null)
            Assert.That(controller.excludeLayers.value, Is.EqualTo(previousMask));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void CharacterCollisionStillBlocksWhenNotIgnored(bool useController)
    {
        if (useController) AddController(false); else AddBody(false);
        var other = AddBox("Other character", new Vector3(0f, 1f, 2f), new Vector3(1f, 2f, .1f));
        other.gameObject.layer = LayerMask.NameToLayer("Enemy");
        Move(new Vector3(0f, 0f, 8f));
        Assert.That(Travel.z, Is.InRange(1.3f, 1.7f));
    }

    [Test]
    public void OpenSpacePreservesFullTranslation()
    {
        AddBody(false);
        Vector3 delta = new(.3f, .5f, 2f);
        Move(delta);
        Assert.That(Vector3.Distance(Travel, delta), Is.LessThan(.001f));
    }

    [Test]
    public void RepeatedResolutionKeepsOneActorMotorAcrossModelReplacement()
    {
        var first = CharacterMovementMotor.Resolve(context, actor.transform);
        var model = Create("Replaceable model", origin);
        model.transform.SetParent(actor.transform);
        Object.DestroyImmediate(model);
        Assert.That(CharacterMovementMotor.Resolve(context, actor.transform), Is.SameAs(first));
        context.ResolveReferences();
        Assert.That(context.MovementMotor, Is.SameAs(first));
        Assert.That(context.GetComponents<CharacterMovementMotor>().Length, Is.EqualTo(1));
    }

    [Test]
    public void SharedMotorReportsActualBlockedDistanceAndHonoursMask()
    {
        AddBody(false);
        AddWall(2f);
        Physics.SyncTransforms();
        var motor = CharacterMovementMotor.Resolve(context, actor.transform);
        var settings = CharacterMovementSettings.Default;
        Vector3 before = actor.transform.position;
        Vector3 applied = motor.Move(Vector3.forward * 8f, settings);
        Assert.That(applied, Is.EqualTo(actor.transform.position - before));
        Assert.That(applied.z, Is.InRange(1.3f, 1.6f));
        settings.CollisionMask = 0;
        applied = motor.Move(Vector3.forward * 3f, settings);
        Assert.That(applied.z, Is.EqualTo(3f).Within(.001f));
    }

    [Test]
    public void DenseSelfCollidersCannotHideBlockingWall()
    {
        AddBody(false);
        for (int i = 0; i < 40; i++)
        {
            var zone = Create("Self hit zone", origin + Vector3.up);
            zone.transform.SetParent(actor.transform, true);
            zone.AddComponent<SphereCollider>().radius = .2f;
        }
        AddWall(2f);
        Move(new Vector3(0f, 0f, 8f));
        Assert.That(Travel.z, Is.InRange(1.3f, 1.6f));
    }

    [Test]
    public void DisabledControllerDoesNotFallBackToUncheckedTranslation()
    {
        AddController(false).enabled = false;
        Move(Vector3.forward * 8f);
        Assert.That(Travel, Is.EqualTo(Vector3.zero));
    }

    [Test]
    public void RebindingCopiesEnvironmentCollisionSettings()
    {
        var source = Create("Inactive settings source", origin);
        source.SetActive(false);
        var sourceDriver = source.AddComponent<RootMotionNavMeshDriver>();
        SetField(sourceDriver, "environmentCollisionMask", (LayerMask)1);
        SetField(sourceDriver, "environmentCollisionPadding", .2f);
        SetField(sourceDriver, "navMeshRecoverySearchRadius", 4f);
        driver.Configure(null, null, null, context, actor.transform, sourceDriver);
        AddBody(false);
        AddWall(2f);
        Move(Vector3.forward * 8f);
        Assert.That(Travel.z, Is.InRange(1.3f, 1.4f), "The copied padding must affect the sweep.");
        Assert.That(GetField(driver, "environmentCollisionMask"), Is.EqualTo((LayerMask)1));
        Assert.That(GetField(driver, "navMeshRecoverySearchRadius"), Is.EqualTo(4f));
    }

    Vector3 Travel => actor.transform.position - origin;

    GameObject Create(string name, Vector3 position)
    {
        var result = new GameObject(name);
        result.transform.position = position;
        objects.Add(result);
        return result;
    }

    CapsuleCollider AddBody(bool nested)
    {
        GameObject bodyObject = nested ? Create("Nested body", origin) : actor;
        if (nested) bodyObject.transform.SetParent(actor.transform, true);
        var body = bodyObject.AddComponent<CapsuleCollider>();
        body.center = Vector3.up;
        body.height = 2f;
        body.radius = .4f;
        context.ColliderRefs = bodyObject.AddComponent<CharacterColliderRefs>();
        context.ColliderRefs.CharacterPositionCollider = body;
        return body;
    }

    CharacterController AddController(bool nested)
    {
        GameObject bodyObject = nested ? Create("Nested controller", origin) : actor;
        if (nested) bodyObject.transform.SetParent(actor.transform, true);
        bodyObject.layer = LayerMask.NameToLayer("Ally");
        var controller = bodyObject.AddComponent<CharacterController>();
        controller.center = Vector3.up;
        controller.height = 2f;
        controller.radius = .4f;
        controller.skinWidth = .03f;
        controller.minMoveDistance = 0f;
        context.cc = controller;
        return controller;
    }

    BoxCollider AddBox(string name, Vector3 offset, Vector3 size)
    {
        var result = Create(name, origin + offset).AddComponent<BoxCollider>();
        result.size = size;
        return result;
    }

    void AddWall(float z) => AddBox("Thin wall", new Vector3(0f, 1f, z), new Vector3(20f, 4f, .1f));

    void SetPolicy(bool ignoreCharacters) => SetField(driver, "_policy",
        new RootMotionPolicy(true, false, false, ignoreCharacters, environmentSafe: false));

    void Move(Vector3 delta)
    {
        Physics.SyncTransforms();
        typeof(RootMotionNavMeshDriver).GetMethod("ApplyTranslation", Hidden).Invoke(driver, new object[] { delta });
    }

    static void SetField(object target, string name, object value) =>
        target.GetType().GetField(name, Hidden).SetValue(target, value);

    static object GetField(object target, string name) => target.GetType().GetField(name, Hidden).GetValue(target);
}
#endif
