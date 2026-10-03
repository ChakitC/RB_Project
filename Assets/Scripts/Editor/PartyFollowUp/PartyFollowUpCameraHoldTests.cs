using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public sealed class PartyFollowUpCameraHoldTests
{
    GameObject root;
    GameplayCameraController camera;
    object owner;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Inactive followUp camera test");
        root.SetActive(false);
        camera = root.AddComponent<GameplayCameraController>();
        owner = new object();
        Set("followUpFocusOwner", owner);
        Set("followUpProfile", new PartyFollowUpPresentationProfile { cameraHoldSeconds = 0.5f });
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
        camera.EndFollowUpFocus(owner, true);
        Assert.That(Get("followUpFocusOwner"), Is.SameAs(owner));
        Assert.That(Get("followUpHoldRemaining"), Is.EqualTo(0.5f));
        Set("followUpHoldRemaining", 0.1f);
        camera.EndFollowUpFocus(owner, true);
        Assert.That(Get("followUpHoldRemaining"), Is.EqualTo(0.1f), "Repeated release must not extend the hold.");
        camera.EndFollowUpFocus(new object());
        Assert.That(Get("followUpFocusOwner"), Is.SameAs(owner), "Old owners must not cancel a new shot.");
        camera.EndFollowUpFocus(owner);
        Assert.That(Get("followUpFocusOwner"), Is.Null);
    }

    [Test]
    public void ZeroHoldPreservesImmediateReturn()
    {
        Set("followUpProfile", new PartyFollowUpPresentationProfile { cameraHoldSeconds = 0f });
        camera.EndFollowUpFocus(owner, true);
        Assert.That(Get("followUpFocusOwner"), Is.Null);
    }

    [Test]
    public void PausePreservesHoldAndExpiredHoldReleasesOwner()
    {
        GlobalTimeScaleManager existing = Object.FindAnyObjectByType<GlobalTimeScaleManager>();
        GlobalTimeScaleManager global = GlobalTimeScaleManager.Instance;
        int pause = global.AcquirePauseToken();
        var tick = typeof(GameplayCameraController).GetMethod("TickFollowUpFocus",
            BindingFlags.Instance | BindingFlags.NonPublic);
        try
        {
            camera.EndFollowUpFocus(owner, true);
            tick.Invoke(camera, null);
            Assert.That(Get("followUpHoldRemaining"), Is.EqualTo(0.5f));
            global.ReleasePauseToken(pause);
            Set("followUpHoldRemaining", 0f);
            tick.Invoke(camera, null);
            Assert.That(Get("followUpFocusOwner"), Is.Null);
        }
        finally
        {
            global.ReleasePauseToken(pause);
            if (existing == null) Object.DestroyImmediate(global.gameObject);
        }
    }
}
