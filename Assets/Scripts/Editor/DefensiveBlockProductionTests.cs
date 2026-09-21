#if UNITY_EDITOR
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class DefensiveBlockProductionTests
{
    [Test]
    public void ProductionAssetsHaveNoDefensiveTestDependencies()
    {
        foreach (string path in new[] {
            DefensiveBlockProductionAuthoring.SkillPath, DefensiveBlockProductionAuthoring.AiresPath,
            DefensiveBlockProductionAuthoring.RectorPath, "Assets/Prefab/Player/Player.prefab",
            "Assets/Prefab/Player/Ally_Stryker.prefab", "Assets/Prefab/Player/Ally_Helper.prefab" })
            Assert.IsFalse(AssetDatabase.GetDependencies(path).Any(p => p.StartsWith("Assets/Tests/DefensiveBlock/")), path);
        var attack = AssetDatabase.LoadAssetAtPath<SkillGemDefinition>(DefensiveBlockProductionAuthoring.SkillPath).defensiveBlock;
        Assert.IsNotNull(attack); Assert.IsTrue(attack.IsConfigured);
        Assert.IsTrue(attack.AllowsStep(0)); Assert.IsTrue(attack.AllowsStep(1));
        Assert.IsFalse(attack.AllowsStep(-1)); Assert.IsFalse(attack.AllowsStep(int.MaxValue));
        Assert.IsTrue(AssetDatabase.LoadAssetAtPath<CharacterStats>(DefensiveBlockProductionAuthoring.AiresPath).defensiveBlock.IsConfigured);
    }

    [Test]
    public void TriggerFootprintCanSweepWithoutChangingColliderFlags()
    {
        var actor = new GameObject("Block footprint actor");
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        try
        {
            var body = actor.AddComponent<CapsuleCollider>(); body.center = Vector3.up; body.height = 2; body.radius = .4f; body.isTrigger = true;
            wall.layer = 31;
            wall.transform.position = new Vector3(0, 1, -1); wall.transform.localScale = new Vector3(4, 2, .2f);
            Assert.IsTrue(CharacterPlacementFootprintUtility.TryGetColliderFootprint(body, actor.transform, out var footprint, out _));
            Physics.SyncTransforms();
            var shape = DefensiveBlockSlideMotor.ResolveShape(footprint, actor.transform);
            float allowed = CharacterBodySweepUtility.ResolveAllowedDistance(shape, Vector3.back, 2, .03f, 1 << 31, QueryTriggerInteraction.Ignore, actor.transform);
            Assert.That(allowed, Is.InRange(.4f, .51f)); Assert.IsTrue(body.isTrigger);
            // Moving the animation collider cannot change a footprint already accepted by the controller.
            body.center = Vector3.down * 5;
            Assert.That(DefensiveBlockSlideMotor.ResolveShape(footprint, actor.transform).Point0, Is.EqualTo(shape.Point0));
        }
        finally { Object.DestroyImmediate(actor); Object.DestroyImmediate(wall); }
    }

    [Test]
    public void StaleCameraOwnerCannotEndNewerShot()
    {
        var root = new GameObject("Block camera ownership"); root.SetActive(false);
        try
        {
            var camera = root.AddComponent<GameplayCameraController>();
            var owner = new object();
            var field = typeof(GameplayCameraController).GetField("blockShotOwner", BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(camera, owner);
            camera.EndDefensiveBlockShot(new object(), false);
            Assert.IsTrue(camera.IsDefensiveBlockShotActive);
            camera.EndDefensiveBlockShot(owner, false);
            Assert.IsFalse(camera.IsDefensiveBlockShotActive);
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void InvalidProfilesCannotOpenAWindow()
    {
        var attack = new SkillDefensiveBlockSettings();
        var actor = ScriptableObject.CreateInstance<DefensiveBlockActorProfile>();
        try
        {
            Assert.IsTrue(attack.IsConfigured);
            attack.hitboxSteps = new[] {-1}; Assert.IsFalse(attack.IsConfigured);
            attack.hitboxSteps = new[] {0}; attack.windowStartNormalized = .9f; attack.windowEndNormalized = .1f;
            Assert.IsFalse(attack.IsConfigured); Assert.IsFalse(actor.IsConfigured);
        }
        finally {  Object.DestroyImmediate(actor); }
    }

    [Test]
    public void GuardVolumeIncludesPhysicalContactMargin()
    {
        // A physics overlap can precede the mathematical plane by contact-offset noise.
        Assert.IsTrue(DefensiveBlockGeometry.SweepsGuard(new Vector3(0,1,2.42f), new Vector3(0,1,2.281f),
            2.28f, Vector3.up, Vector3.forward, .65f, 1.2f, .05f));
        Assert.IsFalse(DefensiveBlockGeometry.SweepsGuard(new Vector3(0,1,2.5f), new Vector3(0,1,2.4f),
            2.28f, Vector3.up, Vector3.forward, .65f, 1.2f, .05f));
    }

    [Test]
    public void IncomingThreatPredictsPlanarContact()
    {
        Assert.IsTrue(DefensiveBlockGeometry.TryPredictThreat(new Vector3(0, 5, 8), Vector3.back,
            Vector3.zero, 1.5f, 2f, 8f, out float seconds));
        Assert.That(seconds, Is.EqualTo(.75f).Within(.001f));
    }

    [Test]
    public void PassingOrDepartingAttacksAreNotThreats()
    {
        Assert.IsFalse(DefensiveBlockGeometry.TryPredictThreat(new Vector3(3, 0, 8), Vector3.back,
            Vector3.zero, 1.5f, 2f, 8f, out _));
        Assert.IsFalse(DefensiveBlockGeometry.TryPredictThreat(Vector3.forward * 8, Vector3.forward,
            Vector3.zero, 1.5f, 2f, 8f, out _));
        Assert.IsFalse(DefensiveBlockGeometry.TryPredictThreat(Vector3.back, Vector3.back,
            Vector3.zero, 1.5f, 2f, 8f, out _));
    }

    [Test]
    public void ThreatPriorityUsesContactTimeBeforeDistanceAndStableTies()
    {
        Assert.IsTrue(DefensiveBlockGeometry.PreferThreat(.2f, 64, 2, .4f, 16, 1));
        Assert.IsFalse(DefensiveBlockGeometry.PreferThreat(.4f, 16, 1, .2f, 64, 2));
        Assert.IsTrue(DefensiveBlockGeometry.PreferThreat(.2f, 16, 2, .2f, 64, 1));
        Assert.IsTrue(DefensiveBlockGeometry.PreferThreat(.2f, 16, 1, .2f, 16, 2));
        Assert.IsFalse(DefensiveBlockGeometry.PreferThreat(.2f, 16, 2, .2f, 16, 1));
    }

    [MenuItem("Tools/RB/Defensive Block/Run Production Smoke Tests")]
    public static void Run()
    {
        DefensiveBlockTests.RunSmokeTests();
        var tests = new DefensiveBlockProductionTests();
        tests.ProductionAssetsHaveNoDefensiveTestDependencies();
        tests.TriggerFootprintCanSweepWithoutChangingColliderFlags();
        tests.StaleCameraOwnerCannotEndNewerShot(); tests.InvalidProfilesCannotOpenAWindow();
        tests.GuardVolumeIncludesPhysicalContactMargin();
        tests.IncomingThreatPredictsPlanarContact(); tests.PassingOrDepartingAttacksAreNotThreats();
        tests.ThreatPriorityUsesContactTimeBeforeDistanceAndStableTies();
        Debug.Log("DefensiveBlock: 17 smoke tests passed (9 existing + 8 production).");
    }
}
#endif
