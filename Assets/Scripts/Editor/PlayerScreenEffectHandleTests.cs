#if UNITY_EDITOR
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class PlayerScreenEffectHandleTests
{
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    GameObject hud, cameraObject;
    PlayerFullscreenEffectController controller;
    PlayerScreenEffectHandle first, second;

    [SetUp] public void Setup()
    {
        hud = new GameObject("Inactive screen effect HUD"); hud.SetActive(false);
        cameraObject = new GameObject("Screen effect test camera", typeof(Camera));
        controller = hud.AddComponent<PlayerFullscreenEffectController>();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VFX/Hovl Studio/Fullscreen effects/Prefabs/Screen wind.prefab");
        Assert.That(prefab, Is.Not.Null);
        typeof(PlayerFullscreenEffectController).GetField("perfectDodgeScreenEffectPrefab", Hidden).SetValue(controller, prefab);
        typeof(PlayerFullscreenEffectController).GetField("targetCamera", Hidden).SetValue(controller, cameraObject.GetComponent<Camera>());
    }

    [TearDown] public void Cleanup()
    {
        first?.Dispose(); second?.Dispose();
        Object.DestroyImmediate(hud); Object.DestroyImmediate(cameraObject);
    }

    [Test] public void InactiveHudUsesExactOwnedDurationAndManualParticleClock()
    {
        first = controller.BeginScreenWind(Vector3.forward, .25f);
        Assert.That(first?.IsActive, Is.True);
        var particle = cameraObject.GetComponentInChildren<ParticleSystem>();
        Assert.That(particle.isPaused, Is.True);
        first.Advance(.1f);
        Assert.That(particle.time, Is.EqualTo(.1f).Within(.001f));
        first.Advance(0f);
        Assert.That(particle.time, Is.EqualTo(.1f).Within(.001f));
        first.Advance(.151f);
        Assert.That(first.IsActive, Is.False, "The prefab/minimum lifetime must not extend the requested duration.");
        Assert.That(cameraObject.transform.childCount, Is.Zero);
    }

    [Test] public void DisposingOneWindDoesNotStopAnotherAndCanRepeat()
    {
        first = controller.BeginScreenWind(Vector3.forward, 3f);
        second = controller.BeginScreenWind(Vector3.right, 3f);
        first.Advance(.2f); first.Dispose(); first.Dispose(); first.Advance(1f);
        Assert.That(first.IsActive, Is.False);
        Assert.That(second.IsActive, Is.True);
        Assert.That(cameraObject.transform.childCount, Is.EqualTo(1));
        second.Advance(.3f);
        Assert.That(cameraObject.GetComponentInChildren<ParticleSystem>().time, Is.EqualTo(.3f).Within(.001f));
    }

    [Test] public void DestroyedCameraCanBeCleanedUpSafely()
    {
        first = controller.BeginScreenWind(Vector3.forward, 3f);
        Object.DestroyImmediate(cameraObject); cameraObject = null;
        first.Advance(.1f); first.Dispose();
        Assert.That(first.IsActive, Is.False);
    }

    [Test] public void InvalidDurationCreatesNoEffect()
    {
        foreach (float duration in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
            Assert.That(controller.BeginScreenWind(Vector3.forward, duration), Is.Null);
        Assert.That(cameraObject.transform.childCount, Is.Zero);
    }
}
#endif
