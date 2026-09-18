using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class PartyComboCameraHoldTests
{
    GameObject root;
    GameplayCameraController camera;
    object owner;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Inactive combo camera test");
        root.SetActive(false);
        camera = root.AddComponent<GameplayCameraController>();
        owner = new object();
        Set("comboFocusOwner", owner);
        Set("comboProfile", new PartyComboPresentationProfile { cameraHoldSeconds = 0.5f });
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(root);

    void Set(string field, object value) => typeof(GameplayCameraController)
        .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(camera, value);

    object Get(string field) => typeof(GameplayCameraController)
        .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(camera);

    [Test]
    public void ReleaseHoldsFocusButCancellationReturnsImmediately()
    {
        camera.EndComboFocus(owner, true);
        Assert.That(Get("comboFocusOwner"), Is.SameAs(owner));
        Assert.That(Get("comboHoldRemaining"), Is.EqualTo(0.5f));
        Set("comboHoldRemaining", 0.1f);
        camera.EndComboFocus(owner, true);
        Assert.That(Get("comboHoldRemaining"), Is.EqualTo(0.1f), "Repeated release must not extend the hold.");
        camera.EndComboFocus(new object());
        Assert.That(Get("comboFocusOwner"), Is.SameAs(owner), "Old owners must not cancel a new shot.");
        camera.EndComboFocus(owner);
        Assert.That(Get("comboFocusOwner"), Is.Null);
    }

    [Test]
    public void ZeroHoldPreservesImmediateReturn()
    {
        Set("comboProfile", new PartyComboPresentationProfile { cameraHoldSeconds = 0f });
        camera.EndComboFocus(owner, true);
        Assert.That(Get("comboFocusOwner"), Is.Null);
    }

    [Test]
    public void PausePreservesHoldAndExpiredHoldReleasesOwner()
    {
        GlobalTimeScaleManager existing = Object.FindAnyObjectByType<GlobalTimeScaleManager>();
        GlobalTimeScaleManager global = GlobalTimeScaleManager.Instance;
        int pause = global.AcquirePauseToken();
        var tick = typeof(GameplayCameraController).GetMethod("TickComboFocus",
            BindingFlags.Instance | BindingFlags.NonPublic);
        try
        {
            camera.EndComboFocus(owner, true);
            tick.Invoke(camera, null);
            Assert.That(Get("comboHoldRemaining"), Is.EqualTo(0.5f));
            global.ReleasePauseToken(pause);
            Set("comboHoldRemaining", 0f);
            tick.Invoke(camera, null);
            Assert.That(Get("comboFocusOwner"), Is.Null);
        }
        finally
        {
            global.ReleasePauseToken(pause);
            if (existing == null) Object.DestroyImmediate(global.gameObject);
        }
    }
}
