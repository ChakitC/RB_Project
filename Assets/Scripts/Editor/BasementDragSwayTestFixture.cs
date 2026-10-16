#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class BasementDragSwayTestFixture
{
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    const string Shared = "Assets/Prefab/CharactorSelect/BasementCharacterPresentation.prefab";
    readonly List<Object> owned = new();
    static void Set(object target, string field, object value) => target.GetType().GetField(field, Hidden).SetValue(target, value);
    static T Get<T>(object target, string field) => (T)target.GetType().GetField(field, Hidden).GetValue(target);
    static void Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Hidden).Invoke(target, args);
    T Own<T>(T value) where T : Object { owned.Add(value); return value; }
    static void Step(BasementDragSway sway, float dt) => Call(sway, "Advance", dt);
    public void Cleanup()
    {
        foreach (var value in owned) if (value) Object.DestroyImmediate(value);
        owned.Clear();
    }

    Camera Camera()
    {
        var camera = Own(new GameObject("Sway test camera")).AddComponent<Camera>();
        camera.enabled = false;
        camera.pixelRect = new Rect(0, 0, 1920, 1080);
        return camera;
    }

    BasementDragSway Synthetic(out BasementDragRig rig, out Transform pivot)
    {
        var root = Own(new GameObject("Sway test"));
        pivot = new GameObject("SwayRoot").transform;
        pivot.SetParent(root.transform, false);
        var model = new GameObject("Model");
        model.transform.SetParent(pivot, false);
        rig = model.AddComponent<BasementDragRig>();
        foreach (var field in new[] { "head", "leftFoot", "rightFoot" })
        {
            var bone = new GameObject(field).transform;
            bone.SetParent(model.transform, false);
            bone.localPosition = field == "head" ? Vector3.up : Vector3.zero;
            Set(rig, field, bone);
        }
        var sway = root.AddComponent<BasementDragSway>();
        Set(sway, "swayRoot", pivot);
        sway.Bind(model);
        return sway;
    }

    public void DirectionReverseClampAndSettle()
    {
        var camera = Camera();
        var sway = Synthetic(out var rig, out var pivot);
        var foot = Get<Transform>(rig, "leftFoot");
        Vector3 grip = rig.GripPosition;
        sway.BeginDrag(camera, Vector2.zero);
        for (int i = 1; i <= 60; i++)
        {
            sway.UpdateDrag(new Vector2(i * 18f, 0));
            Step(sway, 1f / 60);
            Assert.LessOrEqual(Mathf.Abs(Get<float>(sway, "angle")), 20.001f);
            Assert.Less(Vector3.Distance(grip, rig.GripPosition), 0.00001f);
        }
        Assert.Less(foot.position.x, -0.1f, "Feet must lag left when the pointer moves right.");
        for (int i = 1; i <= 60; i++)
        {
            sway.UpdateDrag(new Vector2(1080 - i * 1000, 0));
            Step(sway, 1f / 60);
            Assert.LessOrEqual(Mathf.Abs(Get<float>(sway, "angle")), 20.001f);
        }
        Assert.Greater(foot.position.x, 0.1f);
        bool crossedZero = false;
        for (int i = 0; i < 180; i++)
        {
            Step(sway, 1f / 60); // No new events: pointer speed must become zero.
            crossedZero |= Get<float>(sway, "angle") < 0f;
        }
        Assert.IsTrue(crossedZero, "An underdamped spring should gently overshoot after stopping.");
        Assert.Less(Mathf.Abs(Get<float>(sway, "angle")), 0.01f);
        sway.EndDrag();
        Assert.AreEqual(Vector3.zero, pivot.localPosition);
        Assert.Less(Quaternion.Angle(Quaternion.identity, pivot.localRotation), 0.001f);
    }

    public void SizeFrameRateAndResolution()
    {
        var camera = Camera();
        var sway = Synthetic(out var rig, out _);
        float smallFrequency = 0;
        foreach (float scale in new[] { 0.5f, 1f, 2f })
        {
            sway.transform.localScale = Vector3.one * scale;
            sway.BeginDrag(camera, Vector2.zero);
            Assert.AreEqual(scale, rig.Height, 0.0001f);
            float hz = Get<float>(sway, "dragFrequency");
            if (scale == 0.5f) smallFrequency = hz;
            else Assert.Less(hz, smallFrequency);
            sway.EndDrag();
        }
        sway.transform.localScale = Vector3.one;
        float? expected = null;
        foreach (int height in new[] { 720, 1080, 2160 })
        foreach (int fps in new[] { 30, 60, 120 })
        {
            camera.targetTexture = Own(new RenderTexture(height * 16 / 9, height, 0));
            camera.pixelRect = new Rect(0, 0, height * 16f / 9f, height);
            Assert.AreEqual(height, camera.pixelHeight);
            sway.BeginDrag(camera, Vector2.zero);
            for (int i = 1; i <= fps / 2; i++)
            {
                sway.UpdateDrag(new Vector2(i * height / (float)fps, 0));
                Step(sway, 1f / fps);
            }
            float angle = Get<float>(sway, "angle");
            if (expected.HasValue) Assert.AreEqual(expected.Value, angle, 0.2f);
            expected = angle;
            sway.EndDrag();
        }
    }

    public void AnimatedAnchorScaleFocusAndReset()
    {
        var camera = Camera();
        camera.transform.rotation = Quaternion.Euler(15, 35, 8);
        var sway = Synthetic(out var rig, out var pivot);
        sway.transform.localScale = new Vector3(0.8f, 1.7f, 1.2f);
        Set(rig, "gripOffset", new Vector3(0.03f, 0.1f, 0));
        sway.BeginDrag(camera, Vector2.zero);
        var head = Get<Transform>(rig, "head");
        for (int i = 0; i < 120; i++)
        {
            Call(sway, "RestorePose");
            head.localPosition = new Vector3(Mathf.Sin(i * 0.1f) * 0.1f, 1, 0);
            Vector3 anchor = rig.GripPosition;
            sway.UpdateDrag(new Vector2(i * 18, 0));
            Step(sway, 1f / 60);
            Assert.Less(Vector3.Distance(anchor, rig.GripPosition), 0.0001f);
        }
        Call(sway, "OnApplicationFocus", false);
        sway.UpdateDrag(Vector2.one * 100000);
        Step(sway, 1f / 60);
        Call(sway, "OnApplicationFocus", true);
        Step(sway, 1f / 60);
        sway.EndDrag();
        Assert.AreEqual(0f, Get<float>(sway, "angularVelocity"));
        sway.BeginDrag(camera, Vector2.one * 100000);
        Step(sway, 1f / 60);
        Assert.AreEqual(0f, Get<float>(sway, "angle"));
        sway.enabled = false;
        Assert.AreEqual(Vector3.zero, pivot.localPosition);
    }

    static CharacterStats[] Definitions()
    {
        var result = new List<CharacterStats>();
        var seen = new HashSet<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:CharacterStats"))
        {
            var def = AssetDatabase.LoadAssetAtPath<CharacterStats>(AssetDatabase.GUIDToAssetPath(guid));
            if (!def || !def.CharacterPrefabBasement) continue;
            string path = AssetDatabase.GetAssetPath(def.CharacterPrefabBasement);
            if (path.StartsWith("Assets/Prefab/CharactorSelect/") && seen.Add(path)) result.Add(def);
        }
        return result.ToArray();
    }

    BasementCharacterPresentation Preview(CharacterStats def)
    {
        var p = Own(Object.Instantiate(AssetDatabase.LoadAssetAtPath<BasementCharacterPresentation>(Shared)));
        Assert.IsTrue(p.Build(def));
        return p;
    }

    public void AllEightRigsSizesWeaponsAndLifecycle()
    {
        var defs = Definitions();
        Assert.AreEqual(8, defs.Length);
        var camera = Camera();
        foreach (var def in defs)
        {
            var p = Preview(def);
            var sway = p.GetComponent<BasementDragSway>();
            var rig = p.Model.GetComponent<BasementDragRig>();
            Assert.IsTrue(rig.IsValid, def.name);
            float originalHeight = rig.Height;
            var weapon = new GameObject("Oversized weapon test");
            weapon.transform.SetParent(Get<Transform>(rig, "head"), false);
            weapon.transform.localScale = Vector3.one * 100;
            Assert.AreEqual(originalHeight, rig.Height);
            foreach (float scale in new[] { 0.5f, 1f, 2f })
            {
                p.transform.localScale = Vector3.one * scale;
                p.SetPicked(true);
                p.BeginDrag(camera, Vector2.zero);
                Assert.AreEqual(originalHeight * scale, rig.Height, 0.001f, def.name);
                Vector3 anchor = rig.GripPosition;
                for (int i = 1; i <= 20; i++)
                {
                    p.UpdateDrag(new Vector2(i * 18, 0));
                    Step(sway, 1f / 60);
                    Assert.Less(Vector3.Distance(anchor, rig.GripPosition), 0.001f, def.name);
                }
                p.SetPicked(false);
                Assert.AreEqual(0f, Get<float>(sway, "angle"));
            }
            p.BeginDrag(camera, Vector2.zero);
            p.EnterMenu(null);
            Assert.IsFalse(Get<bool>(sway, "dragging"));
            p.ExitMenu();
            p.BeginDrag(camera, Vector2.zero);
            p.Build(def);
            Assert.IsTrue(Get<bool>(sway, "dragging"), "Refreshing the same model must preserve the ongoing gesture.");
            p.BeginDrag(camera, Vector2.zero);
            p.Clear();
            Assert.IsFalse(Get<bool>(sway, "bound"));
        }
    }

    public void MissingRigStillAllowsPresentation()
    {
        var sway = Synthetic(out _, out _);
        var missing = Own(new GameObject("Missing rig"));
        LogAssert.Expect(LogType.Warning, "[Basement Drag] Bind SwayRoot and the model's head/feet rig. Dragging remains available without sway.");
        sway.Bind(missing);
        sway.BeginDrag(Camera(), Vector2.zero);
        Assert.IsFalse(Get<bool>(sway, "dragging"));
    }

    public IEnumerator ActualLateUpdateUnscaledAndAllEight()
    {
        var camera = Camera();
        float savedTimeScale = Time.timeScale;
        try
        {
            Time.timeScale = 0f;
            foreach (var def in Definitions())
            {
                var p = Preview(def);
                var sway = p.GetComponent<BasementDragSway>();
                p.SetPicked(true);
                p.BeginDrag(camera, Vector2.zero);
                float x = 0;
                for (int i = 0; i < 20; i++)
                {
                    x += camera.pixelHeight * Mathf.Min(Time.unscaledDeltaTime, 0.05f);
                    p.UpdateDrag(new Vector2(x, 0));
                    yield return null;
                }
                Assert.Less(Get<float>(sway, "angle"), -0.1f, def.name);
                p.SetPicked(false);
                Assert.AreEqual(0f, Get<float>(sway, "angle"));
                p.gameObject.SetActive(false);
                p.gameObject.SetActive(true);
                Assert.IsFalse(Get<bool>(sway, "dragging"));
            }
        }
        finally { Time.timeScale = savedTimeScale; }
    }
}
#endif
