#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Animancer;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class BasementAnimationTestFixture
{
    static readonly string[] Names = { "Roma", "Aires", "Feno", "Milano", "Abbygail", "Dorothy", "Noemi", "Roger" };
    readonly List<Object> owned = new();
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    T Own<T>(T value) where T : Object { owned.Add(value); return value; }
    static void Set(object obj, string name, object value) => obj.GetType().GetField(name, Hidden).SetValue(obj, value);
    static T Get<T>(object obj, string name) => (T)obj.GetType().GetField(name, Hidden).GetValue(obj);

    public void Cleanup()
    {
        for (int i = owned.Count - 1; i >= 0; i--)
            if (owned[i] != null) Object.DestroyImmediate(owned[i]);
        owned.Clear();
    }

    AnimationClip Clip(string name, bool loop)
    {
        var clip = Own(new AnimationClip { name = name });
        clip.SetCurve("Bone", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0, 0, 1, 1));
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        return clip;
    }

    BasementAnimationController Create(out AnimancerComponent animancer, out BasementAnimationProfileSO profile)
    {
        profile = Own(ScriptableObject.CreateInstance<BasementAnimationProfileSO>());
        profile.idle.Clip = Clip("Idle", true);
        profile.idle.FadeDuration = 0.1f;
        profile.pickup.Clip = Clip("Pickup", false);
        profile.pickup.FadeDuration = 0.1f;
        var root = Own(new GameObject("Basement animation test"));
        root.SetActive(false);
        new GameObject("Bone").transform.SetParent(root.transform, false);
        var animator = root.AddComponent<Animator>();
        animancer = root.AddComponent<AnimancerComponent>();
        animancer.Animator = animator;
        var controller = root.AddComponent<BasementAnimationController>();
        Set(controller, "animancer", animancer);
        Set(controller, "profile", profile);
        root.SetActive(true);
        controller.ResetToIdle();
        return controller;
    }

    public void PlaybackAndRepeatedCommands()
    {
        var controller = Create(out var animancer, out var profile);
        Assert.AreSame(profile.idle.Clip, animancer.States.Current.Clip);
        controller.SetPicked(true);
        animancer.Evaluate(0.2f);
        var state = animancer.States.Current;
        double time = state.Time;
        controller.SetPicked(true);
        Assert.AreSame(state, animancer.States.Current);
        Assert.AreEqual(time, state.Time, 0.0001);
        controller.SetPicked(false);
        Assert.AreSame(profile.idle.Clip, animancer.States.Current.Clip);
        controller.SetPicked(true);
        controller.ResetToIdle();
        Assert.IsFalse(controller.IsPicked);
        Assert.AreEqual(0, animancer.States.Current.Time, 0.0001);
    }

    public void FormationStatusAndSubmenuRemainSeparate()
    {
        var controller = Create(out var animancer, out var profile);
        profile.inMenuIdle.Clip = Clip("StatusIdle", true);
        Assert.AreSame(profile.idle.Clip, animancer.States.Current.Clip);
        controller.SetPicked(true);
        Assert.AreSame(profile.pickup.Clip, animancer.States.Current.Clip);
        controller.SetPicked(false);
        Assert.AreSame(profile.idle.Clip, animancer.States.Current.Clip);
        controller.SetInCharacterMenu(true);
        Assert.AreSame(profile.inMenuIdle.Clip, animancer.States.Current.Clip);
        animancer.Evaluate(0);
        animancer.Evaluate(.4f);
        double time = animancer.States.Current.Time;
        controller.SetInCharacterMenu(true);
        Assert.AreEqual(time, animancer.States.Current.Time);
        foreach (string name in new[] { "Accessory", "Gun" })
        {
            var entry = new BasementAnimationProfileSO.MenuEntry();
            entry.animation.Clip = Clip(name, true);
            controller.EnterMenu(entry);
            controller.SetPicked(true);
            controller.SetPicked(false);
            Assert.AreSame(entry.animation.Clip, animancer.States.Current.Clip);
            controller.ExitMenu();
            Assert.AreSame(profile.inMenuIdle.Clip, animancer.States.Current.Clip);
        }
        // Closing the parent while the child is open must not restore the closed parent's pose.
        controller.EnterMenu(new BasementAnimationProfileSO.MenuEntry { animation = new ClipTransition { Clip = Clip("Child", true) } });
        controller.SetInCharacterMenu(false);
        controller.ExitMenu();
        Assert.AreSame(profile.idle.Clip, animancer.States.Current.Clip);
        controller.SetPicked(true);
        Assert.AreSame(profile.pickup.Clip, animancer.States.Current.Clip);
        controller.SetInCharacterMenu(true);
        controller.SetInCharacterMenu(false);
        Assert.AreSame(profile.pickup.Clip, animancer.States.Current.Clip);
    }

    public void MenuPoseAndSelectionRestore()
    {
        var controller = Create(out var animancer, out var profile);
        controller.SetPicked(true);
        var mode = animancer.UpdateMode;
        var menu = Own(ScriptableObject.CreateInstance<BasementMenuDefinitionSO>());
        var entry = new BasementAnimationProfileSO.MenuEntry { menu = menu };
        entry.animation.Clip = Clip("Menu", false);
        entry.animation.FadeDuration = .17f;
        entry.animation.Speed = 1.5f;
        entry.animation.NormalizedStartTime = .2f;
        profile.menus = new[] { entry };
        Assert.AreSame(entry, profile.FindMenu(menu));
        controller.EnterMenu(entry);
        Assert.AreEqual(AnimatorUpdateMode.UnscaledTime, animancer.UpdateMode);
        var state = animancer.States.Current;
        Assert.AreSame(entry.animation.Clip, state.Clip);
        Assert.AreEqual(.2, state.NormalizedTime, .001);
        Assert.AreEqual(1.5, state.Speed);
        animancer.Evaluate(.5f);
        double time = state.Time;
        controller.EnterMenu(entry);
        Assert.AreEqual(time, state.Time, "Repeated entry must not restart.");
        controller.SetPicked(false);
        Assert.AreSame(state, animancer.States.Current, "Selection commands must not replace the menu pose.");
        for (int i = 0; i < 20; i++) animancer.Evaluate(.1f);
        Assert.AreEqual(1, controller.transform.Find("Bone").localPosition.x, .001);
        controller.ExitMenu();
        Assert.AreEqual(mode, animancer.UpdateMode);
        Assert.AreSame(profile.idle.Clip, animancer.States.Current.Clip);
    }

    public void MenuLoopAndMissingEntryPreservePlayback()
    {
        var controller = Create(out var animancer, out var profile);
        controller.SetPicked(true);
        animancer.Evaluate(.3f);
        var previous = animancer.States.Current;
        double previousTime = previous.Time;
        controller.EnterMenu(null);
        controller.ExitMenu();
        Assert.AreSame(previous, animancer.States.Current);
        Assert.AreEqual(previousTime, previous.Time, "Fallback should not restart pickup.");
        var entry = new BasementAnimationProfileSO.MenuEntry();
        entry.animation.Clip = Clip("MenuLoop", true);
        entry.animation.FadeDuration = 0;
        controller.EnterMenu(entry);
        animancer.Evaluate(0f); // Apply the transition's explicit start time before advancing the graph.
        animancer.Evaluate(2.25f);
        Assert.IsTrue(animancer.States.Current.IsLooping);
        Assert.AreEqual(.25, controller.transform.Find("Bone").localPosition.x, .01);
        controller.ExitMenu();
        Assert.AreSame(profile.pickup.Clip, animancer.States.Current.Clip);
    }

    public void MenuCameraBakeIncludesParentsAndRejectsAmbiguity()
    {
        var source = Own(new GameObject("Camera source"));
        source.transform.SetPositionAndRotation(new Vector3(20, 1, -5), Quaternion.Euler(0, 70, 0));
        var parent = new GameObject("Rig").transform;
        parent.SetParent(source.transform, false);
        parent.localPosition = new Vector3(2, 3, 4);
        parent.localRotation = Quaternion.Euler(10, 25, 0);
        parent.localScale = Vector3.one * 2;
        var camera = new GameObject("Camera").AddComponent<Camera>();
        camera.transform.SetParent(parent, false);
        camera.transform.localPosition = new Vector3(0, 1, 2);
        camera.usePhysicalProperties = true;
        camera.sensorSize = new Vector2(36, 24);
        camera.focalLength = 26;
        camera.lensShift = new Vector2(.1f, -.2f);
        var preset = Own(ScriptableObject.CreateInstance<BasementMenuCameraPresetSO>());
        preset.sourceFbx = source;
        BasementMenuCameraPresetEditor.Bake(preset);
        Assert.That(Vector3.Distance(preset.position, source.transform.InverseTransformPoint(camera.transform.position)), Is.LessThan(.0001f));
        Assert.That(Quaternion.Angle(preset.rotation, Quaternion.Inverse(source.transform.rotation) * camera.transform.rotation), Is.LessThan(.001f));
        Assert.AreEqual(camera.fieldOfView, preset.lens.FieldOfView, .0001);
        Assert.AreEqual(camera.lensShift, preset.lens.PhysicalProperties.LensShift);
        new GameObject("Other Camera").AddComponent<Camera>().transform.SetParent(source.transform, false);
        preset.cameraPath = "";
        Assert.Throws<ArgumentException>(() => BasementMenuCameraPresetEditor.Bake(preset));
        preset.cameraPath = "Rig/Camera";
        Assert.DoesNotThrow(() => BasementMenuCameraPresetEditor.Bake(preset));
    }

    public void ProductionMenuPresetsMatchSources()
    {
        const string folder = "Assets/Data/Animation/Basement/Menus/";
        foreach (var name in new[] { "Accessory", "GunMenu" })
        {
            var menu = AssetDatabase.LoadAssetAtPath<BasementMenuDefinitionSO>(folder + name + ".asset");
            Assert.IsNotNull(menu);
            var baked = menu.defaultCamera;
            var copy = Own(Object.Instantiate(baked));
            BasementMenuCameraPresetEditor.Bake(copy);
            Assert.That(Vector3.Distance(copy.position, baked.position), Is.LessThan(.0001f));
            Assert.That(Quaternion.Angle(copy.rotation, baked.rotation), Is.LessThan(.001f));
            Assert.AreEqual(copy.lens.FieldOfView, baked.lens.FieldOfView, .0001);
            Assert.AreEqual(copy.lens.PhysicalProperties.SensorSize, baked.lens.PhysicalProperties.SensorSize);
            Assert.AreEqual(copy.lens.PhysicalProperties.GateFit, baked.lens.PhysicalProperties.GateFit);
        }
        var gun = AssetDatabase.LoadAssetAtPath<BasementMenuDefinitionSO>(folder + "GunMenu.asset");
        var aires = AssetDatabase.LoadAssetAtPath<BasementAnimationProfileSO>("Assets/Data/Animation/Basement/Aires_BasementAnimation.asset");
        Assert.AreSame(gun.defaultCamera, aires.FindMenu(gun).camera);
        Assert.That(gun.defaultCamera.position.x, Is.EqualTo(-.399025f).Within(.00001f));
    }

    public void NonLoopPickupHoldsUntilRelease()
    {
        var controller = Create(out var animancer, out var profile);
        controller.SetPicked(true);
        var state = animancer.States.Current;
        for (int i = 0; i < 20; i++) animancer.Evaluate(0.1f);
        var bone = controller.transform.Find("Bone");
        Assert.Greater(state.Time, state.Length);
        Assert.AreEqual(1f, bone.localPosition.x, 0.001f, "Non-looping clip should hold its final pose.");
        Assert.AreEqual(profile.pickup.Speed, state.Speed);
        double time = state.Time;
        controller.SetPicked(true);
        Assert.AreEqual(time, state.Time, 0.0001);
        controller.SetPicked(false);
        controller.SetPicked(true);
        Assert.AreEqual(profile.pickup.Speed, animancer.States.Current.Speed);
        Assert.AreEqual(0, animancer.States.Current.Time, 0.0001);
    }

    public void TransitionSettingsDrivePlayback()
    {
        var controller = Create(out var animancer, out var profile);
        profile.pickup.NormalizedStartTime = 0.4f;
        profile.pickup.Speed = 1.5f;
        controller.SetPicked(true);
        var state = animancer.States.Current;
        Assert.AreEqual(0.4f, state.NormalizedTime, 0.0001);
        Assert.AreEqual(1.5f, state.Speed);
        controller.SetPicked(false);
        controller.SetPicked(true);
        Assert.AreEqual(0.4f, animancer.States.Current.NormalizedTime, 0.0001);

        profile.idle.NormalizedStartTime = 0.25f;
        profile.idle.Speed = 0.75f;
        controller.ResetToIdle();
        Assert.AreEqual(0.25f, animancer.States.Current.NormalizedTime, 0.0001);
        Assert.AreEqual(0.75f, animancer.States.Current.Speed);
        Assert.AreEqual(1f, animancer.States.Current.Weight, 0.0001, "Reset must apply the initial pose immediately.");
    }

    public void MissingClipDoesNotBlockSelection()
    {
        var controller = Create(out var animancer, out var profile);
        profile.pickup.Clip = null;
        var selectable = controller.gameObject.AddComponent<CharacterSelectable>();
        selectable.syncDefToParentSlot = false;
        var presentation = controller.gameObject.AddComponent<BasementCharacterPresentation>();
        Set(presentation, "animationController", controller);
        selectable.BindPresentation(presentation);
        LogAssert.Expect(LogType.Warning, "[Basement Animation] Assign Animancer, Animator and both profile clips.");
        selectable.SetPicked(true);
        Assert.IsTrue(Get<bool>(selectable, "_picked"));
        Assert.IsTrue(controller.IsPicked);
        Assert.AreSame(profile.idle.Clip, animancer.States.Current.Clip);
        selectable.SetPicked(false);
        Assert.IsFalse(Get<bool>(selectable, "_picked"));
        Set(controller, "animancer", null);
        Assert.DoesNotThrow(() => selectable.SetPicked(true));
        Assert.IsTrue(Get<bool>(selectable, "_picked"));
    }

    static string PrefabPath(string name) => AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefab/CharactorSelect" })
        .Select(AssetDatabase.GUIDToAssetPath).Single(p => System.IO.Path.GetFileNameWithoutExtension(p).StartsWith(name, StringComparison.Ordinal));

    static BasementCharacterPresentation PresentationPrefab => AssetDatabase.LoadAssetAtPath<GameObject>(
        "Assets/Prefab/CharactorSelect/BasementCharacterPresentation.prefab").GetComponent<BasementCharacterPresentation>();

    static CharacterStats Definition(string name) => AssetDatabase.FindAssets("t:CharacterStats")
        .Select(AssetDatabase.GUIDToAssetPath).Where(p => !p.StartsWith("Assets/Tests/", StringComparison.Ordinal))
        .Select(AssetDatabase.LoadAssetAtPath<CharacterStats>).Single(d => d.CharacterPrefabBasement != null
            && AssetDatabase.GetAssetPath(d.CharacterPrefabBasement) == PrefabPath(name));

    public void AllEightPrefabBindingsAndLegacyTiming()
    {
        foreach (string name in Names)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(name));
            var def = Definition(name);
            Assert.IsNull(prefab.GetComponentInChildren<BasementAnimationController>(true), name);
            Assert.IsNull(prefab.GetComponentInChildren<AnimancerComponent>(true), name);
            var target = string.IsNullOrEmpty(def.basementAnimatorPath) ? prefab.transform : prefab.transform.Find(def.basementAnimatorPath);
            Assert.IsNotNull(target, name);
            var animator = target.GetComponent<Animator>();
            Assert.IsNotNull(animator, name);
            Assert.IsNotNull(animator.avatar, name);
            Assert.IsTrue(animator.enabled, name);
            Assert.IsFalse(animator.applyRootMotion, name);
            Assert.IsNotNull(animator.transform.Find("root"), name);
            Assert.AreEqual(prefab.GetComponent<Animator>().cullingMode, animator.cullingMode, name);
            Assert.AreEqual(1, prefab.GetComponentsInChildren<Animator>(true).Count(a => a.enabled), name);
            foreach (var a in prefab.GetComponentsInChildren<Animator>(true)) Assert.IsNull(a.runtimeAnimatorController, name);
            Assert.IsNull(prefab.GetComponentInChildren<CharacterAnimBrain>(true), name);
            Assert.IsNull(prefab.GetComponentInChildren<CharacterAnimDriver>(true), name);
            // Original authoring profiles remain restorable while production uses a shared test profile.
            var profile = AssetDatabase.LoadAssetAtPath<BasementAnimationProfileSO>(
                "Assets/Data/Animation/Basement/" + name + "_BasementAnimation.asset");
            Assert.IsNotNull(profile, name);
            Assert.AreEqual(0f, profile.idle.NormalizedStartTime, name);
            Assert.AreEqual(0f, profile.pickup.NormalizedStartTime, name);
            var path = AssetDatabase.FindAssets("t:AnimatorController", new[] { "Assets/Character/" + name })
                .Select(AssetDatabase.GUIDToAssetPath).Single(p => p.IndexOf("basement", StringComparison.OrdinalIgnoreCase) >= 0);
            var sm = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(path).layers[0].stateMachine;
            var idle = sm.defaultState;
            var pickup = sm.states.Select(s => s.state).Single(s => s != idle);
            Assert.AreSame(idle.motion, profile.idle.Clip, name);
            Assert.AreSame(pickup.motion, profile.pickup.Clip, name);
            Assert.AreEqual(idle.transitions.Single(t => t.destinationState == pickup).duration, profile.pickup.FadeDuration, name);
            Assert.AreEqual(pickup.transitions.Single(t => t.destinationState == idle).duration, profile.idle.FadeDuration, name);
        }
    }

    public void SharedAiresMenuTestProfileBindings()
    {
        var profile = AssetDatabase.LoadAssetAtPath<BasementAnimationProfileSO>(
            "Assets/Data/Animation/Basement/Shared_Aires_MenuTest.asset");
        Assert.IsNotNull(profile);
        Assert.AreEqual("Aires_Formation_Idle", profile.idle.Clip.name);
        Assert.IsTrue(profile.idle.Clip.isLooping);
        Assert.AreEqual("Aires_Inmenu_Idle", profile.inMenuIdle.Clip.name);
        Assert.IsTrue(profile.inMenuIdle.Clip.isLooping);
        Assert.AreEqual("Aires_Formation_Pickup", profile.pickup.Clip.name);
        foreach (var name in Names)
            Assert.AreSame(AssetDatabase.LoadAssetAtPath<BasementAnimationProfileSO>(
                "Assets/Data/Animation/Basement/" + name + "_BasementAnimation.asset"),
                Definition(name).basementAnimationProfile, name);
        foreach (var name in new[] { "Accessory", "GunMenu" })
        {
            var menu = AssetDatabase.LoadAssetAtPath<BasementMenuDefinitionSO>(
                "Assets/Data/Animation/Basement/Menus/" + name + ".asset");
            var entry = profile.FindMenu(menu);
            Assert.IsNotNull(entry, name);
            Assert.AreEqual(name == "Accessory" ? "Aires_AccessoryMenu_Idle" : "Aires_GunMenu_Idle", entry.animation.Clip.name);
            Assert.IsTrue(entry.animation.Clip.isLooping, name);
            Assert.AreEqual(0f, entry.animation.NormalizedStartTime, name);
            Assert.AreEqual(1f, entry.animation.Speed, name);
            Assert.AreSame(menu.defaultCamera, entry.camera, name);
        }
    }

    public void PreviewRebuildBindsNewModel()
    {
        var root = Own(new GameObject("Drag preview test"));
        var preview = root.AddComponent<CharacterDragVisualPreview>();
        preview.presentationPrefab = PresentationPrefab;
        var first = Definition("Roma");
        var second = Definition("Aires");
        Assert.IsTrue(preview.Build(first));
        preview.SetPicked(true);
        var previous = BasementAnimationController.Resolve(root);
        Assert.IsTrue(previous.IsPicked);
        Assert.IsTrue(preview.Build(second));
        var current = BasementAnimationController.Resolve(root);
        Assert.IsNotNull(current);
        Assert.AreSame(previous, current, "Model swaps must reuse the shared animation owner.");
        Assert.AreSame(current.Animator, preview.Animator);
        Assert.IsTrue(current.IsPicked, "A new roster model starts directly in Pickup.");
        preview.SetPicked(true);
        Assert.IsTrue(current.IsPicked);
        Assert.IsTrue(preview.Build(second));
        Assert.IsTrue(current.IsPicked, "Refreshing the same roster model preserves its pose.");
        preview.Clear();
        Assert.IsNull(preview.Animator);
        Assert.AreSame(current, BasementAnimationController.Resolve(root));
        Assert.IsFalse(current.GetComponent<AnimancerComponent>().IsGraphInitialized);
        Assert.IsTrue(preview.Build(first));
        Assert.AreSame(current, BasementAnimationController.Resolve(root));
        Assert.AreEqual(first.basementAnimationProfile.pickup.Clip, current.GetComponent<AnimancerComponent>().States.Current.Clip);
    }

    public void InitialPickupAndSameModelRefreshPreservePlayback()
    {
        var root = Own(new GameObject("Direct pickup preview test"));
        var preview = root.AddComponent<CharacterDragVisualPreview>();
        preview.presentationPrefab = PresentationPrefab;
        foreach (var name in Names)
        {
            var def = Definition(name);
            Assert.IsTrue(preview.Build(def));
            var p = preview.Presentation;
            var animation = p.GetComponent<AnimancerComponent>();
            var state = animation.States.Current;
            Assert.AreSame(def.basementAnimationProfile.pickup.Clip, state.Clip, name);
            Assert.AreEqual(1f, state.Weight, 0.0001f, name);
            Assert.IsFalse(animation.States.TryGet(def.basementAnimationProfile.idle.Clip, out _), "Must not create an intermediate Idle state: " + name);
            animation.Evaluate(0.23f);
            double time = state.Time;
            var model = p.Model;
            Assert.IsTrue(preview.Build(def));
            Assert.AreSame(model, p.Model);
            Assert.AreSame(state, animation.States.Current);
            Assert.AreEqual(time, state.Time);
            Assert.IsTrue(p.IsPicked);

            var entry = new BasementAnimationProfileSO.MenuEntry { animation = new ClipTransition { Clip = Clip("Refresh menu " + name, true) } };
            p.EnterMenu(entry);
            animation.Evaluate(0.3f);
            state = animation.States.Current;
            time = state.Time;
            float weight = state.Weight;
            Assert.IsTrue(p.Build(def));
            Assert.AreSame(state, animation.States.Current);
            Assert.AreEqual(time, state.Time);
            Assert.AreEqual(weight, state.Weight);
            p.Clear();
        }
    }

    public void MenuSwitchNeverRestartsIdleAndRestoresOnce()
    {
        var controller = Create(out var animation, out var profile);
        var idle = animation.States.Current;
        animation.Evaluate(0.31f);
        var mode = animation.UpdateMode;
        controller.SetPicked(true);
        var parent = new BasementAnimationProfileSO.MenuEntry { animation = new ClipTransition { Clip = Clip("Parent direct", true), FadeDuration = 0.2f } };
        var child = new BasementAnimationProfileSO.MenuEntry { animation = new ClipTransition { Clip = Clip("Child direct", true), FadeDuration = 0.4f } };
        controller.EnterMenu(parent);
        controller.SetPicked(false); // Opening a menu cancels dragging after taking animation ownership.
        animation.Evaluate(0.5f);
        double idleTime = idle.Time;
        var parentState = animation.States.Current;
        controller.SwitchMenu(child);
        Assert.AreSame(child.animation.Clip, animation.States.Current.Clip);
        Assert.AreEqual(idleTime, idle.Time, "Switching must never restart Idle, even in the same frame.");
        Assert.AreEqual(0f, idle.TargetWeight);
        Assert.Greater(parentState.Weight, 0f, "The outgoing menu remains the blend source.");
        animation.Evaluate(0.5f);
        idleTime = idle.Time;
        controller.SwitchMenu(parent);
        Assert.AreSame(parent.animation.Clip, animation.States.Current.Clip);
        Assert.AreEqual(idleTime, idle.Time);
        Assert.AreEqual(AnimatorUpdateMode.UnscaledTime, animation.UpdateMode);
        var state = animation.States.Current;
        controller.SwitchMenu(null);
        Assert.AreSame(state, animation.States.Current);
        controller.ExitMenu();
        Assert.AreSame(profile.idle.Clip, animation.States.Current.Clip);
        Assert.AreEqual(mode, animation.UpdateMode);
    }

    public void SharedPresentationSurvivesSlotCleanup()
    {
        var slot = Own(new GameObject("Slot preview test"));
        var preview = slot.AddComponent<PartySlotVisualPreview>();
        var selectable = slot.AddComponent<CharacterSelectable>();
        selectable.syncDefToParentSlot = false;
        Set(preview, "modelRoot", slot.transform);
        Set(preview, "presentationPrefab", PresentationPrefab);
        preview.BuildCharacter(Definition("Roma"), slot.transform);
        var shared = slot.GetComponentInChildren<BasementCharacterPresentation>();
        Assert.AreSame(slot.transform, preview.PresentationRoot, "Accessory camera anchor changed.");
        selectable.SetPicked(true);
        Assert.IsTrue(shared.IsPicked, "Slot input must reach its bound presentation.");

        var drag = Own(new GameObject("Independent drag test")).AddComponent<CharacterDragVisualPreview>();
        drag.presentationPrefab = PresentationPrefab;
        Assert.IsTrue(drag.Build(Definition("Roma")));
        Assert.IsTrue(shared.Animation.IsPicked, "Building another preview changed the slot state.");
        Assert.AreNotSame(shared.Animation, BasementAnimationController.Resolve(drag.gameObject));
        drag.SetPicked(true);
        Assert.IsTrue(drag.Presentation.IsPicked);
        drag.SetPicked(false);
        Assert.IsFalse(drag.Presentation.IsPicked);
        Assert.IsTrue(shared.IsPicked);

        var decoration = new GameObject("Decoration_Select_Preview");
        decoration.transform.SetParent(slot.transform, false);
        decoration.AddComponent<CharacterDefHolder>();
        preview.ClearTemporarySelectObjects(slot.transform);
        Assert.IsTrue(shared != null && shared.Model != null);
        var firstModel = shared.Model;
        preview.BuildCharacter(Definition("Roma"), slot.transform);
        Assert.AreSame(firstModel, shared.Model, "Refreshing the same character must reuse the model.");
        preview.BuildCharacter(Definition("Aires"), slot.transform);
        Assert.AreSame(shared, slot.GetComponentInChildren<BasementCharacterPresentation>());
        Assert.AreEqual(1, slot.GetComponentsInChildren<AnimancerComponent>(true).Length);
        Assert.IsFalse(shared.Animation.IsPicked);
        preview.ClearPreview();
        Assert.IsTrue(decoration != null && decoration.activeSelf, "Cleanup must preserve unrelated slot children.");
        Assert.IsNull(shared.Model);
        Assert.IsNull(preview.Animator);
        Assert.AreSame(slot.transform, preview.PresentationRoot);

        var slotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/CharactorSelect/SlotPoint.prefab");
        var uiPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/User Interface/CharactorSlotUI.prefab");
        Assert.AreSame(PresentationPrefab, Get<BasementCharacterPresentation>(slotPrefab.GetComponent<PartySlotVisualPreview>(), "presentationPrefab"));
        Assert.AreSame(PresentationPrefab, Get<BasementCharacterPresentation>(uiPrefab.GetComponent<SpawnOnDragOutButton>(), "presentationPrefab"));
        Assert.AreEqual(0, slotPrefab.GetComponentsInChildren<CharacterDefHolder>(true).Length, "Remove the authored legacy Roma model.");
    }

    public void MissingAnimatorPathKeepsModelAndSelection()
    {
        var presenter = Own(Object.Instantiate(PresentationPrefab));
        var def = Own(Object.Instantiate(Definition("Roma")));
        def.basementAnimatorPath = "Missing/Skeleton";
        LogAssert.Expect(LogType.Warning, "[Basement Animation] Assign Animancer, Animator and both profile clips.");
        Assert.IsTrue(presenter.Build(def));
        Assert.IsNotNull(presenter.Model);
        Assert.IsNull(presenter.Animator);
        var weapon = Own(ScriptableObject.CreateInstance<GunConfig>());
        weapon.WeaponPrefab = Own(new GameObject("Missing animator weapon template"));
        presenter.ShowWeapon(weapon);
        Assert.IsNull(Get<GameObject>(presenter.GetComponent<BasementWeaponPresentation>(), "rightWeapon"));
        var selectable = presenter.gameObject.AddComponent<CharacterSelectable>();
        selectable.syncDefToParentSlot = false;
        selectable.BindPresentation(presenter);
        selectable.SetPicked(true);
        Assert.IsTrue(Get<bool>(selectable, "_picked"));
        Assert.IsNotNull(Get<ZLZ.AnimeShader.ZLZ_SelectionController>(selectable, "_outline"));
        selectable.SetPicked(false);
        Assert.IsFalse(Get<bool>(selectable, "_picked"));
    }

    public IEnumerator AllEightPlayAndReactivate()
    {
        foreach (string name in Names)
        {
            var root = Own(Object.Instantiate(PresentationPrefab.gameObject));
            var def = Definition(name);
            Assert.IsTrue(root.GetComponent<BasementCharacterPresentation>().Build(def));
            var controller = root.GetComponent<BasementAnimationController>();
            var animancer = root.GetComponent<AnimancerComponent>();
            var profile = def.basementAnimationProfile;
            // This isolated test scene has no rendering camera. Preserve authored culling
            // on the asset, but evaluate the test instance's bones even when invisible.
            controller.Animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            controller.ResetToIdle();
            Assert.AreSame(profile.idle.Clip, animancer.States.Current.Clip, name);
            var bones = controller.Animator.transform.GetComponentsInChildren<Transform>();
            var before = bones.Select(t => t.localRotation).ToArray();
            controller.SetPicked(true);
            animancer.Evaluate(0.5f);
            Assert.AreSame(profile.pickup.Clip, animancer.States.Current.Clip, name);
            Assert.IsTrue(bones.Where((t, i) => Quaternion.Angle(before[i], t.localRotation) > 0.1f).Any(), name + " skeleton did not animate");
            controller.SetPicked(false);
            root.SetActive(false);
            root.SetActive(true);
            yield return null;
            Assert.IsFalse(controller.IsPicked, name);
            Assert.AreSame(profile.idle.Clip, animancer.States.Current.Clip, name);
            Object.DestroyImmediate(root);
        }
    }

    GunConfig PreviewWeapon()
    {
        var weapon = Own(ScriptableObject.CreateInstance<GunConfig>());
        weapon.WeaponPrefab = Own(new GameObject("Weapon template"));
        weapon.overrideWeaponModelMountOffset = true;
        weapon.rightHandWeaponModelMount = new WeaponModelMountOffset
        {
            localPosition = new Vector3(0.1f, 0.2f, 0.3f),
            localRotationEuler = new Vector3(10f, 20f, 30f),
            localScale = Vector3.one * 0.75f
        };
        weapon.leftHandWeaponModelMount = WeaponModelMountOffset.Identity;
        weapon.leftHandWeaponModelMount.localPosition = new Vector3(-0.1f, 0.3f, 0.2f);
        return weapon;
    }

    public void SharedWeaponsMountAndClearWithModel()
    {
        var owner = Own(Object.Instantiate(PresentationPrefab));
        var def = Own(Object.Instantiate(Definition("Roma")));
        def.weaponHandMode = CharacterWeaponHandMode.BothHands;
        Assert.IsTrue(owner.Build(def));
        var weapons = owner.GetComponent<BasementWeaponPresentation>();
        Assert.IsNotNull(weapons);
        var weapon = PreviewWeapon();
        owner.ShowWeapon(weapon);
        var right = Get<GameObject>(weapons, "rightWeapon");
        var left = Get<GameObject>(weapons, "leftWeapon");
        Assert.IsNotNull(right);
        Assert.IsNotNull(left);
        Assert.AreEqual("Weapon.R", right.transform.parent.name);
        Assert.AreEqual("Weapon.L", left.transform.parent.name);
        Assert.AreEqual(weapon.rightHandWeaponModelMount.localPosition, right.transform.localPosition);
        Assert.AreEqual(weapon.leftHandWeaponModelMount.localPosition, left.transform.localPosition);
        Assert.AreEqual(weapon.rightHandWeaponModelMount.localScale, right.transform.localScale);
        Assert.Less(Quaternion.Angle(Quaternion.Euler(weapon.rightHandWeaponModelMount.localRotationEuler), right.transform.localRotation), 0.01f);
        owner.ShowWeapon(weapon);
        Assert.AreSame(right, Get<GameObject>(weapons, "rightWeapon"), "Repeated refresh must reuse the mounted object.");
        def.weaponHandMode = CharacterWeaponHandMode.LeftHand;
        owner.ShowWeapon(weapon);
        Assert.IsTrue(right == null);
        Assert.AreSame(left, Get<GameObject>(weapons, "leftWeapon"));

        var oldModel = owner.Model;
        Assert.IsTrue(owner.Build(Definition("Aires")));
        Assert.IsTrue(oldModel == null && left == null);
        Assert.IsNull(Get<GameObject>(weapons, "leftWeapon"));
        owner.ShowWeapon(weapon);
        Assert.IsNotNull(Get<GameObject>(weapons, "rightWeapon"));
        owner.ShowWeapon(null);
        Assert.IsNull(Get<GameObject>(weapons, "rightWeapon"));
        owner.ShowWeapon(weapon);
        owner.Clear();
        Assert.IsNull(Get<GameObject>(weapons, "rightWeapon"));
        Assert.IsNull(owner.Animator);
    }

    public IEnumerator ModelSwapReleasesWeaponsBeforeDestroy()
    {
        var owner = Own(Object.Instantiate(PresentationPrefab));
        var weapon = PreviewWeapon();
        owner.Build(Definition("Roma"));
        owner.ShowWeapon(weapon);
        var oldModel = owner.Model;
        var weapons = owner.GetComponent<BasementWeaponPresentation>();
        var oldWeapon = Get<GameObject>(weapons, "rightWeapon");
        Assert.IsNotNull(oldWeapon);
        owner.SetPicked(true);
        owner.Build(Definition("Aires"));
        Assert.IsFalse(oldModel.activeSelf);
        Assert.IsFalse(oldWeapon.activeSelf);
        Assert.IsFalse(owner.IsPicked);
        owner.ShowWeapon(weapon);
        var newWeapon = Get<GameObject>(weapons, "rightWeapon");
        Assert.IsTrue(newWeapon.transform.IsChildOf(owner.Model.transform));
        yield return null;
        Assert.IsTrue(oldModel == null && oldWeapon == null);
        Assert.IsNotNull(newWeapon);
        owner.Clear();
        yield return null;
        Assert.IsTrue(newWeapon == null);
    }

    public IEnumerator NonLoopPickupHoldsPoseInPlayMode()
    {
        var controller = Create(out var animancer, out var profile);
        controller.SetPicked(true);
        var state = animancer.States.Current;
        // Advance beyond the end and verify the actual sampled pose, not a callback.
        for (int i = 0; i < 20; i++)
        {
            animancer.Evaluate(0.1f);
            yield return null;
        }
        var bone = controller.transform.Find("Bone");
        Assert.Greater(state.Time, state.Length);
        Assert.AreEqual(profile.pickup.Speed, state.Speed);
        Assert.AreEqual(1f, bone.localPosition.x, 0.001);
        Assert.IsTrue(controller.IsPicked);
        double time = state.Time;
        controller.SetPicked(true);
        Assert.AreEqual(time, state.Time, 0.0001);
        animancer.Evaluate(0.5f);
        yield return null;
        Assert.AreEqual(1f, bone.localPosition.x, 0.001);
        controller.SetPicked(false);
        Assert.AreSame(profile.idle.Clip, animancer.States.Current.Clip);
        for (int i = 0; i < 3; i++) { animancer.Evaluate(0.1f); yield return null; }
        Assert.Less(bone.localPosition.x, 0.9f, "Release should leave the held pose.");
        controller.SetPicked(true);
        Assert.AreEqual(0f, animancer.States.Current.Time, 0.0001);
    }
}
#endif
