using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class BlockReadyCueTests
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    readonly List<GameObject> created = new List<GameObject>();
    Component cue;
    GameObject prefab;
    static object Get(object o, string name) => o.GetType().GetField(name, Flags).GetValue(o);
    static void Set(object o, string name, object value) => o.GetType().GetField(name, Flags).SetValue(o, value);
    static object Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, Flags).Invoke(o, args);
    GameObject Go(string name) { var go = new GameObject(name); created.Add(go); return go; }

    [SetUp]
    public void SetUp()
    {
        cue = Go("Ready cue test").AddComponent(Type.GetType("DefensiveBlockReadyCue, Assembly-CSharp", true));
        prefab = GameObject.CreatePrimitive(PrimitiveType.Quad);
        created.Add(prefab);
        Set(cue, "cuePrefab", prefab);
        Set(cue, "displayedReady", true);
        var unavailable = GameObject.CreatePrimitive(PrimitiveType.Quad);
        unavailable.name = "Unavailable ring test";
        created.Add(unavailable);
        Set(cue, "unavailableCuePrefab", unavailable);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var field in new[] { "readyCue", "unavailableCue" })
        {
            var instance = Get(cue, field) as Transform;
            if (instance != null) Object.DestroyImmediate(instance.gameObject);
        }
        for (int i = created.Count - 1; i >= 0; i--) Object.DestroyImmediate(created[i]);
        created.Clear();
    }

    [Test]
    public void CameraChangesWhileOldCameraRemainsEnabled()
    {
        var old = Go("Old camera").AddComponent<Camera>();
        Set(cue, "viewCamera", old);
        var main = Camera.main;
        if (main == null) { main = Go("New main camera").AddComponent<Camera>(); main.tag = "MainCamera"; }
        Call(cue, "ResolveViewCamera");
        Assert.That(Get(cue, "viewCamera"), Is.SameAs(main));
        Assert.That(old.enabled, Is.True);
    }

    [Test]
    public void ReenableResetsHiddenPresentation()
    {
        Call(cue, "PrepareCue");
        Set(cue, "readyWeight", 1f);
        // Edit Mode does not dispatch ordinary MonoBehaviour lifecycle callbacks.
        Call(cue, "OnDisable");
        Call(cue, "OnEnable");
        Assert.That(((Transform)Get(cue, "cue")).gameObject.activeSelf, Is.False);
        Assert.That(Get(cue, "readyWeight"), Is.EqualTo(0f));
        Assert.That(Get(cue, "viewCamera"), Is.Null);
        Assert.That(Call(cue, "PrepareCue"), Is.True);
    }

    [Test]
    public void RestoresDisabledRendererAndMissingMaterial()
    {
        Call(cue, "PrepareCue");
        var renderer = (Renderer)Get(cue, "cueRenderer");
        renderer.enabled = false;
        renderer.forceRenderingOff = true;
        renderer.sharedMaterial = null;
        renderer.gameObject.SetActive(false);
        Assert.That(Call(cue, "PrepareCue"), Is.True);
        Assert.That(renderer.enabled && !renderer.forceRenderingOff && renderer.gameObject.activeSelf, Is.True);
        Assert.That(renderer.sharedMaterial, Is.SameAs(prefab.GetComponent<Renderer>().sharedMaterial));
    }

    [Test]
    public void RecreatesDestroyedInstance()
    {
        Call(cue, "PrepareCue");
        Object.DestroyImmediate(((Transform)Get(cue, "cue")).gameObject);
        Assert.That(Call(cue, "PrepareCue"), Is.True);
        Assert.That((Renderer)Get(cue, "cueRenderer"), Is.Not.Null);
    }

    [Test]
    public void InvalidPrefabFailsWithoutThrowing()
    {
        Set(cue, "cuePrefab", Go("Missing renderer"));
        Assert.That(Call(cue, "PrepareCue"), Is.False);
    }

    [Test]
    public void DiagnosticsLogOnlyTransitionsIncludingReadiness()
    {
        Set(cue, "logPresentation", true);
        LogAssert.Expect(LogType.Log, "[Block.ReadyCue] ready=False status=Offscreen camera='' target=''");
        Call(cue, "ReportPresentation", "Offscreen");
        Call(cue, "ReportPresentation", "Offscreen");
        Set(cue, "<IsReady>k__BackingField", true);
        LogAssert.Expect(LogType.Log, "[Block.ReadyCue] ready=True status=Offscreen camera='' target=''");
        Call(cue, "ReportPresentation", "Offscreen");
        Call(cue, "ReportPresentation", "Offscreen");
        LogAssert.NoUnexpectedReceived();
        Set(cue, "logPresentation", false);
    }

    [Test]
    public void SwitchingReadinessReusesDistinctInstancesAndHidesPreviousSymbol()
    {
        Call(cue, "PrepareCue");
        var ready = (Transform)Get(cue, "cue");
        Call(cue, "SetPresentationReady", false);
        Assert.That(Call(cue, "PrepareCue"), Is.True);
        var unavailable = (Transform)Get(cue, "cue");
        Assert.That(unavailable, Is.Not.SameAs(ready));
        Assert.That(ready.gameObject.activeSelf, Is.False);
        Assert.That(unavailable.gameObject.activeSelf, Is.True);
        Assert.That(Get(cue, "phase").ToString(), Is.EqualTo("Holding"));
        Call(cue, "SetPresentationReady", true);
        Call(cue, "PrepareCue");
        Assert.That(Get(cue, "cue"), Is.SameAs(ready));
        Assert.That(unavailable.gameObject.activeSelf, Is.False);
        Assert.That(Get(cue, "phase").ToString(), Is.EqualTo("Appearing"));
        Call(cue, "AdvanceAnimation", 0.09f);
        var elapsed = Get(cue, "phaseTime");
        Call(cue, "SetPresentationReady", true);
        Assert.That(Get(cue, "phaseTime"), Is.EqualTo(elapsed), "Holding readiness must not restart the flash every frame.");
        Call(cue, "SetPresentationReady", false);
        Assert.That(Get(cue, "intensity"), Is.EqualTo(1f));
        Assert.That(Get(cue, "readyWeight"), Is.EqualTo(Get(cue, "unavailableBrightness")));
    }

    [Test]
    public void MissingUnavailablePrefabDoesNotShowReadyFlareAsWarning()
    {
        Set(cue, "unavailableCuePrefab", null);
        Call(cue, "SetPresentationReady", false);
        Assert.That(Call(cue, "PrepareCue"), Is.False);
    }
}
