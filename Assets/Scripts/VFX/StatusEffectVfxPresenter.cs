using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class StatusEffectVfxPresenter : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private StatusEffectController controller;
    [SerializeField] private Transform rootAnchor;
    [SerializeField] private Transform centerAnchor;
    [SerializeField] private Transform feetAnchor;
    [SerializeField] private Transform headAnchor;
    [SerializeField] private Animator animator;

    [Header("Fallback Offsets")]
    [SerializeField] private Vector3 centerOffset = new Vector3(0f, 1f, 0f);
    [SerializeField] private Vector3 headOffset = new Vector3(0f, 1.7f, 0f);

    [Header("Debug")]
    [SerializeField] private bool warnWhenSpawnerMissing;

    readonly Dictionary<StatusEffectInstance, ActiveLoop> _activeLoops = new Dictionary<StatusEffectInstance, ActiveLoop>();
    readonly List<StatusEffectInstance> _removeBuffer = new List<StatusEffectInstance>();
    readonly Dictionary<string, float> _lastCueTimes = new Dictionary<string, float>();

    bool _subscribed;

    void Awake()
    {
        ResolveReferences();
    }

    void OnEnable()
    {
        ResolveReferences();
        Subscribe();
        SyncActiveLoops();
    }

    void OnDisable()
    {
        Unsubscribe();
        StopAllLoops(false);
    }

    void Subscribe()
    {
        if (_subscribed || controller == null || !isActiveAndEnabled)
            return;

        controller.EffectLifecycleChanged += HandleEffectLifecycleChanged;
        controller.EffectsChanged += SyncActiveLoops;
        _subscribed = true;
    }

    void Unsubscribe()
    {
        if (!_subscribed || controller == null)
            return;

        controller.EffectLifecycleChanged -= HandleEffectLifecycleChanged;
        controller.EffectsChanged -= SyncActiveLoops;
        _subscribed = false;
    }

    public void Bind(StatusEffectController newController)
    {
        if (controller == newController)
        {
            ResolveReferences();
            Subscribe();
            SyncActiveLoops();
            return;
        }

        Unsubscribe();
        controller = newController;
        ResolveReferences();
        Subscribe();
        SyncActiveLoops();
    }

    void ResolveReferences()
    {
        if (!controller)
            TryGetComponent(out controller);

        if (!controller)
            controller = GetComponentInParent<StatusEffectController>();

        if (!controller)
            controller = GetComponentInChildren<StatusEffectController>(true);

        if (!controller)
        {
            var ownerContext = GetComponentInParent<CharacteContext>();
            if (ownerContext)
                controller = ownerContext.GetComponentInChildren<StatusEffectController>(true);
        }

        if (!rootAnchor)
            rootAnchor = transform;

        if (!animator)
            animator = GetComponentInChildren<Animator>(true);

        if (!animator)
            animator = GetComponentInParent<Animator>();
    }

    void HandleEffectLifecycleChanged(StatusEffectEvent statusEvent)
    {
        StatusEffectDef definition = statusEvent.Definition;
        StatusEffectVfxProfile profile = definition ? definition.vfxProfile : null;
        if (!profile)
            return;

        switch (statusEvent.EventType)
        {
            case StatusEffectEventType.AppliedNew:
                PlayCue(profile, profile.applyBurst, statusEvent.Instance, "apply");
                StartOrUpdateLoop(statusEvent.Instance);
                break;

            case StatusEffectEventType.Refreshed:
                PlayCue(profile, profile.refreshBurst, statusEvent.Instance, "refresh");
                StartOrUpdateLoop(statusEvent.Instance);
                break;

            case StatusEffectEventType.StackChanged:
                PlayCue(profile, profile.stackBurst, statusEvent.Instance, "stack");
                StartOrUpdateLoop(statusEvent.Instance);
                break;

            case StatusEffectEventType.Removed:
                StopLoop(statusEvent.Instance, true);
                PlayCue(profile, profile.removeBurst, statusEvent.Instance, "remove");
                break;

            case StatusEffectEventType.Ticked:
                PlayCue(profile, profile.tickBurst, statusEvent.Instance, "tick");
                break;
        }
    }

    void SyncActiveLoops()
    {
        if (controller == null)
            return;

        IReadOnlyList<StatusEffectInstance> activeEffects = controller.ActiveEffects;
        if (activeEffects != null)
        {
            for (int i = 0; i < activeEffects.Count; i++)
                StartOrUpdateLoop(activeEffects[i]);
        }

        _removeBuffer.Clear();
        foreach (var pair in _activeLoops)
        {
            if (!IsStillActive(pair.Key, activeEffects))
                _removeBuffer.Add(pair.Key);
        }

        for (int i = 0; i < _removeBuffer.Count; i++)
            StopLoop(_removeBuffer[i], true);

        _removeBuffer.Clear();
    }

    bool IsStillActive(StatusEffectInstance instance, IReadOnlyList<StatusEffectInstance> activeEffects)
    {
        if (instance == null || activeEffects == null)
            return false;

        for (int i = 0; i < activeEffects.Count; i++)
        {
            if (ReferenceEquals(activeEffects[i], instance))
                return true;
        }

        return false;
    }

    void StartOrUpdateLoop(StatusEffectInstance instance)
    {
        StatusEffectDef definition = instance?.Definition;
        StatusEffectVfxProfile profile = definition ? definition.vfxProfile : null;
        StatusEffectVfxCue cue = profile ? profile.activeLoop : null;

        if (instance == null || instance.CurrentStacks <= 0 || profile == null || cue == null || !cue.IsEnabled)
        {
            StopLoop(instance, true);
            return;
        }

        if (_activeLoops.TryGetValue(instance, out ActiveLoop existing))
        {
            existing.ApplyStackScale(instance.CurrentStacks);
            return;
        }

        Transform anchor = ResolveAnchor(cue, out Vector3 fallbackOffset);
        Vector3 position = ResolvePosition(anchor, fallbackOffset, cue);
        Quaternion rotation = ResolveRotation(anchor, cue);
        Transform parent = cue.parentToAnchor ? anchor : null;
        float scale = Mathf.Max(0f, cue.scale);

        GameObject loopObject = SpawnLoop(cue.prefab, position, rotation, parent, scale);
        if (!loopObject)
            return;

        var activeLoop = new ActiveLoop(loopObject, profile, cue, loopObject.transform.localScale);
        _activeLoops[instance] = activeLoop;
        activeLoop.ApplyStackScale(instance.CurrentStacks);
    }

    void StopLoop(StatusEffectInstance instance, bool allowParticlesToFinish)
    {
        if (instance == null || !_activeLoops.TryGetValue(instance, out ActiveLoop activeLoop))
            return;

        _activeLoops.Remove(instance);

        if (!activeLoop.Root)
            return;

        if (VfxSpawner.Instance != null)
        {
            VfxSpawner.Instance.StopLoopingVfx(
                activeLoop.Root,
                allowParticlesToFinish,
                activeLoop.Cue != null ? activeLoop.Cue.extraLife : 0f);
            return;
        }

        Destroy(activeLoop.Root, allowParticlesToFinish ? 2f : 0f);
    }

    void StopAllLoops(bool allowParticlesToFinish)
    {
        _removeBuffer.Clear();
        foreach (var pair in _activeLoops)
            _removeBuffer.Add(pair.Key);

        for (int i = 0; i < _removeBuffer.Count; i++)
            StopLoop(_removeBuffer[i], allowParticlesToFinish);

        _removeBuffer.Clear();
    }

    void PlayCue(StatusEffectVfxProfile profile, StatusEffectVfxCue cue, StatusEffectInstance instance, string cueName)
    {
        if (profile == null || cue == null || !cue.IsEnabled || !CanPlayCue(profile, cue, instance, cueName))
            return;

        Transform anchor = ResolveAnchor(cue, out Vector3 fallbackOffset);
        Vector3 position = ResolvePosition(anchor, fallbackOffset, cue);
        Quaternion rotation = ResolveRotation(anchor, cue);
        float scale = Mathf.Max(0f, cue.scale);
        Transform parent = cue.parentToAnchor ? anchor : null;

        SpawnOneShot(cue.prefab, position, rotation, cue.extraLife, scale, parent);
    }

    bool CanPlayCue(StatusEffectVfxProfile profile, StatusEffectVfxCue cue, StatusEffectInstance instance, string cueName)
    {
        if (cue.minInterval <= 0f)
            return true;

        int instanceKey = instance != null ? instance.GetHashCode() : 0;
        string key = $"{profile.GetInstanceID()}:{cueName}:{instanceKey}";
        float now = Time.time;

        if (_lastCueTimes.TryGetValue(key, out float lastTime) && now - lastTime < cue.minInterval)
            return false;

        _lastCueTimes[key] = now;
        return true;
    }

    GameObject SpawnOneShot(GameObject prefab, Vector3 position, Quaternion rotation, float extraLife, float scale, Transform parent)
    {
        if (!prefab)
            return null;

        if (VfxSpawner.Instance != null)
            return VfxSpawner.Instance.SpawnVfx(prefab, position, rotation, extraLife, scale, parent: parent);

        if (warnWhenSpawnerMissing)
            Debug.LogWarning($"{nameof(StatusEffectVfxPresenter)} could not find {nameof(VfxSpawner)}. Falling back to local VFX spawn.", this);

        GameObject vfx = Instantiate(prefab, position, rotation, parent);
        vfx.transform.localScale *= scale;
        Destroy(vfx, 2f + Mathf.Max(0f, extraLife));
        return vfx;
    }

    GameObject SpawnLoop(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent, float scale)
    {
        if (!prefab)
            return null;

        if (VfxSpawner.Instance != null)
            return VfxSpawner.Instance.SpawnLoopingVfx(prefab, position, rotation, parent, scale);

        if (warnWhenSpawnerMissing)
            Debug.LogWarning($"{nameof(StatusEffectVfxPresenter)} could not find {nameof(VfxSpawner)}. Falling back to local loop spawn.", this);

        GameObject vfx = Instantiate(prefab, position, rotation, parent);
        vfx.transform.localScale *= scale;
        return vfx;
    }

    Transform ResolveAnchor(StatusEffectVfxCue cue, out Vector3 fallbackOffset)
    {
        fallbackOffset = Vector3.zero;
        Transform root = rootAnchor ? rootAnchor : transform;

        if (cue == null)
            return root;

        switch (cue.anchor)
        {
            case StatusEffectVfxAnchor.Root:
                return root;

            case StatusEffectVfxAnchor.Center:
                if (centerAnchor)
                    return centerAnchor;

                fallbackOffset = centerOffset;
                return root;

            case StatusEffectVfxAnchor.Feet:
                return feetAnchor ? feetAnchor : root;

            case StatusEffectVfxAnchor.Head:
                if (headAnchor)
                    return headAnchor;

                Transform head = ResolveHumanoidHead();
                if (head)
                    return head;

                fallbackOffset = headOffset;
                return root;

            case StatusEffectVfxAnchor.CustomName:
                Transform custom = FindChildByName(root, cue.customAnchorName);
                return custom ? custom : root;

            default:
                return root;
        }
    }

    Vector3 ResolvePosition(Transform anchor, Vector3 fallbackOffset, StatusEffectVfxCue cue)
    {
        Transform resolvedAnchor = anchor ? anchor : transform;
        Vector3 localOffset = fallbackOffset + (cue != null ? cue.localOffset : Vector3.zero);
        return resolvedAnchor.TransformPoint(localOffset);
    }

    Quaternion ResolveRotation(Transform anchor, StatusEffectVfxCue cue)
    {
        Quaternion baseRotation = cue != null && cue.useAnchorRotation && anchor
            ? anchor.rotation
            : transform.rotation;

        Quaternion offsetRotation = cue != null
            ? Quaternion.Euler(cue.rotationEuler)
            : Quaternion.identity;

        return baseRotation * offsetRotation;
    }

    Transform ResolveHumanoidHead()
    {
        if (!animator)
            animator = GetComponentInChildren<Animator>(true);

        if (!animator)
            return null;

        return animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
    }

    static Transform FindChildByName(Transform root, string targetName)
    {
        if (!root || string.IsNullOrWhiteSpace(targetName))
            return null;

        if (root.name == targetName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChildByName(root.GetChild(i), targetName);
            if (found)
                return found;
        }

        return null;
    }

    sealed class ActiveLoop
    {
        public readonly GameObject Root;
        public readonly StatusEffectVfxProfile Profile;
        public readonly StatusEffectVfxCue Cue;
        readonly Vector3 _baseLocalScale;

        public ActiveLoop(GameObject root, StatusEffectVfxProfile profile, StatusEffectVfxCue cue, Vector3 baseLocalScale)
        {
            Root = root;
            Profile = profile;
            Cue = cue;
            _baseLocalScale = baseLocalScale;
        }

        public void ApplyStackScale(int stacks)
        {
            if (!Root || !Profile)
                return;

            Root.transform.localScale = _baseLocalScale * Profile.GetLoopScaleMultiplier(stacks);
        }
    }
}
