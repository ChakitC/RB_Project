using Sirenix.OdinInspector;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
[HideMonoScript]
public sealed class SkillVfxAuthoringEntry : MonoBehaviour
{
#if UNITY_EDITOR
    [System.NonSerialized] bool visualPreviewActive;
    [System.NonSerialized] bool visualPreviewLooping;
    [System.NonSerialized] bool visualPreviewStopping;
    [System.NonSerialized] double lastPreviewUpdateTime;
    [System.NonSerialized] double visualPreviewStopDeadline;
    [System.NonSerialized] float visualPreviewElapsedTime;
    [System.NonSerialized] float visualPreviewDuration;
    [System.NonSerialized] GameObject visualPreviewRoot;
    [System.NonSerialized] GameObject visualPreviewSource;
    [System.NonSerialized] ParticleSystem[] visualPreviewParticleSystems = System.Array.Empty<ParticleSystem>();
    [System.NonSerialized] bool[] visualPreviewOriginalLooping = System.Array.Empty<bool>();
    [System.NonSerialized] MonoBehaviour[] cartoonFxPreviewBehaviours = System.Array.Empty<MonoBehaviour>();
    [System.NonSerialized] bool cartoonFxPreviewBehavioursCached;

    public static int ActivePreviewCount => PreviewCoordinator.ActiveCount;
    public static int ActivePreviewParticleSystemCount => PreviewCoordinator.ActiveParticleSystemCount;
    public static int ActivePreviewCartoonFxCallbackCount => PreviewCoordinator.ActiveCartoonFxCallbackCount;
    public static float ActivePreviewUpdateFps => PreviewCoordinator.UpdateFps;
    public static bool HasActivePreviews => PreviewCoordinator.ActiveCount > 0;
    public static event System.Action PreviewActivityChanged
    {
        add => PreviewCoordinator.ActivityChanged += value;
        remove => PreviewCoordinator.ActivityChanged -= value;
    }

    public static bool UpdateActivePreviews(double editorTime)
    {
        return PreviewCoordinator.Update(editorTime);
    }

    public static void CleanupOrphanedVisualPreviews()
    {
        PreviewCoordinator.CleanupOrphanedPlaybackObjects();
    }
#endif

    [SerializeField, Min(0), LabelText("Legacy VFX Cue Index"), ShowIf(nameof(ShowLegacyCueIndex))]
    private int cueIndex;

    [SerializeField]
    private SkillVfxAction action;

    [SerializeField, HideInInspector]
    private GameObject prefab;

    [ShowInInspector, ReadOnly, ShowIf(nameof(RequiresPrefab)), LabelText("VFX Prefab From Child")]
    private GameObject AuthoredPrefab => ResolvePrefabAsset();

    [SerializeField, ShowIf(nameof(RequiresPrefab))]
    private SkillVfxAnchor anchor = SkillVfxAnchor.CastOrigin;

    [SerializeField, ShowIf(nameof(ShowCustomAnchorPath))]
    private string customAnchorPath;

    [SerializeField, ShowIf(nameof(ShowHumanoidBone))]
    private HumanBodyBones humanoidBone = HumanBodyBones.RightHand;

    [SerializeField, HideInInspector]
    private bool parentToAnchor;

    [ShowInInspector, ShowIf(nameof(RequiresPrefab)), LabelText("Anchor Mode")]
    [PropertyTooltip("World Space keeps the spawned VFX at its spawn position. Follow Anchor moves it with the selected anchor.")]
    private SkillVfxAnchorMode AnchorMode
    {
        get => parentToAnchor ? SkillVfxAnchorMode.FollowAnchor : SkillVfxAnchorMode.WorldSpace;
        set => parentToAnchor = value == SkillVfxAnchorMode.FollowAnchor;
    }

    [SerializeField, ShowIf(nameof(UsesLoopKey))]
    private string loopKey;

    [SerializeField, Min(0f), ShowIf(nameof(ShowExtraLife))]
    private float extraLife;

    [SerializeField, ShowIf(nameof(ShowStopOptions))]
    private bool allowParticlesToFinish = true;

    public int CueIndex
    {
        get
        {
            SkillVfxAuthoringSlot slot = GetComponentInParent<SkillVfxAuthoringSlot>();
            return slot != null ? slot.CueIndex : cueIndex;
        }
    }
    public SkillVfxAction Action => action;
    public GameObject Prefab => ResolvePrefabAsset();
    public SkillVfxAnchor Anchor => anchor;
    public string CustomAnchorPath => customAnchorPath;
    public HumanBodyBones HumanoidBone => humanoidBone;
    public bool ParentToAnchor => parentToAnchor;
    public string LoopKey => loopKey;
    public float ExtraLife => extraLife;
    public bool AllowParticlesToFinish => allowParticlesToFinish;
    public bool RequiresPrefab => action != SkillVfxAction.StopLoop;

    private bool UsesLoopKey => action != SkillVfxAction.OneShot;
    private bool ShowLegacyCueIndex => GetComponentInParent<SkillVfxAuthoringSlot>() == null;
    private bool ShowCustomAnchorPath => RequiresPrefab && anchor == SkillVfxAnchor.CustomChildPath;
    private bool ShowHumanoidBone => RequiresPrefab && anchor == SkillVfxAnchor.HumanoidBone;
    private bool ShowExtraLife => action == SkillVfxAction.OneShot || action == SkillVfxAction.StopLoop;
    private bool ShowStopOptions => action == SkillVfxAction.StopLoop;

    public void Configure(SkillVfxEvent cue)
    {
        if (cue == null)
            return;

        cueIndex = Mathf.Max(0, cue.cueIndex);
        action = cue.action;
        prefab = cue.prefab;
        anchor = cue.anchor;
        customAnchorPath = cue.customAnchorPath;
        humanoidBone = cue.humanoidBone;
        parentToAnchor = cue.parentToAnchor;
        loopKey = cue.loopKey;
        extraLife = cue.extraLife;
        allowParticlesToFinish = cue.allowParticlesToFinish;
    }

    public SkillVfxEvent CreateData()
    {
        return new SkillVfxEvent
        {
            cueIndex = CueIndex,
            action = action,
            prefab = ResolvePrefabAsset(),
            anchor = anchor,
            customAnchorPath = customAnchorPath,
            humanoidBone = humanoidBone,
            parentToAnchor = parentToAnchor,
            loopKey = loopKey,
            extraLife = extraLife,
            allowParticlesToFinish = allowParticlesToFinish,
            localScale = Vector3.one,
        };
    }

    public Transform GetPlacementTransform()
    {
#if UNITY_EDITOR
        GameObject instance = FindAuthoredPrefabInstance();
        if (instance != null)
            return instance.transform;
#endif
        return transform;
    }

    public GameObject ResolvePrefabAsset()
    {
#if UNITY_EDITOR
        GameObject instance = FindAuthoredPrefabInstance();
        if (instance != null)
        {
            GameObject source = PrefabUtility.GetCorrespondingObjectFromSource(instance);
            if (source != null)
                return source;
        }
#endif
        return prefab;
    }

    public int GetAuthoredPrefabInstanceCount()
    {
#if UNITY_EDITOR
        int count = 0;
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (IsDirectPrefabInstanceRoot(child.gameObject))
                count++;
        }

        return count;
#else
        return 0;
#endif
    }

    public GameObject CreateAuthoredPrefabInstance(string undoLabel)
    {
#if UNITY_EDITOR
        GameObject existing = FindAuthoredPrefabInstance();
        if (existing != null || !RequiresPrefab || prefab == null)
            return existing;

        GameObject instance = PrefabUtility.InstantiatePrefab(prefab, transform) as GameObject;
        if (instance == null)
            instance = Instantiate(prefab, transform);

        if (instance != null)
        {
            instance.name = prefab.name;
            Undo.RegisterCreatedObjectUndo(instance, undoLabel);
            prefab = null;
        }

        return instance;
#else
        return null;
#endif
    }

    public void SetCueIndex(int value)
    {
        cueIndex = Mathf.Max(0, value);
    }

    void OnDisable()
    {
#if UNITY_EDITOR
        DestroyPlaybackPreview();
#endif
    }

    void OnDestroy()
    {
#if UNITY_EDITOR
        DestroyPlaybackPreview();
#endif
    }

    [Button("Refresh Visual Preview")]
    public void RefreshVisualPreview()
    {
#if UNITY_EDITOR
        DestroyVisualPreview();
        if (!RequiresPrefab)
            return;

        PlayVisualPreview();
#endif
    }

    [Button("Play Visual Preview")]
    public void PlayVisualPreview()
    {
#if UNITY_EDITOR
        ReleasePlaybackPreview();

        GameObject authoredInstance = FindAuthoredPrefabInstance();
        GameObject source = authoredInstance != null ? authoredInstance : prefab;
        if (source == null)
            return;

        Transform placement = authoredInstance != null ? authoredInstance.transform : transform;
        if (visualPreviewRoot == null || visualPreviewSource != source)
        {
            DestroyPlaybackPreview();
            visualPreviewRoot = Instantiate(source);
            visualPreviewSource = source;
            visualPreviewRoot.name = $"__SkillVfxPlayback_Cue{CueIndex + 1}";
            SetPlaybackHideFlags(visualPreviewRoot.transform);
            visualPreviewParticleSystems = visualPreviewRoot.GetComponentsInChildren<ParticleSystem>(true);
            visualPreviewOriginalLooping = new bool[visualPreviewParticleSystems.Length];
            for (int i = 0; i < visualPreviewParticleSystems.Length; i++)
            {
                ParticleSystem particleSystem = visualPreviewParticleSystems[i];
                if (particleSystem != null)
                    visualPreviewOriginalLooping[i] = particleSystem.main.loop;
            }

            visualPreviewDuration = CalculatePreviewDuration(visualPreviewParticleSystems);
        }

        visualPreviewRoot.transform.SetParent(null, true);
        visualPreviewRoot.transform.SetPositionAndRotation(placement.position, placement.rotation);
        visualPreviewRoot.transform.localScale = authoredInstance != null
            ? placement.lossyScale
            : Vector3.Scale(placement.lossyScale, source.transform.localScale);

        if (parentToAnchor)
        {
            Transform anchorTransform = SkillVfxAnchorResolver.Resolve(transform, CreateData());
            if (anchorTransform != null)
                visualPreviewRoot.transform.SetParent(anchorTransform, true);
        }

        visualPreviewRoot.SetActive(true);
        RegisterCartoonFxEditorPreview();

        for (int i = 0; i < visualPreviewParticleSystems.Length; i++)
        {
            ParticleSystem particleSystem = visualPreviewParticleSystems[i];
            if (particleSystem == null)
                continue;

            ParticleSystem.MainModule main = particleSystem.main;
            main.loop = action == SkillVfxAction.StartLoop && visualPreviewOriginalLooping[i];
            particleSystem.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            particleSystem.Simulate(0.0001f, withChildren: false, restart: true, fixedTimeStep: false);
        }

        visualPreviewActive = visualPreviewParticleSystems.Length > 0;
        visualPreviewLooping = action == SkillVfxAction.StartLoop;
        visualPreviewStopping = false;
        visualPreviewElapsedTime = 0.0001f;
        lastPreviewUpdateTime = EditorApplication.timeSinceStartup;
        visualPreviewStopDeadline = 0d;

        EnsureSceneViewParticleEffectsVisible();
        if (visualPreviewActive)
            PreviewCoordinator.Register(this);
#endif
    }

    [Button("Stop Visual Preview")]
    public void StopVisualPreview()
    {
#if UNITY_EDITOR
        for (int i = 0; i < visualPreviewParticleSystems.Length; i++)
        {
            ParticleSystem particleSystem = visualPreviewParticleSystems[i];
            if (particleSystem != null)
                particleSystem.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        visualPreviewActive = false;
        visualPreviewLooping = false;
        visualPreviewStopping = false;
        DestroyPlaybackPreview();

        SceneView.RepaintAll();
#endif
    }

    public bool UpdateVisualPreview(double editorTime)
    {
#if UNITY_EDITOR
        if (!visualPreviewActive || Application.isPlaying)
            return false;

        float deltaTime = Mathf.Clamp((float)(editorTime - lastPreviewUpdateTime), 0f, 0.1f);
        lastPreviewUpdateTime = editorTime;
        visualPreviewElapsedTime += deltaTime;

        bool anyAlive = false;
        for (int i = 0; i < visualPreviewParticleSystems.Length; i++)
        {
            ParticleSystem particleSystem = visualPreviewParticleSystems[i];
            if (particleSystem == null)
                continue;

            if (deltaTime > 0f)
                particleSystem.Simulate(deltaTime, withChildren: false, restart: false, fixedTimeStep: false);

            if (particleSystem.IsAlive(withChildren: false))
                anyAlive = true;
        }

        if (visualPreviewStopping)
            visualPreviewActive = anyAlive || editorTime < visualPreviewStopDeadline;
        else
            visualPreviewActive = visualPreviewLooping ||
                                  visualPreviewElapsedTime < visualPreviewDuration ||
                                  anyAlive;

        if (!visualPreviewActive)
            ReleasePlaybackPreview();

        return visualPreviewActive;
#else
        return false;
#endif
    }

    public void DestroyVisualPreview()
    {
#if UNITY_EDITOR
        visualPreviewActive = false;
        DestroyPlaybackPreview();
#endif
    }

    public void StopLoopVisualPreview(bool allowParticlesToFinish, float stopExtraLife)
    {
#if UNITY_EDITOR
        if (!visualPreviewActive || visualPreviewRoot == null || !allowParticlesToFinish)
        {
            StopVisualPreview();
            return;
        }

        double now = EditorApplication.timeSinceStartup;
        UpdateVisualPreview(now);
        for (int i = 0; i < visualPreviewParticleSystems.Length; i++)
        {
            ParticleSystem particleSystem = visualPreviewParticleSystems[i];
            if (particleSystem == null)
                continue;

            ParticleSystem.MainModule main = particleSystem.main;
            main.loop = false;
            particleSystem.Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }

        visualPreviewLooping = false;
        visualPreviewStopping = true;
        lastPreviewUpdateTime = now;
        visualPreviewStopDeadline = now + visualPreviewDuration + Mathf.Max(0f, stopExtraLife);
#endif
    }

#if UNITY_EDITOR
    void ReleasePlaybackPreview()
    {
        PreviewCoordinator.Unregister(this);
        visualPreviewActive = false;
        visualPreviewLooping = false;
        visualPreviewStopping = false;
        visualPreviewElapsedTime = 0f;
        visualPreviewStopDeadline = 0d;

        for (int i = 0; i < visualPreviewParticleSystems.Length; i++)
        {
            ParticleSystem particleSystem = visualPreviewParticleSystems[i];
            if (particleSystem != null)
                particleSystem.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        UnregisterCartoonFxEditorPreview();
        if (visualPreviewRoot != null)
            visualPreviewRoot.SetActive(false);
    }

    void DestroyPlaybackPreview()
    {
        ReleasePlaybackPreview();
        visualPreviewDuration = 0f;
        visualPreviewParticleSystems = System.Array.Empty<ParticleSystem>();
        visualPreviewOriginalLooping = System.Array.Empty<bool>();
        cartoonFxPreviewBehaviours = System.Array.Empty<MonoBehaviour>();
        cartoonFxPreviewBehavioursCached = false;

        if (visualPreviewRoot != null)
            DestroyImmediate(visualPreviewRoot);

        visualPreviewRoot = null;
        visualPreviewSource = null;
    }

    static void EnsureSceneViewParticleEffectsVisible()
    {
        for (int i = 0; i < SceneView.sceneViews.Count; i++)
        {
            SceneView sceneView = SceneView.sceneViews[i] as SceneView;
            if (sceneView == null)
                continue;

            bool requiresRepaint = !sceneView.sceneViewState.fxEnabled ||
                                   !sceneView.sceneViewState.showParticleSystems;
            sceneView.sceneViewState.fxEnabled = true;
            sceneView.sceneViewState.showParticleSystems = true;
            if (requiresRepaint)
                sceneView.Repaint();
        }
    }

    static void SetPlaybackHideFlags(Transform root)
    {
        if (root == null)
            return;

        root.gameObject.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild | HideFlags.NotEditable;
        for (int i = 0; i < root.childCount; i++)
            SetPlaybackHideFlags(root.GetChild(i));
    }

    void RegisterCartoonFxEditorPreview()
    {
        if (visualPreviewRoot == null)
            return;

        if (!cartoonFxPreviewBehavioursCached)
        {
            MonoBehaviour[] behaviours = visualPreviewRoot.GetComponentsInChildren<MonoBehaviour>(true);
            var previewBehaviours = new System.Collections.Generic.List<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour != null && behaviour.GetType().FullName == "CartoonFX.CFXR_Effect")
                    previewBehaviours.Add(behaviour);
            }

            cartoonFxPreviewBehaviours = previewBehaviours.ToArray();
            cartoonFxPreviewBehavioursCached = true;
        }

        for (int i = 0; i < cartoonFxPreviewBehaviours.Length; i++)
        {
            MonoBehaviour behaviour = cartoonFxPreviewBehaviours[i];
            if (behaviour == null)
                continue;

            System.Reflection.MethodInfo registerMethod = behaviour.GetType().GetMethod(
                "RegisterEditorUpdate",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            registerMethod?.Invoke(behaviour, null);
        }
    }

    void UnregisterCartoonFxEditorPreview()
    {
        for (int i = 0; i < cartoonFxPreviewBehaviours.Length; i++)
        {
            MonoBehaviour behaviour = cartoonFxPreviewBehaviours[i];
            if (behaviour == null)
                continue;

            System.Reflection.MethodInfo unregisterMethod = behaviour.GetType().GetMethod(
                "UnregisterEditorUpdate",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            unregisterMethod?.Invoke(behaviour, null);
        }

    }

    static class PreviewCoordinator
    {
        const string PlaybackNamePrefix = "__SkillVfxPlayback_";

        static readonly System.Collections.Generic.List<SkillVfxAuthoringEntry> activeEntries =
            new System.Collections.Generic.List<SkillVfxAuthoringEntry>();

        static double lastUpdateTime;
        static float updateFps;

        public static event System.Action ActivityChanged;

        public static int ActiveCount => activeEntries.Count;
        public static float UpdateFps => updateFps;

        public static int ActiveParticleSystemCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < activeEntries.Count; i++)
                {
                    SkillVfxAuthoringEntry entry = activeEntries[i];
                    if (entry != null)
                        count += entry.visualPreviewParticleSystems.Length;
                }

                return count;
            }
        }

        public static int ActiveCartoonFxCallbackCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < activeEntries.Count; i++)
                {
                    SkillVfxAuthoringEntry entry = activeEntries[i];
                    if (entry == null)
                        continue;

                    for (int j = 0; j < entry.cartoonFxPreviewBehaviours.Length; j++)
                    {
                        if (entry.cartoonFxPreviewBehaviours[j] != null)
                            count++;
                    }
                }

                return count;
            }
        }

        [InitializeOnLoadMethod]
        static void Initialize()
        {
            EditorApplication.delayCall += CleanupOrphanedPlaybackObjects;
            AssemblyReloadEvents.beforeAssemblyReload -= CleanupAll;
            AssemblyReloadEvents.beforeAssemblyReload += CleanupAll;
            EditorApplication.quitting -= CleanupAll;
            EditorApplication.quitting += CleanupAll;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        public static void Register(SkillVfxAuthoringEntry entry)
        {
            if (entry == null || activeEntries.Contains(entry))
                return;

            activeEntries.Add(entry);
            ActivityChanged?.Invoke();
        }

        public static void Unregister(SkillVfxAuthoringEntry entry)
        {
            if (!activeEntries.Remove(entry))
                return;

            if (activeEntries.Count == 0)
            {
                lastUpdateTime = 0d;
                updateFps = 0f;
            }

            ActivityChanged?.Invoke();
        }

        public static bool Update(double now)
        {
            bool hadWork = activeEntries.Count > 0;
            if (!hadWork)
                return false;

            if (lastUpdateTime > 0d)
            {
                float currentFps = (float)(1d / System.Math.Max(0.0001d, now - lastUpdateTime));
                updateFps = updateFps <= 0f ? currentFps : Mathf.Lerp(updateFps, currentFps, 0.2f);
            }

            lastUpdateTime = now;
            for (int i = activeEntries.Count - 1; i >= 0; i--)
            {
                SkillVfxAuthoringEntry entry = activeEntries[i];
                if (entry == null || !entry.UpdateVisualPreview(now))
                {
                    activeEntries.Remove(entry);
                    continue;
                }
            }

            if (activeEntries.Count == 0)
            {
                lastUpdateTime = 0d;
                updateFps = 0f;
                ActivityChanged?.Invoke();
            }

            return hadWork;
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.EnteredPlayMode)
                CleanupAll();
        }

        static void CleanupAll()
        {
            while (activeEntries.Count > 0)
            {
                int index = activeEntries.Count - 1;
                SkillVfxAuthoringEntry entry = activeEntries[index];
                activeEntries.RemoveAt(index);
                if (entry != null)
                    entry.DestroyPlaybackPreview();
            }

            lastUpdateTime = 0d;
            updateFps = 0f;
            ActivityChanged?.Invoke();
            CleanupOrphanedPlaybackObjects();
        }

        public static void CleanupOrphanedPlaybackObjects()
        {
            GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
            SkillVfxAuthoringEntry[] entries = Resources.FindObjectsOfTypeAll<SkillVfxAuthoringEntry>();
            for (int i = 0; i < objects.Length; i++)
            {
                GameObject candidate = objects[i];
                if (candidate == null || !candidate.name.StartsWith(PlaybackNamePrefix, System.StringComparison.Ordinal) ||
                    !candidate.scene.IsValid())
                {
                    continue;
                }

                bool hasOwner = false;
                for (int j = 0; j < entries.Length; j++)
                {
                    if (entries[j] != null && entries[j].visualPreviewRoot == candidate)
                    {
                        hasOwner = true;
                        break;
                    }
                }

                if (!hasOwner)
                    DestroyImmediate(candidate);
            }
        }
    }

    static float CalculatePreviewDuration(ParticleSystem[] particleSystems)
    {
        float duration = 0.5f;
        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem particleSystem = particleSystems[i];
            if (particleSystem == null)
                continue;

            ParticleSystem.MainModule main = particleSystem.main;
            duration = Mathf.Max(
                duration,
                main.duration + GetMaximumCurveValue(main.startDelay) + GetMaximumCurveValue(main.startLifetime));
        }

        return duration;
    }

    static float GetMaximumCurveValue(ParticleSystem.MinMaxCurve curve)
    {
        switch (curve.mode)
        {
            case ParticleSystemCurveMode.Constant:
                return curve.constant;
            case ParticleSystemCurveMode.TwoConstants:
                return curve.constantMax;
            case ParticleSystemCurveMode.Curve:
                return GetMaximumCurveValue(curve.curve, curve.curveMultiplier);
            case ParticleSystemCurveMode.TwoCurves:
                return Mathf.Max(
                    GetMaximumCurveValue(curve.curveMin, curve.curveMultiplier),
                    GetMaximumCurveValue(curve.curveMax, curve.curveMultiplier));
            default:
                return 0f;
        }
    }

    static float GetMaximumCurveValue(AnimationCurve curve, float multiplier)
    {
        if (curve == null || curve.length == 0)
            return 0f;

        float maximum = 0f;
        Keyframe[] keys = curve.keys;
        for (int i = 0; i < keys.Length; i++)
            maximum = Mathf.Max(maximum, keys[i].value * multiplier);

        return maximum;
    }

    GameObject FindAuthoredPrefabInstance()
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (IsDirectPrefabInstanceRoot(child.gameObject))
                return child.gameObject;
        }

        return null;
    }

    static bool IsDirectPrefabInstanceRoot(GameObject candidate)
    {
        return candidate != null &&
               PrefabUtility.IsPartOfPrefabInstance(candidate) &&
               PrefabUtility.GetNearestPrefabInstanceRoot(candidate) == candidate;
    }
#endif
}
