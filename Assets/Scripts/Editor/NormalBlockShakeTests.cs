#if UNITY_EDITOR
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class NormalBlockShakeTests
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    GameObject root;
    GameplayCameraController camera;
    DefensiveBlockActorProfile profile;
    readonly object owner = new object();

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Normal Block shake test");
        root.SetActive(false);
        camera = root.AddComponent<GameplayCameraController>();
        profile = ScriptableObject.CreateInstance<DefensiveBlockActorProfile>();
        Set("blockShotOwner", owner);
        Call("ConfigureNormalBlockShake", profile);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(root);
        Object.DestroyImmediate(profile);
    }

    [Test]
    public void ContactStartsOnceWithSnapshotAndOwnerCheckedCleanup()
    {
        Assert.That(Sample(), Is.EqualTo(Vector3.zero));
        camera.PlayNormalBlockImpact(new object());
        Assert.That(Sample(), Is.EqualTo(Vector3.zero));
        camera.PlayNormalBlockImpact(owner);
        Assert.That(Sample().x, Is.EqualTo(-1.2f));
        Call("AdvanceNormalBlockShake", .04f, false);
        Vector3 first = Sample();
        Assert.That(first.magnitude, Is.GreaterThan(0f).And.LessThan(1.2f));
        profile.normalImpactShakeDegrees = 50f;
        camera.PlayNormalBlockImpact(owner);
        camera.EndDefensiveBlockShot(new object(), false);
        Assert.That(Sample(), Is.EqualTo(first));
        Call("AdvanceNormalBlockShake", .2f, false);
        Assert.That(Sample(), Is.EqualTo(Vector3.zero));
        camera.PlayNormalBlockImpact(owner);
        Assert.That(Sample(), Is.EqualTo(Vector3.zero), "Duplicate contact must not restart shake.");
        camera.EndDefensiveBlockShot(owner, false);
        Assert.That(Sample(), Is.EqualTo(Vector3.zero));
        Assert.That((bool)typeof(GameplayCameraController).GetField("normalBlockShakeStarted", Private).GetValue(camera), Is.False);
    }

    [TestCase(30)]
    [TestCase(60)]
    [TestCase(144)]
    public void RealTimeDecayPausesAndEndsAtEveryFrameRate(int fps)
    {
        camera.PlayNormalBlockImpact(owner);
        Vector3 initial = Sample();
        Call("AdvanceNormalBlockShake", 10f, true);
        Assert.That(Sample(), Is.EqualTo(initial));
        for (int i = 0; i < Mathf.CeilToInt(.15f * fps) + 1; i++)
            Call("AdvanceNormalBlockShake", 1f / fps, false);
        Assert.That(Sample(), Is.EqualTo(Vector3.zero));
    }

    [Test]
    public void CooperativeAndDisabledProfilesDoNotReceiveNormalShake()
    {
        Set("cooperativeShotAlly", root.transform);
        camera.PlayNormalBlockImpact(owner);
        Assert.That(Sample(), Is.EqualTo(Vector3.zero));
        Set("cooperativeShotAlly", null);
        foreach (float strength in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
        {
            profile.normalImpactShakeDegrees = strength;
            Call("ConfigureNormalBlockShake", profile);
            camera.PlayNormalBlockImpact(owner);
            Assert.That(Sample(), Is.EqualTo(Vector3.zero));
        }
    }

    [Test]
    public void ClearingInterruptedShotImmediatelyRemovesShake()
    {
        camera.PlayNormalBlockImpact(owner);
        Assert.That(Sample().sqrMagnitude, Is.GreaterThan(0f));
        Call("ClearDefensiveBlockShot");
        Assert.That(Sample(), Is.EqualTo(Vector3.zero));
        camera.PlayNormalBlockImpact(owner);
        Assert.That(Sample(), Is.EqualTo(Vector3.zero));
    }

    void Set(string name, object value) => typeof(GameplayCameraController).GetField(name, Private).SetValue(camera, value);
    object Call(string name, params object[] args) => typeof(GameplayCameraController).GetMethod(name, Private).Invoke(camera, args);
    Vector3 Sample() => (Vector3)Call("SampleNormalBlockShake");
}
#endif
