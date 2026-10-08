#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public sealed class VfxLifecycleTestFixture
{
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    readonly List<Object> created = new();
    VfxSpawner spawner;
    VfxSpawner previousSpawner;
    float previousTimeScale;
    static readonly FieldInfo InstanceField = typeof(VfxSpawner).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);

    public void SetUp()
    {
        previousTimeScale = Time.timeScale;
        previousSpawner = (VfxSpawner)InstanceField.GetValue(null);
        InstanceField.SetValue(null, null);
        spawner = New("VFX test spawner").AddComponent<VfxSpawner>();
        InstanceField.SetValue(null, spawner);
    }

    public void TearDown()
    {
        Time.timeScale = previousTimeScale;
        var pool = spawner != null ? spawner.GetComponent<VfxPool>() : null;
        foreach (var handle in Resources.FindObjectsOfTypeAll<PooledVfxHandle>())
            if (handle != null && pool != null && handle.Pool == pool) Object.DestroyImmediate(handle.gameObject);
        for (int i = created.Count - 1; i >= 0; i--)
            if (created[i] != null) Object.DestroyImmediate(created[i]);
        created.Clear();
        InstanceField.SetValue(null, previousSpawner);
    }

    GameObject New(string name)
    {
        var go = new GameObject(name); created.Add(go); return go;
    }

    GameObject ParticlePrefab()
    {
        var prefab = New("VFX test particle");
        prefab.SetActive(false);
        var ps = prefab.AddComponent<ParticleSystem>();
        var main = ps.main;
        main.duration = .1f; main.startLifetime = .1f; main.startSpeed = 0;
        main.loop = true; main.playOnAwake = true; main.stopAction = ParticleSystemStopAction.Destroy;
        return prefab;
    }

    static float Lifetime(GameObject instance) => (float)typeof(WorldTimeScaledVfx)
        .GetField("_destroyAfterWorldSeconds", Hidden).GetValue(instance.GetComponent<WorldTimeScaledVfx>());

    public void ExplicitAndAutoLifetime()
    {
        var prefab = ParticlePrefab();
        var auto = spawner.SpawnVfx(prefab, Vector3.zero, Quaternion.identity, extraLife: .2f, minimumLifetime: 2f);
        Assert.That(Lifetime(auto), Is.EqualTo(2.2f).Within(.001f));
        var exact = spawner.SpawnVfx(prefab, Vector3.zero, Quaternion.identity, extraLife: 99f,
            minimumLifetime: 99f, lifetimeMode: VfxLifetimeMode.Explicit, explicitLifetime: .5f);
        Assert.That(Lifetime(exact), Is.EqualTo(.5f));
        var mesh = New("VFX mesh-only"); mesh.SetActive(false);
        Assert.That(Lifetime(spawner.SpawnVfx(mesh, Vector3.zero, Quaternion.identity, extraLife: .8f)), Is.EqualTo(.8f));
    }

    public void ReuseResetsStateAndRejectsStaleLease()
    {
        var prefab = ParticlePrefab(); prefab.AddComponent<VfxLifecycleProbe>();
        var child = New("VFX child"); child.transform.SetParent(prefab.transform); child.transform.localPosition = Vector3.up;
        var first = spawner.SpawnVfx(prefab, Vector3.zero, Quaternion.identity);
        var lease = new VfxLease(first);
        first.GetComponent<VfxLifecycleProbe>().Value = 42;
        first.transform.GetChild(0).localPosition = Vector3.one * 10;
        first.layer = 2;
        lease.Stop(); lease.Stop();
        var reused = spawner.SpawnLoopingVfx(prefab, Vector3.right, Quaternion.identity);
        Assert.That(reused, Is.SameAs(first));
        Assert.That(reused.GetComponent<ParticleSystem>().main.loop, Is.True);
        Assert.That(reused.GetComponent<ParticleSystem>().main.stopAction, Is.EqualTo(ParticleSystemStopAction.None));
        Assert.That(reused.layer, Is.EqualTo(prefab.layer));
        Assert.That(reused.transform.GetChild(0).localPosition, Is.EqualTo(Vector3.up));
        var probe = reused.GetComponent<VfxLifecycleProbe>();
        Assert.That(probe.Value, Is.Zero); Assert.That(probe.ReturnCount, Is.EqualTo(1));
        Assert.That(probe.RentCount, Is.EqualTo(2)); Assert.That(probe.RentWasInactive, Is.True);
        lease.Stop(); Assert.That(reused.activeSelf, Is.True);
        Assert.That((bool)typeof(WorldTimeScaledVfx).GetField("_destroyAfterLifetime", Hidden)
            .GetValue(reused.GetComponent<WorldTimeScaledVfx>()), Is.False);
    }

    public void ConfigureRunsBeforeOnEnable()
    {
        var prefab = ParticlePrefab(); prefab.AddComponent<VfxLifecycleProbe>();
        var instance = spawner.SpawnVfx(prefab, Vector3.right, Quaternion.identity,
            configure: go => { Assert.That(go.activeInHierarchy, Is.False); go.transform.position = Vector3.up * 3; });
        var probe = instance.GetComponent<VfxLifecycleProbe>();
        Assert.That(probe.EnableCount, Is.EqualTo(1));
        Assert.That(probe.EnabledPosition, Is.EqualTo(Vector3.up * 3));
    }

    public void SessionEndDetachesTailAndIsolatesActors()
    {
        var prefab = ParticlePrefab(); var owner = New("VFX owner");
        owner.transform.localScale = Vector3.one * 2;
        var presenter = owner.AddComponent<AnimationVfxPresenter>();
        var cue = new AnimationVfxCue { prefab = prefab, action = AnimationVfxAction.StartLoop,
            loopKey = "same", anchorMode = AnimationVfxAnchorMode.FollowAnchor,
            sessionEndMode = VfxSessionEndMode.FinishParticles };
        var track = new AnimationVfxTrack(); track.ReplaceCues(new[] { cue });
        var anchors = new AnimationVfxAnchorContext(owner.transform, owner.transform, owner.transform, null);
        var first = presenter.BeginSession(track, anchors);
        var second = presenter.BeginSession(track, anchors);
        presenter.HandleCue(first, 0); presenter.HandleCue(second, 0);
        var handles = new List<PooledVfxHandle>();
        foreach (var handle in Resources.FindObjectsOfTypeAll<PooledVfxHandle>())
            if (handle.SourcePrefab == prefab && handle.gameObject.activeSelf) handles.Add(handle);
        Assert.That(handles.Count, Is.EqualTo(2));
        owner.transform.position = Vector3.one * 4;
        foreach (var handle in handles)
        {
            Assert.That(handle.transform.lossyScale, Is.EqualTo(Vector3.one * 2));
            handle.GetComponent<AnimationVfxFollowAnchor>().ApplyNow();
            Assert.That(handle.transform.position, Is.EqualTo(owner.transform.position));
        }
        presenter.EndSession(first); presenter.EndSession(first);
        int tails = 0;
        foreach (var handle in handles)
            if (!handle.GetComponent<AnimationVfxFollowAnchor>().enabled)
            {
                tails++; Assert.That(handle.gameObject.activeSelf, Is.True);
                Assert.That(handle.GetComponent<ParticleSystem>().isEmitting, Is.False);
            }
        Assert.That(tails, Is.EqualTo(1));
        if (!Application.isPlaying) presenter.EndSession(second); // EditMode does not invoke OnDisable.
        Object.DestroyImmediate(owner);
        foreach (var handle in handles) Assert.That(handle != null && handle.gameObject.activeSelf, Is.True);
    }

    public void DefaultSessionEndIsImmediateAndDebugSwitchWorks()
    {
        var prefab = ParticlePrefab(); var owner = New("VFX immediate owner");
        owner.transform.localScale = Vector3.one * 2;
        var presenter = owner.AddComponent<AnimationVfxPresenter>();
        var track = new AnimationVfxTrack(); track.ReplaceCues(new[] {
            new AnimationVfxCue { prefab = prefab, action = AnimationVfxAction.StartLoop, loopKey = "loop", anchorMode = AnimationVfxAnchorMode.FollowAnchor } });
        var token = presenter.BeginSession(track, new AnimationVfxAnchorContext(owner.transform, null, null, null));
        presenter.HandleCue(token, 0);
        Assert.That(owner.GetComponentInChildren<PooledVfxHandle>().transform.lossyScale, Is.EqualTo(Vector3.one * 2));
        presenter.EndSession(token);
        foreach (var handle in Resources.FindObjectsOfTypeAll<PooledVfxHandle>())
            if (handle.SourcePrefab == prefab) Assert.That(handle.gameObject.activeSelf, Is.False);
        spawner.SpawnVfxEnabled = false;
        Assert.That(spawner.SpawnVfx(prefab, Vector3.zero, Quaternion.identity), Is.Null);
        Assert.That(spawner.SpawnLoopingVfx(prefab, Vector3.zero, Quaternion.identity), Is.Null);
    }

    public void AuthoringRoundTripAndValidation()
    {
        var prefab = ParticlePrefab(); var entry = New("VFX authoring entry").AddComponent<SkillVfxAuthoringEntry>();
        var cue = new AnimationVfxCue { prefab = prefab, lifetimeMode = VfxLifetimeMode.Explicit,
            explicitLifetime = 1.75f, sessionEndMode = VfxSessionEndMode.FinishParticles };
        entry.Configure(cue);
        var skillCue = entry.CreateData(); entry.Configure(skillCue);
        var actual = entry.CreateAnimationData();
        Assert.That(actual.lifetimeMode, Is.EqualTo(cue.lifetimeMode));
        Assert.That(actual.explicitLifetime, Is.EqualTo(cue.explicitLifetime));
        Assert.That(actual.sessionEndMode, Is.EqualTo(cue.sessionEndMode));
        var issues = new List<string>(); cue.explicitLifetime = 0;
        AnimationVfxValidation.CollectCueIssues(cue, 0, issues);
        Assert.That(issues.Exists(x => x.Contains("Explicit Lifetime")), Is.True);
        cue.prefab = New("VFX mesh"); cue.lifetimeMode = VfxLifetimeMode.Auto;
        issues.Clear(); AnimationVfxValidation.CollectCueIssues(cue, 0, issues);
        Assert.That(issues.Exists(x => x.Contains("cannot estimate lifetime")), Is.True);
    }

    public void AnimatedMeshUsesClipLifetimeAndCanReplay()
    {
        var prefab = New("VFX animated mesh"); prefab.SetActive(false); prefab.AddComponent<Animator>();
        var child = New("AnimatedChild"); child.transform.SetParent(prefab.transform);
        var clip = new AnimationClip(); created.Add(clip);
        clip.SetCurve("AnimatedChild", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0, 0, .5f, 1));
        var owner = New("VFX mesh owner"); var presenter = owner.AddComponent<AnimationVfxPresenter>();
        var track = new AnimationVfxTrack(); track.ReplaceCues(new[] { new AnimationVfxCue { prefab = prefab, animClip = clip } });
        var token = presenter.BeginSession(track, new AnimationVfxAnchorContext(owner.transform, null, null, null));
        GameObject first = null;
        for (int i = 0; i < 2; i++)
        {
            presenter.HandleCue(token, 0);
            foreach (var handle in Resources.FindObjectsOfTypeAll<PooledVfxHandle>())
            {
                if (handle.SourcePrefab != prefab || !handle.gameObject.activeSelf) continue;
                if (first == null) first = handle.gameObject; else Assert.That(handle.gameObject, Is.SameAs(first));
                Assert.That(Lifetime(handle.gameObject), Is.EqualTo(.5f).Within(.001f));
                Assert.That(handle.GetComponent<Animancer.AnimancerComponent>(), Is.Not.Null);
                new VfxLease(handle.gameObject).Stop();
            }
        }
        Assert.That(first, Is.Not.Null);
        presenter.EndSession(token);
    }

    public void PreviewUsesExactDeadlineAndSessionPolicy()
    {
        var prefab = ParticlePrefab(); var entry = New("VFX preview entry").AddComponent<SkillVfxAuthoringEntry>();
        entry.Configure(new AnimationVfxCue { prefab = prefab, lifetimeMode = VfxLifetimeMode.Explicit, explicitLifetime = .3f });
        try
        {
            Assert.That(entry.SampleTimelinePreview(.1f, -1, false, 0, true), Is.True);
            Assert.That(entry.SampleTimelinePreview(.4f, -1, false, 0, false), Is.False);
            entry.Configure(new AnimationVfxCue { prefab = prefab, action = AnimationVfxAction.StartLoop,
                loopKey = "preview", sessionEndMode = VfxSessionEndMode.FinishParticles });
            entry.RefreshVisualPreview(); entry.CompleteSessionVisualPreview();
            Assert.That(entry.UpdateVisualPreview(EditorApplication.timeSinceStartup + 10), Is.False);
        }
        finally { entry.DestroyVisualPreview(); }
    }

    public void ProductionCartoonFxReuse()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/VFX/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Electric/CFXR Lightning Impact.prefab");
        Assert.That(prefab, Is.Not.Null);
        var original = prefab.GetComponentInChildren<CartoonFX.CFXR_Effect>(true).clearBehavior;
        GameObject first = null;
        for (int i = 0; i < 10; i++)
        {
            var instance = spawner.SpawnVfx(prefab, Vector3.zero, Quaternion.identity);
            if (first == null) first = instance; else Assert.That(instance, Is.SameAs(first));
            Assert.That(instance.GetComponentInChildren<CartoonFX.CFXR_Effect>().clearBehavior,
                Is.EqualTo(CartoonFX.CFXR_Effect.ClearBehavior.None));
            new VfxLease(instance).Stop();
        }
        Assert.That(prefab.GetComponentInChildren<CartoonFX.CFXR_Effect>(true).clearBehavior, Is.EqualTo(original));
    }

    public void DetachedFollowPreservesOffsetsScaleAndStackChanges()
    {
        var prefab = ParticlePrefab(); var owner = New("VFX scaled owner");
        owner.transform.localScale = Vector3.one * 2;
        var instance = spawner.SpawnLoopingVfx(prefab, owner.transform.TransformPoint(Vector3.up),
            Quaternion.identity, owner.transform, followWithoutParent: true);
        var follower = instance.GetComponent<AnimationVfxFollowAnchor>();
        Assert.That(instance.transform.parent, Is.Null);
        Assert.That(instance.transform.localScale, Is.EqualTo(Vector3.one * 2));
        instance.transform.localScale *= 3; // Status stack presentation owns this multiplier.
        follower.ApplyNow();
        Assert.That(instance.transform.localScale, Is.EqualTo(Vector3.one * 6));
        owner.transform.localScale = Vector3.one * 4;
        owner.transform.position = Vector3.right;
        follower.ApplyNow();
        Assert.That(instance.transform.position, Is.EqualTo(Vector3.right + Vector3.up * 4));
        Assert.That(instance.transform.localScale, Is.EqualTo(Vector3.one * 12));
        new VfxLease(instance).Stop(true);
        var position = instance.transform.position;
        owner.transform.position = Vector3.zero;
        follower.ApplyNow(); Assert.That(instance.transform.position, Is.EqualTo(position));
    }

    public void LoopReplacementAndExplicitStopOverrideSessionPolicy()
    {
        var prefab = ParticlePrefab(); var owner = New("VFX grouped owner");
        var presenter = owner.AddComponent<AnimationVfxPresenter>();
        var track = new AnimationVfxTrack();
        track.ReplaceCues(new[] {
            new AnimationVfxCue { prefab = prefab, action = AnimationVfxAction.StartLoop, loopKey = "group", sessionEndMode = VfxSessionEndMode.FinishParticles },
            new AnimationVfxCue { prefab = prefab, action = AnimationVfxAction.StartLoop, loopKey = "group", sessionEndMode = VfxSessionEndMode.FinishParticles },
            new AnimationVfxCue { cueIndex = 1, action = AnimationVfxAction.StopLoop, loopKey = "group", allowParticlesToFinish = false } });
        var token = presenter.BeginSession(track, new AnimationVfxAnchorContext(owner.transform, null, null, null));
        presenter.HandleCue(token, 0);
        var old = new List<VfxLease>();
        foreach (var handle in Resources.FindObjectsOfTypeAll<PooledVfxHandle>())
            if (handle.SourcePrefab == prefab && handle.gameObject.activeSelf) old.Add(new VfxLease(handle.gameObject));
        Assert.That(old.Count, Is.EqualTo(2));
        presenter.HandleCue(token, 0);
        foreach (var lease in old) Assert.That(lease.IsValid, Is.False);
        presenter.HandleCue(token, 1); presenter.EndSession(token);
        foreach (var handle in Resources.FindObjectsOfTypeAll<PooledVfxHandle>())
            if (handle.SourcePrefab == prefab) Assert.That(handle.gameObject.activeSelf, Is.False);
    }

    public void MigratedGameplayImpactUsesCentralClockAndSwitch()
    {
        var prefab = ParticlePrefab(); var owner = New("VFX guard owner"); owner.SetActive(false);
        var context = owner.AddComponent<EnemyContext>();
        var guard = owner.AddComponent<DefensiveBlockController>();
        guard.impactVfx = prefab;
        typeof(DefensiveBlockController).GetField("ctx", Hidden).SetValue(guard, context);
        typeof(DefensiveBlockController).GetField("impactVfxLifetime", Hidden).SetValue(guard, .7f);
        var method = typeof(DefensiveBlockController).GetMethod("SpawnImpactVfx", Hidden);
        var instance = (GameObject)method.Invoke(guard, new object[] { Vector3.one });
        Assert.That(instance.GetComponent<PooledVfxHandle>(), Is.Not.Null);
        Assert.That(instance.transform.position, Is.EqualTo(Vector3.one));
        Assert.That(Lifetime(instance), Is.EqualTo(.7f));
        Assert.That((bool)typeof(WorldTimeScaledVfx).GetField("_useWorldTimeScale", Hidden)
            .GetValue(instance.GetComponent<WorldTimeScaledVfx>()), Is.False);
        spawner.SpawnVfxEnabled = false;
        Assert.That(method.Invoke(guard, new object[] { Vector3.one }), Is.Null);

        var payload = ScriptableObject.CreateInstance<TargetedDeliverySkillPayloadDef>(); created.Add(payload);
        typeof(TargetedDeliverySkillPayloadDef).GetField("impactVfxPrefab", Hidden).SetValue(payload, prefab);
        typeof(TargetedDeliverySkillPayloadDef).GetField("impactVfxLifetime", Hidden).SetValue(payload, .9f);
        var delivery = owner.AddComponent<TargetedDeliveryRuntime>();
        typeof(TargetedDeliveryRuntime).GetField("payload", Hidden).SetValue(delivery, payload);
        typeof(TargetedDeliveryRuntime).GetField("flightEndPoint", Hidden).SetValue(delivery, Vector3.right * 5);
        spawner.SpawnVfxEnabled = true;
        typeof(TargetedDeliveryRuntime).GetMethod("PlayImpactFeedback", Hidden).Invoke(delivery, null);
        bool found = false;
        foreach (var handle in Resources.FindObjectsOfTypeAll<PooledVfxHandle>())
            if (handle.SourcePrefab == prefab && handle.transform.position == Vector3.right * 5)
            { found = true; Assert.That(Lifetime(handle.gameObject), Is.EqualTo(.9f)); }
        Assert.That(found, Is.True);
    }

    public IEnumerator RuntimeLifecycleCallbacks()
    {
        ConfigureRunsBeforeOnEnable();
        SessionEndDetachesTailAndIsolatesActors();
        var prefab = ParticlePrefab(); var owner = New("VFX disabled parent");
        var instance = spawner.SpawnLoopingVfx(prefab, Vector3.zero, Quaternion.identity, owner.transform);
        owner.SetActive(false);
        new VfxLease(instance).Stop();
        yield return null;
        yield return null;
        Assert.That(instance.transform.parent, Is.EqualTo(spawner.GetComponent<VfxPool>().GetPoolRoot()));
        Assert.That(instance.activeSelf, Is.False);
        var active = spawner.SpawnLoopingVfx(prefab, Vector3.zero, Quaternion.identity);
        spawner.gameObject.SetActive(false);
        Assert.That(active.activeSelf, Is.False);
        spawner.gameObject.SetActive(true);
        yield return null;
    }

    public IEnumerator RuntimeProductionEffects()
    {
        string[] paths = {
            "Assets/VFX/Hovl Studio/AAA Projectiles Vol 1/Prefabs/Flash and hits/Hit 11 orange arrow.prefab",
            "Assets/VFX/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Electric/CFXR Lightning Impact.prefab",
            "Assets/VFX/Lana Studio/Casual RPG VFX/Prefabs/Backlight_resources/backlight_health.Aires_drop.prefab" };
        var instances = new List<GameObject>();
        for (int i = 0; i < paths.Length; i++)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]);
            Assert.That(prefab, Is.Not.Null, paths[i]);
            instances.Add(spawner.SpawnVfx(prefab, new Vector3((i - 1) * 3, 0, 0), Quaternion.identity,
                lifetimeMode: VfxLifetimeMode.Explicit, explicitLifetime: .8f, useWorldTimeScale: false,
                configure: go => { foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 31; }));
        }
        yield return new WaitForSecondsRealtime(.12f);
        var camera = New("VFX capture camera").AddComponent<Camera>();
        camera.transform.position = new Vector3(0, 3, -10);
        camera.transform.LookAt(Vector3.zero);
        camera.orthographic = true; camera.orthographicSize = 4; camera.cullingMask = 1 << 31;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
        var rt = new RenderTexture(900, 450, 24); var texture = new Texture2D(900, 450, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0, 0, 900, 450), 0, 0); texture.Apply();
            var folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../../BuildArtifacts/VfxLifecycle"));
            System.IO.Directory.CreateDirectory(folder);
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder, "production-vfx.png"), texture.EncodeToPNG());
        }
        finally { RenderTexture.active = previous; camera.targetTexture = null; Object.DestroyImmediate(texture); Object.DestroyImmediate(rt); }
        yield return new WaitForSeconds(.9f);
        foreach (var instance in instances)
        {
            Assert.That(instance != null && !instance.activeSelf, Is.True,
                instance == null ? "Production effect destroyed itself" : instance.name + " age=" +
                typeof(WorldTimeScaledVfx).GetField("_age", Hidden).GetValue(instance.GetComponent<WorldTimeScaledVfx>()));
            var prefab = instance.GetComponent<PooledVfxHandle>().SourcePrefab;
            var reused = spawner.SpawnVfx(prefab, Vector3.zero, Quaternion.identity,
                lifetimeMode: VfxLifetimeMode.Explicit, explicitLifetime: .1f, useWorldTimeScale: false);
            Assert.That(reused, Is.SameAs(instance));
        }
        yield return new WaitForSeconds(.3f);
        foreach (var instance in instances) Assert.That(instance.activeSelf, Is.False);
    }

    public IEnumerator RuntimeLifetimePauseAndWorldSlow()
    {
        var prefab = ParticlePrefab();
        var clock = TimeSlowManager.Instance;
        int slow = clock.BeginPresentationSlow(.2f);
        try
        {
            var tail = spawner.SpawnLoopingVfx(prefab, Vector3.zero, Quaternion.identity);
            new VfxLease(tail).Stop(true);
            Assert.That(Lifetime(tail), Is.EqualTo(.45f).Within(.001f), "Tail must not count world slow twice");
            var world = spawner.SpawnVfx(prefab, Vector3.zero, Quaternion.identity,
                lifetimeMode: VfxLifetimeMode.Explicit, explicitLifetime: .4f);
            var game = spawner.SpawnVfx(prefab, Vector3.zero, Quaternion.identity,
                lifetimeMode: VfxLifetimeMode.Explicit, explicitLifetime: .4f, useWorldTimeScale: false);
            Time.timeScale = 0;
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(world.activeSelf && game.activeSelf, Is.True);
            Time.timeScale = 1;
            yield return new WaitForSecondsRealtime(.6f);
            Assert.That(game.activeSelf, Is.False); Assert.That(world.activeSelf, Is.True);
            clock.EndPresentationSlow(slow); slow = 0;
            yield return new WaitForSecondsRealtime(.5f);
            Assert.That(world.activeSelf, Is.False);
            Assert.That(tail.activeSelf, Is.False);
        }
        finally { Time.timeScale = previousTimeScale; if (slow != 0) clock.EndPresentationSlow(slow); }
    }

    public IEnumerator RuntimeTailSceneCleanupAndReuse()
    {
        var scene = SceneManager.CreateScene("VFX owned test scene");
        var prefab = ParticlePrefab(); var owner = New("VFX scene owner");
        SceneManager.MoveGameObjectToScene(owner, scene);
        var instance = spawner.SpawnLoopingVfx(prefab, owner.transform.position, Quaternion.identity, owner.transform);
        var old = new VfxLease(instance); old.Stop(true, 10f);
        Assert.That(instance.activeSelf, Is.True);
        yield return SceneManager.UnloadSceneAsync(scene);
        Assert.That(instance != null && !instance.activeSelf, Is.True);
        var reused = spawner.SpawnLoopingVfx(prefab, Vector3.zero, Quaternion.identity);
        Assert.That(reused, Is.SameAs(instance)); old.Stop(); Assert.That(reused.activeSelf, Is.True);
        new VfxLease(reused).Stop(true);
        yield return new WaitForSecondsRealtime(.8f);
        Assert.That(reused.activeSelf, Is.False);
    }
}
#endif
