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
    [System.NonSerialized] AnimationVfxFollowAnchor visualPreviewFollowAnchor;
    [System.NonSerialized] ParticleSystem[] visualPreviewParticleSystems = System.Array.Empty<ParticleSystem>();
    [System.NonSerialized] bool[] visualPreviewOriginalLooping = System.Array.Empty<bool>();
    [System.NonSerialized] MonoBehaviour[] cartoonFxPreviewBehaviours = System.Array.Empty<MonoBehaviour>();
    [System.NonSerialized] bool cartoonFxPreviewBehavioursCached;
    [System.NonSerialized] bool timelinePreviewActive;
    [System.NonSerialized] float timelinePreviewElapsedTime;
    [System.NonSerialized] float timelinePreviewStopTime = -1f;
    [System.NonSerialized] bool timelinePreviewAllowFinish;
    [System.NonSerialized] float timelinePreviewStopExtraLife;
    [System.NonSerialized] bool timelinePreviewCompleted;
    [System.NonSerialized] float timelinePreviewCompletedTime;
    [System.NonSerialized] float timelinePreviewCompletedStopTime = -1f;
    [System.NonSerialized] bool timelinePreviewCompletedAllowFinish;
    [System.NonSerialized] float timelinePreviewCompletedExtraLife;

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
    [PropertyTooltip("ลำดับคิว VFX แบบเก่า เริ่มนับจาก 0 และจะแสดงเฉพาะ Entry ที่ไม่ได้อยู่ใต้ Skill VFX Authoring Slot")]
    private int cueIndex;

    [SerializeField]
    [PropertyTooltip("กำหนดการทำงานของ VFX: One Shot เล่นหนึ่งครั้ง, Start Loop เริ่มเอฟเฟกต์ต่อเนื่อง และ Stop Loop หยุดเอฟเฟกต์ที่ใช้ Loop Key เดียวกัน")]
    private AnimationVfxAction action;

    [SerializeField, HideInInspector]
    private GameObject prefab;

    [ShowInInspector, ReadOnly, ShowIf(nameof(RequiresPrefab)), LabelText("VFX Prefab From Child")]
    [PropertyTooltip("Prefab VFX ที่อ่านจากลูกโดยตรงของ Entry นี้ ตำแหน่ง การหมุน และขนาดของลูกจะถูกใช้เป็นค่าจัดวางตอนบันทึก")]
    private GameObject AuthoredPrefab => ResolvePrefabAsset();

    [SerializeField, ShowIf(nameof(RequiresPrefab)), OnValueChanged(nameof(OnAnchorChanged))]
    [PropertyTooltip("จุดอ้างอิงบนตัวละครที่ใช้คำนวณตำแหน่งเกิดของ VFX")]
    private AnimationVfxAnchor anchor = AnimationVfxAnchor.CastOrigin;

    [SerializeField, ShowIf(nameof(ShowAnchorPath)), LabelText("$AnchorPathLabel")]
    [PropertyTooltip("พาธของ Transform ใต้รากตัวละคร ใช้ระบุตำแหน่ง Custom Anchor หรือ Generic Bone")]
    private string customAnchorPath;

    [SerializeField, ShowIf(nameof(ShowHumanoidBone))]
    [PropertyTooltip("กระดูก Humanoid ที่ใช้เป็นจุดอ้างอิงของ VFX")]
    private HumanBodyBones humanoidBone = HumanBodyBones.RightHand;

    [SerializeField, HideInInspector]
    private bool parentToAnchor;

    [ShowInInspector, ShowIf(nameof(RequiresPrefab)), LabelText("Anchor Mode")]
    [PropertyTooltip("World Space ให้ VFX อยู่ที่ตำแหน่งเกิดเดิม ส่วน Follow Anchor ให้ VFX เคลื่อนที่ตาม Anchor ที่เลือก")]
    private AnimationVfxAnchorMode AnchorMode
    {
        get => parentToAnchor ? AnimationVfxAnchorMode.FollowAnchor : AnimationVfxAnchorMode.WorldSpace;
        set => parentToAnchor = value == AnimationVfxAnchorMode.FollowAnchor;
    }

    [SerializeField, ShowIf(nameof(UsesLoopKey))]
    [PropertyTooltip("ชื่อกลุ่มของ VFX แบบ Loop โดย Start Loop และ Stop Loop ต้องใช้ค่าเดียวกันจึงจะควบคุมเอฟเฟกต์ชุดเดียวกัน")]
    private string loopKey;

    [SerializeField, Min(0f), ShowIf(nameof(ShowExtraLife))]
    [PropertyTooltip("ถ้า VFX ไม่มี Particle System, Trail Renderer และ Animation Clip ค่านี้คืออายุรวมทั้งหมด มิฉะนั้นจะเป็นเวลาที่บวกเพิ่มก่อนทำลาย")]
    private float extraLife;

    [SerializeField, ShowIf(nameof(ShowStopOptions))]
    [PropertyTooltip("เปิดไว้เพื่อหยุดปล่อยอนุภาคใหม่และรอให้อนุภาคเดิมเล่นจนจบ ปิดไว้เพื่อลบ VFX ทันที")]
    private bool allowParticlesToFinish = true;

    [SerializeField, ShowIf(nameof(RequiresPrefab))]
    [PropertyTooltip("Animation Clip ของตัว VFX ที่ใช้สำหรับสุ่มตัวอย่างและแสดงผลใน Visual Preview")]
    private AnimationClip animClip;

    public int CueIndex
    {
        get
        {
            SkillVfxAuthoringSlot slot = GetComponentInParent<SkillVfxAuthoringSlot>();
            return slot != null ? slot.CueIndex : cueIndex;
        }
    }
    public SkillVfxAction Action => (SkillVfxAction)action;
    public AnimationVfxAction AnimationAction => action;
    public GameObject Prefab => ResolvePrefabAsset();
    public SkillVfxAnchor Anchor => (SkillVfxAnchor)anchor;
    public AnimationVfxAnchor AnimationAnchor => anchor;
    public AnimationVfxAnchorMode AnimationAnchorMode => parentToAnchor
        ? AnimationVfxAnchorMode.FollowAnchor
        : AnimationVfxAnchorMode.WorldSpace;
    public string CustomAnchorPath => customAnchorPath;
    public HumanBodyBones HumanoidBone => humanoidBone;
    public bool ParentToAnchor => parentToAnchor;
    public string LoopKey => loopKey;
    public float ExtraLife => extraLife;
    public bool AllowParticlesToFinish => allowParticlesToFinish;
    public bool RequiresPrefab => action != AnimationVfxAction.StopLoop;
    public AnimationClip AnimClip => animClip;

    private bool UsesLoopKey => action != AnimationVfxAction.OneShot;
    private bool ShowLegacyCueIndex => GetComponentInParent<SkillVfxAuthoringSlot>() == null;
    private bool ShowAnchorPath => RequiresPrefab &&
                                   (anchor == AnimationVfxAnchor.CustomChildPath ||
                                    anchor == AnimationVfxAnchor.GenericBone);
    private bool ShowGenericBonePath => RequiresPrefab && anchor == AnimationVfxAnchor.GenericBone;
    private string AnchorPathLabel => anchor == AnimationVfxAnchor.GenericBone
        ? "Generic Bone Path"
        : "Custom Anchor Path";
    private bool ShowHumanoidBone => RequiresPrefab && anchor == AnimationVfxAnchor.HumanoidBone;
    private bool ShowExtraLife => action == AnimationVfxAction.OneShot || action == AnimationVfxAction.StopLoop;
    private bool ShowStopOptions => action == AnimationVfxAction.StopLoop;

    void OnAnchorChanged()
    {
        if (anchor == AnimationVfxAnchor.GenericBone)
            parentToAnchor = true;
    }

    [Button("Use Selected Bone"), ShowIf(nameof(ShowGenericBonePath))]
    [PropertyTooltip("ใช้ Transform ที่เลือกอยู่ใน Hierarchy เป็น Generic Bone และบันทึกพาธจากรากตัวละครให้อัตโนมัติ")]
    void UseSelectedGenericBone()
    {
#if UNITY_EDITOR
        SetAnimationVfxData authoring = ResolveAuthoring();
        if (authoring == null)
        {
            Debug.LogWarning("Could not find SetAnimationVfxData for this VFX entry.", this);
            return;
        }

        if (!authoring.TryGetSelectedGenericBonePath(out string path, out string error))
        {
            Debug.LogWarning(error, this);
            return;
        }

        Undo.RecordObject(this, "Set Generic Bone Path");
        customAnchorPath = path;
        parentToAnchor = true;
        EditorUtility.SetDirty(this);
#endif
    }

    public void Configure(IAnimationVfxCue cue)
    {
        if (cue == null)
            return;

        cueIndex = Mathf.Max(0, cue.CueIndex);
        action = cue.Action;
        prefab = cue.Prefab;
        anchor = cue.Anchor;
        customAnchorPath = cue.CustomAnchorPath;
        humanoidBone = cue.HumanoidBone;
        parentToAnchor = cue.AnchorMode == AnimationVfxAnchorMode.FollowAnchor;
        loopKey = cue.LoopKey;
        extraLife = cue.ExtraLife;
        allowParticlesToFinish = cue.AllowParticlesToFinish;
        animClip = cue.AnimClip;
    }

    public void Configure(SkillVfxEvent cue)
    {
        Configure((IAnimationVfxCue)cue);
    }

    public AnimationVfxCue CreateAnimationData()
    {
        return new AnimationVfxCue
        {
            cueIndex = CueIndex,
            action = action,
            prefab = ResolvePrefabAsset(),
            anchor = anchor,
            customAnchorPath = customAnchorPath,
            humanoidBone = humanoidBone,
            anchorMode = AnimationAnchorMode,
            loopKey = loopKey,
            extraLife = extraLife,
            allowParticlesToFinish = allowParticlesToFinish,
            animClip = animClip,
            localScale = Vector3.one,
        };
    }

    public SkillVfxEvent CreateData()
    {
        return new SkillVfxEvent
        {
            cueIndex = CueIndex,
            action = (SkillVfxAction)action,
            prefab = ResolvePrefabAsset(),
            anchor = (SkillVfxAnchor)anchor,
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
    [PropertyOrder(10)]
    [PropertyTooltip("สร้าง Visual Preview ใหม่จากค่าปัจจุบันของ Entry นี้")]
    public void RefreshVisualPreview()
    {
#if UNITY_EDITOR
        DestroyVisualPreview();
        if (!RequiresPrefab)
            return;

        PlayVisualPreview();
#endif
    }

    [ButtonGroup("entryPreviewRow")]
    [Button("Play Visual Preview")]
    [PropertyTooltip("เล่นตัวอย่าง VFX ของ Entry นี้ใน Scene View โดยไม่บันทึกวัตถุตัวอย่างลง Scene")]
    public void PlayVisualPreview()
    {
#if UNITY_EDITOR
        ResolveAuthoring()?.StopTimelineVfxPreview();
        ReleasePlaybackPreview();
        if (!PreparePlaybackPreview(true))
            return;

        for (int i = 0; i < visualPreviewParticleSystems.Length; i++)
        {
            ParticleSystem particleSystem = visualPreviewParticleSystems[i];
            if (particleSystem == null)
                continue;

            ParticleSystem.MainModule main = particleSystem.main;
            main.loop = action == AnimationVfxAction.StartLoop && visualPreviewOriginalLooping[i];
            particleSystem.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            particleSystem.Simulate(0.0001f, withChildren: false, restart: true, fixedTimeStep: false);
        }

        visualPreviewActive = visualPreviewParticleSystems.Length > 0;
        visualPreviewLooping = action == AnimationVfxAction.StartLoop;
        visualPreviewStopping = false;
        visualPreviewElapsedTime = 0.0001f;
        lastPreviewUpdateTime = EditorApplication.timeSinceStartup;
        visualPreviewStopDeadline = 0d;

        EnsureSceneViewParticleEffectsVisible();
        if (visualPreviewActive)
            PreviewCoordinator.Register(this);
#endif
    }

    public bool SampleTimelinePreview(
        float elapsedTime,
        float stopTime,
        bool allowParticlesToFinishAfterStop,
        float stopExtraLife,
        bool forceRestart)
    {
#if UNITY_EDITOR
        if (Application.isPlaying || action == AnimationVfxAction.StopLoop || elapsedTime < 0f ||
            (stopTime >= 0f && !allowParticlesToFinishAfterStop))
        {
            StopTimelinePreview();
            return false;
        }

        float sampleTime = Mathf.Max(0f, elapsedTime);
        float clampedStopTime = stopTime >= 0f ? Mathf.Clamp(stopTime, 0f, sampleTime) : -1f;
        float clampedExtraLife = Mathf.Max(0f, stopExtraLife);
        bool matchesCompletedSample = timelinePreviewCompleted &&
                                      Mathf.Approximately(clampedStopTime, timelinePreviewCompletedStopTime) &&
                                      allowParticlesToFinishAfterStop == timelinePreviewCompletedAllowFinish &&
                                      Mathf.Approximately(clampedExtraLife, timelinePreviewCompletedExtraLife);
        if (!forceRestart && matchesCompletedSample && sampleTime >= timelinePreviewCompletedTime)
            return false;

        timelinePreviewCompleted = false;
        bool wasTimelineActive = timelinePreviewActive;
        bool restart = forceRestart || !wasTimelineActive || sampleTime < timelinePreviewElapsedTime ||
                       !Mathf.Approximately(clampedStopTime, timelinePreviewStopTime) ||
                       allowParticlesToFinishAfterStop != timelinePreviewAllowFinish ||
                       !Mathf.Approximately(stopExtraLife, timelinePreviewStopExtraLife);

        PreviewCoordinator.Unregister(this);
        visualPreviewActive = false;
        visualPreviewLooping = false;
        visualPreviewStopping = false;
        if (!PreparePlaybackPreview(restart))
        {
            StopTimelinePreview();
            return false;
        }

        if (restart)
        {
            UnregisterCartoonFxEditorPreview();
            RegisterCartoonFxEditorPreview();
            RestartTimelineParticles(sampleTime, clampedStopTime);
        }
        else
        {
            AdvanceTimelineParticles(timelinePreviewElapsedTime, sampleTime, clampedStopTime);
        }

        SamplePreviewAnimClip(sampleTime);

        timelinePreviewActive = true;
        timelinePreviewElapsedTime = sampleTime;
        timelinePreviewStopTime = clampedStopTime;
        timelinePreviewAllowFinish = allowParticlesToFinishAfterStop;
        timelinePreviewStopExtraLife = clampedExtraLife;
        visualPreviewFollowAnchor?.ApplyNow();

        bool anyAlive = HasAliveTimelineParticles();
        bool shouldRemainVisible = clampedStopTime < 0f
            ? action == AnimationVfxAction.StartLoop || anyAlive || sampleTime <= visualPreviewDuration
            : anyAlive || sampleTime - clampedStopTime <= visualPreviewDuration + timelinePreviewStopExtraLife;
        if (!shouldRemainVisible)
        {
            ReleasePlaybackPreview();
            timelinePreviewCompleted = true;
            timelinePreviewCompletedTime = sampleTime;
            timelinePreviewCompletedStopTime = clampedStopTime;
            timelinePreviewCompletedAllowFinish = allowParticlesToFinishAfterStop;
            timelinePreviewCompletedExtraLife = clampedExtraLife;
            return false;
        }

        return true;
#else
        return false;
#endif
    }

    public void StopTimelinePreview()
    {
#if UNITY_EDITOR
        if (!timelinePreviewActive && visualPreviewRoot == null)
            return;

        ReleasePlaybackPreview();
        SceneView.RepaintAll();
#endif
    }

    [ButtonGroup("entryPreviewRow")]
    [Button("Stop Visual Preview")]
    [PropertyTooltip("หยุดและลบ Visual Preview ของ Entry นี้")]
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

        visualPreviewFollowAnchor?.ApplyNow();

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

        SamplePreviewAnimClip(visualPreviewElapsedTime);

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
        timelinePreviewActive = false;
        timelinePreviewElapsedTime = 0f;
        timelinePreviewStopTime = -1f;
        timelinePreviewAllowFinish = false;
        timelinePreviewStopExtraLife = 0f;
        timelinePreviewCompleted = false;
        timelinePreviewCompletedTime = 0f;
        timelinePreviewCompletedStopTime = -1f;
        timelinePreviewCompletedAllowFinish = false;
        timelinePreviewCompletedExtraLife = 0f;

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

    bool PreparePlaybackPreview(bool resetPose)
    {
        GameObject authoredInstance = FindAuthoredPrefabInstance();
        GameObject source = authoredInstance != null ? authoredInstance : prefab;
        if (source == null)
            return false;

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
                if (particleSystem == null)
                    continue;

                visualPreviewOriginalLooping[i] = particleSystem.main.loop;

                // Deterministic editor preview: pin the random seed so scrubbing to the same
                // playhead time always reproduces the same particle state (no flicker).
                // Applied once on the throwaway clone - never touches the prefab asset.
                particleSystem.useAutoRandomSeed = false;
                particleSystem.randomSeed = (uint)(i + 1);
                ParticleSystem.MainModule main = particleSystem.main;
                main.useUnscaledTime = true;
            }

            visualPreviewDuration = VfxSpawner.EstimateLifetime(visualPreviewRoot);
            if (animClip != null)
                visualPreviewDuration = Mathf.Max(visualPreviewDuration, animClip.length);
            if (action == AnimationVfxAction.OneShot)
                visualPreviewDuration += Mathf.Max(0f, extraLife);
            resetPose = true;
        }

        if (resetPose)
            ApplyPlaybackPose(authoredInstance, source);
        else
            visualPreviewFollowAnchor?.ApplyNow();

        visualPreviewRoot.SetActive(true);
        RegisterCartoonFxEditorPreview();
        EnsureSceneViewParticleEffectsVisible();
        return true;
    }

    void ApplyPlaybackPose(GameObject authoredInstance, GameObject source)
    {
        Transform placement = authoredInstance != null ? authoredInstance.transform : transform;
        visualPreviewRoot.transform.SetParent(null, true);
        visualPreviewRoot.transform.SetPositionAndRotation(placement.position, placement.rotation);
        visualPreviewRoot.transform.localScale = authoredInstance != null
            ? placement.lossyScale
            : Vector3.Scale(placement.lossyScale, source.transform.localScale);

        RemoveVisualPreviewFollower();
        if (!parentToAnchor)
            return;

        IAnimationVfxCue cue = CreateAnimationData();
        Transform anchorTransform = ResolvePreviewAnchor(cue);
        if (anchorTransform == null)
            return;

        if (anchor == AnimationVfxAnchor.GenericBone)
        {
            Vector3 localPosition = AnimationVfxAnchorResolver.ResolveLocalPosition(
                anchorTransform,
                cue,
                placement.position);
            Quaternion localRotation = Quaternion.Inverse(anchorTransform.rotation) * placement.rotation;
            visualPreviewFollowAnchor = visualPreviewRoot.AddComponent<AnimationVfxFollowAnchor>();
            visualPreviewFollowAnchor.Configure(anchorTransform, localPosition, localRotation);
            visualPreviewFollowAnchor.ApplyNow();
        }
        else
        {
            visualPreviewRoot.transform.SetParent(anchorTransform, true);
        }
    }

    void RestartTimelineParticles(float sampleTime, float stopTime)
    {
        SetTimelineParticleLooping(true);
        StopTimelineParticles(ParticleSystemStopBehavior.StopEmittingAndClear);

        float emittingTime = stopTime >= 0f ? stopTime : sampleTime;
        SimulateTimelineParticles(Mathf.Max(0.0001f, emittingTime), true);
        if (stopTime < 0f)
            return;

        SetTimelineParticleLooping(false);
        StopTimelineParticles(ParticleSystemStopBehavior.StopEmitting);
        float afterStop = sampleTime - stopTime;
        if (afterStop > 0f)
            SimulateTimelineParticles(afterStop, false);
    }

    void AdvanceTimelineParticles(float previousTime, float sampleTime, float stopTime)
    {
        if (sampleTime <= previousTime)
            return;

        if (stopTime < 0f)
        {
            SimulateTimelineParticles(sampleTime - previousTime, false);
            return;
        }

        if (previousTime < stopTime)
        {
            float beforeStop = Mathf.Min(sampleTime, stopTime) - previousTime;
            if (beforeStop > 0f)
                SimulateTimelineParticles(beforeStop, false);
            if (sampleTime < stopTime)
                return;

            SetTimelineParticleLooping(false);
            StopTimelineParticles(ParticleSystemStopBehavior.StopEmitting);
        }

        float afterStop = sampleTime - Mathf.Max(previousTime, stopTime);
        if (afterStop > 0f)
            SimulateTimelineParticles(afterStop, false);
    }

    void SetTimelineParticleLooping(bool enabled)
    {
        for (int i = 0; i < visualPreviewParticleSystems.Length; i++)
        {
            ParticleSystem particleSystem = visualPreviewParticleSystems[i];
            if (particleSystem == null)
                continue;
            ParticleSystem.MainModule main = particleSystem.main;
            main.loop = enabled && action == AnimationVfxAction.StartLoop && visualPreviewOriginalLooping[i];
        }
    }

    void StopTimelineParticles(ParticleSystemStopBehavior behavior)
    {
        for (int i = 0; i < visualPreviewParticleSystems.Length; i++)
        {
            ParticleSystem particleSystem = visualPreviewParticleSystems[i];
            if (particleSystem != null)
                particleSystem.Stop(false, behavior);
        }
    }

    void SimulateTimelineParticles(float time, bool restart)
    {
        if (time <= 0f)
            return;
        for (int i = 0; i < visualPreviewParticleSystems.Length; i++)
        {
            ParticleSystem particleSystem = visualPreviewParticleSystems[i];
            if (particleSystem != null)
                particleSystem.Simulate(time, withChildren: false, restart: restart, fixedTimeStep: false);
        }
    }

    bool HasAliveTimelineParticles()
    {
        for (int i = 0; i < visualPreviewParticleSystems.Length; i++)
        {
            ParticleSystem particleSystem = visualPreviewParticleSystems[i];
            if (particleSystem != null && particleSystem.IsAlive(withChildren: false))
                return true;
        }

        return false;
    }

    void SamplePreviewAnimClip(float time)
    {
        if (animClip == null || visualPreviewRoot == null)
            return;
        float sampleTime = action == AnimationVfxAction.StartLoop
            ? time % Mathf.Max(0.0001f, animClip.length)
            : Mathf.Min(time, animClip.length);
        animClip.SampleAnimation(visualPreviewRoot, sampleTime);
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
        visualPreviewFollowAnchor = null;
    }

    Transform ResolvePreviewAnchor(IAnimationVfxCue cue)
    {
        SetAnimationVfxData authoring = ResolveAuthoring();
        if (authoring != null)
            return authoring.ResolveEntryPreviewAnchor(cue);

        return SkillVfxAnchorResolver.Resolve(transform, CreateData());
    }

    void RemoveVisualPreviewFollower()
    {
        if (visualPreviewFollowAnchor == null && visualPreviewRoot != null)
            visualPreviewFollowAnchor = visualPreviewRoot.GetComponent<AnimationVfxFollowAnchor>();
        if (visualPreviewFollowAnchor != null)
            DestroyImmediate(visualPreviewFollowAnchor);
        visualPreviewFollowAnchor = null;
    }

    SetAnimationVfxData ResolveAuthoring()
    {
        SetAnimationVfxData authoring = GetComponentInParent<SetAnimationVfxData>();
        if (authoring != null)
            return authoring;

        SetAnimationVfxData[] candidates = FindObjectsByType<SetAnimationVfxData>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        for (int i = 0; i < candidates.Length; i++)
        {
            if (candidates[i] != null && candidates[i].ContainsAuthoringTransform(transform))
                return candidates[i];
        }

        return null;
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
