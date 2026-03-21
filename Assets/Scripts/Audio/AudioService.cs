using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

[DefaultExecutionOrder(-250)]
public class AudioService : MonoBehaviour
{
    [Serializable]
    private sealed class CategoryOutput
    {
        public AudioCategory category = AudioCategory.Sfx;
        [Range(0f, 1f)] public float volume = 1f;
        public AudioMixerGroup mixerGroup;
    }

    private sealed class ActivePlayback
    {
        public int instanceId;
        public AudioCue cue;
        public AudioSource source;
        public Transform followTarget;
        public Vector3 followOffset;
        public bool isFollowing;
        public float baseVolume = 1f;
        public float basePitch = 1f;
        public float volumeMultiplier = 1f;
        public float pitchMultiplier = 1f;
        public float startedAt;
    }

    static AudioService _instance;

    [Header("Pooling")]
    [SerializeField] private int initialPoolSize = 12;
    [SerializeField] private int maxPoolSize = 48;
    [SerializeField] private string pooledSourcePrefix = "AudioSource";

    [Header("Mix")]
    [Range(0f, 1f)] [SerializeField] private float masterVolume = 1f;
    [SerializeField] private List<CategoryOutput> categoryOutputs = new();

    readonly Queue<AudioSource> _availableSources = new();
    readonly List<AudioSource> _allSources = new();
    readonly List<ActivePlayback> _activePlaybacks = new();
    readonly Dictionary<int, ActivePlayback> _activeById = new();
    readonly Dictionary<AudioCue, float> _lastPlayedAt = new();
    readonly Dictionary<AudioCue, int> _lastVariationIndex = new();
    readonly Dictionary<AudioCategory, CategoryOutput> _categoryLookup = new();
    readonly List<IAudioVolumeProvider> _volumeProviders = new();

    Transform _poolRoot;
    int _nextPlaybackId = 1;

    public static AudioService Instance
    {
        get
        {
            if (_instance != null)
                return _instance;

            _instance = FindAnyObjectByType<AudioService>();
            if (_instance != null)
                return _instance;

            var go = new GameObject(nameof(AudioService));
            _instance = go.AddComponent<AudioService>();
            return _instance;
        }
    }

    public static bool HasInstance
    {
        get
        {
            if (_instance != null)
                return true;

            _instance = FindAnyObjectByType<AudioService>();
            return _instance != null;
        }
    }

    void Reset()
    {
        EnsureDefaultCategoryOutputs();
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        EnsurePoolRoot();
        EnsureDefaultCategoryOutputs();
        RebuildCategoryLookup();
        WarmPool();
    }

    void OnValidate()
    {
        EnsureDefaultCategoryOutputs();
        RebuildCategoryLookup();
    }

    void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    void Update()
    {
        CleanupFinishedPlaybacks();
        SyncFollowingSources();
    }

    public void RegisterVolumeProvider(IAudioVolumeProvider provider)
    {
        if (provider == null || _volumeProviders.Contains(provider))
            return;

        _volumeProviders.Add(provider);
        RefreshAllVolumes();
    }

    public void UnregisterVolumeProvider(IAudioVolumeProvider provider)
    {
        if (provider == null)
            return;

        if (_volumeProviders.Remove(provider))
            RefreshAllVolumes();
    }

    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
        RefreshAllVolumes();
    }

    public float GetMasterVolume() => masterVolume;

    public void SetCategoryVolume(AudioCategory category, float volume)
    {
        EnsureDefaultCategoryOutputs();

        for (int i = 0; i < categoryOutputs.Count; i++)
        {
            if (categoryOutputs[i].category != category)
                continue;

            categoryOutputs[i].volume = Mathf.Clamp01(volume);
            RebuildCategoryLookup();
            RefreshAllVolumes();
            return;
        }
    }

    public AudioHandle Play(AudioCue cue)
    {
        return Play(AudioPlaybackRequest.Create(cue));
    }

    public AudioHandle PlayAtPosition(
        AudioCue cue,
        Vector3 worldPosition,
        float volumeMultiplier = 1f,
        float pitchMultiplier = 1f)
    {
        var request = AudioPlaybackRequest.Create(cue);
        request.worldPosition = worldPosition;
        request.hasWorldPosition = true;
        request.volumeMultiplier = volumeMultiplier;
        request.pitchMultiplier = pitchMultiplier;
        return Play(request);
    }

    public AudioHandle PlayAttached(
        AudioCue cue,
        Transform target,
        Vector3 offset,
        float volumeMultiplier = 1f,
        float pitchMultiplier = 1f)
    {
        var request = AudioPlaybackRequest.Create(cue);
        request.followTarget = target;
        request.followOffset = offset;
        request.forceFollowTarget = true;
        request.volumeMultiplier = volumeMultiplier;
        request.pitchMultiplier = pitchMultiplier;
        return Play(request);
    }

    public AudioHandle Play(AudioPlaybackRequest request)
    {
        var cue = request.cue;
        if (cue == null || !cue.HasAnyClip)
            return default;

        CleanupFinishedPlaybacks();

        float now = Time.unscaledTime;
        if (cue.cooldown > 0f &&
            _lastPlayedAt.TryGetValue(cue, out float lastPlayedAt) &&
            now - lastPlayedAt < cue.cooldown)
        {
            return default;
        }

        if (!TryEnforceInstanceLimit(cue))
            return default;

        if (!TryPickVariation(cue, out var variation, out int variationIndex) || variation.clip == null)
            return default;

        var source = AcquireSource();
        if (source == null)
            return default;

        bool shouldFollow = request.followTarget != null && (request.forceFollowTarget || cue.followTarget);
        Vector3 worldPosition = ResolveWorldPosition(request);
        float volumeMultiplier = Mathf.Approximately(request.volumeMultiplier, 0f) ? 1f : Mathf.Max(0f, request.volumeMultiplier);
        float pitchMultiplier = Mathf.Approximately(request.pitchMultiplier, 0f) ? 1f : Mathf.Max(0.01f, request.pitchMultiplier);

        ConfigureSource(source, cue, variation, request, worldPosition);

        var playback = new ActivePlayback
        {
            instanceId = _nextPlaybackId++,
            cue = cue,
            source = source,
            followTarget = request.followTarget,
            followOffset = request.followOffset,
            isFollowing = shouldFollow,
            baseVolume = ResolveBaseVolume(cue, variation),
            basePitch = ResolveBasePitch(cue, variation),
            volumeMultiplier = volumeMultiplier,
            pitchMultiplier = pitchMultiplier,
            startedAt = now
        };

        ApplyPlaybackProperties(playback);
        source.Play();

        _activePlaybacks.Add(playback);
        _activeById[playback.instanceId] = playback;
        _lastPlayedAt[cue] = now;
        _lastVariationIndex[cue] = variationIndex;

        return new AudioHandle(this, playback.instanceId);
    }

    public void StopCategory(AudioCategory category)
    {
        for (int i = _activePlaybacks.Count - 1; i >= 0; i--)
        {
            var playback = _activePlaybacks[i];
            if (playback.cue == null || playback.cue.category != category)
                continue;

            RecyclePlayback(playback);
        }
    }

    public void StopAll()
    {
        for (int i = _activePlaybacks.Count - 1; i >= 0; i--)
            RecyclePlayback(_activePlaybacks[i]);
    }

    internal bool IsHandleValid(int instanceId)
    {
        return _activeById.ContainsKey(instanceId);
    }

    internal bool IsHandlePlaying(int instanceId)
    {
        if (!_activeById.TryGetValue(instanceId, out var playback) || playback.source == null)
            return false;

        return playback.source.isPlaying;
    }

    internal void Stop(int instanceId)
    {
        if (_activeById.TryGetValue(instanceId, out var playback))
            RecyclePlayback(playback);
    }

    internal void SetHandleVolumeMultiplier(int instanceId, float multiplier)
    {
        if (!_activeById.TryGetValue(instanceId, out var playback))
            return;

        playback.volumeMultiplier = Mathf.Max(0f, multiplier);
        ApplyPlaybackProperties(playback);
    }

    internal void SetHandlePitchMultiplier(int instanceId, float multiplier)
    {
        if (!_activeById.TryGetValue(instanceId, out var playback))
            return;

        playback.pitchMultiplier = Mathf.Max(0.01f, multiplier);
        ApplyPlaybackProperties(playback);
    }

    internal void SetHandleWorldPosition(int instanceId, Vector3 worldPosition)
    {
        if (!_activeById.TryGetValue(instanceId, out var playback) || playback.source == null)
            return;

        playback.isFollowing = false;
        playback.followTarget = null;
        playback.source.transform.position = worldPosition;
    }

    internal void SetHandleFollowTarget(int instanceId, Transform target, Vector3 offset)
    {
        if (!_activeById.TryGetValue(instanceId, out var playback) || playback.source == null)
            return;

        playback.followTarget = target;
        playback.followOffset = offset;
        playback.isFollowing = target != null;

        if (target != null)
            playback.source.transform.position = target.position + offset;
    }

    internal void ClearHandleFollowTarget(int instanceId)
    {
        if (!_activeById.TryGetValue(instanceId, out var playback))
            return;

        playback.isFollowing = false;
        playback.followTarget = null;
    }

    void EnsurePoolRoot()
    {
        if (_poolRoot != null)
            return;

        var existing = transform.Find("AudioSources");
        if (existing != null)
        {
            _poolRoot = existing;
            return;
        }

        var root = new GameObject("AudioSources");
        root.transform.SetParent(transform, false);
        _poolRoot = root.transform;
    }

    void EnsureDefaultCategoryOutputs()
    {
        if (categoryOutputs == null)
            categoryOutputs = new List<CategoryOutput>();

        EnsureCategoryOutput(AudioCategory.Sfx);
        EnsureCategoryOutput(AudioCategory.UI);
        EnsureCategoryOutput(AudioCategory.Music);
        EnsureCategoryOutput(AudioCategory.Ambience);
        EnsureCategoryOutput(AudioCategory.Voice);
    }

    void EnsureCategoryOutput(AudioCategory category)
    {
        for (int i = 0; i < categoryOutputs.Count; i++)
        {
            if (categoryOutputs[i].category == category)
                return;
        }

        categoryOutputs.Add(new CategoryOutput { category = category, volume = 1f });
    }

    void RebuildCategoryLookup()
    {
        _categoryLookup.Clear();
        if (categoryOutputs == null)
            return;

        for (int i = 0; i < categoryOutputs.Count; i++)
        {
            var output = categoryOutputs[i];
            if (output == null)
                continue;

            _categoryLookup[output.category] = output;
        }
    }

    void WarmPool()
    {
        int desiredPoolSize = Mathf.Max(0, initialPoolSize);
        for (int i = _allSources.Count; i < desiredPoolSize; i++)
            _availableSources.Enqueue(CreateSource());
    }

    AudioSource AcquireSource()
    {
        while (_availableSources.Count > 0)
        {
            var pooled = _availableSources.Dequeue();
            if (pooled != null)
                return pooled;
        }

        if (maxPoolSize <= 0 || _allSources.Count < maxPoolSize)
            return CreateSource();

        if (_activePlaybacks.Count == 0)
            return null;

        var oldest = _activePlaybacks[0];
        var recycledSource = oldest.source;
        RecyclePlayback(oldest, false);
        return recycledSource;
    }

    AudioSource CreateSource()
    {
        EnsurePoolRoot();

        var go = new GameObject($"{pooledSourcePrefix}_{_allSources.Count:D2}");
        go.transform.SetParent(_poolRoot, false);

        var source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.gameObject.SetActive(false);

        _allSources.Add(source);
        return source;
    }

    void ConfigureSource(
        AudioSource source,
        AudioCue cue,
        AudioCue.Variation variation,
        AudioPlaybackRequest request,
        Vector3 worldPosition)
    {
        source.gameObject.SetActive(true);
        source.transform.position = worldPosition;
        source.Stop();
        source.clip = variation.clip;
        source.time = 0f;
        source.loop = request.overrideLoop ? request.loop : cue.loop;
        source.spatialBlend = cue.spatialBlend;
        source.panStereo = cue.stereoPan;
        source.priority = Mathf.Clamp(cue.priority, 0, 256);
        source.minDistance = Mathf.Max(0f, cue.minDistance);
        source.maxDistance = Mathf.Max(source.minDistance, cue.maxDistance);
        source.dopplerLevel = Mathf.Max(0f, cue.dopplerLevel);
        source.rolloffMode = cue.rolloffMode;
        source.outputAudioMixerGroup = ResolveMixerGroup(cue.category);
    }

    bool TryEnforceInstanceLimit(AudioCue cue)
    {
        int maxInstances = Mathf.Max(0, cue.maxInstances);
        if (maxInstances == 0)
            return true;

        int activeCount = 0;
        ActivePlayback oldestPlayback = null;

        for (int i = 0; i < _activePlaybacks.Count; i++)
        {
            var playback = _activePlaybacks[i];
            if (playback.cue != cue)
                continue;

            activeCount++;
            if (oldestPlayback == null || playback.startedAt < oldestPlayback.startedAt)
                oldestPlayback = playback;
        }

        if (activeCount < maxInstances)
            return true;

        if (!cue.stopOldestInstanceWhenLimitReached || oldestPlayback == null)
            return false;

        RecyclePlayback(oldestPlayback);
        return true;
    }

    bool TryPickVariation(AudioCue cue, out AudioCue.Variation variation, out int variationIndex)
    {
        variation = default;
        variationIndex = -1;

        if (cue == null || cue.variations == null || cue.variations.Count == 0)
            return false;

        int lastIndex = -1;
        _lastVariationIndex.TryGetValue(cue, out lastIndex);

        float totalWeight = 0f;
        int validCount = 0;

        for (int i = 0; i < cue.variations.Count; i++)
        {
            var candidate = cue.variations[i];
            if (candidate.clip == null)
                continue;

            if (cue.avoidImmediateRepeats && i == lastIndex)
                continue;

            totalWeight += Mathf.Max(0.0001f, candidate.weight);
            validCount++;
        }

        if (validCount == 0)
        {
            for (int i = 0; i < cue.variations.Count; i++)
            {
                if (cue.variations[i].clip == null)
                    continue;

                variation = cue.variations[i];
                variationIndex = i;
                return true;
            }

            return false;
        }

        float roll = UnityEngine.Random.value * totalWeight;
        float cursor = 0f;

        for (int i = 0; i < cue.variations.Count; i++)
        {
            var candidate = cue.variations[i];
            if (candidate.clip == null)
                continue;

            if (cue.avoidImmediateRepeats && i == lastIndex)
                continue;

            cursor += Mathf.Max(0.0001f, candidate.weight);
            if (roll > cursor)
                continue;

            variation = candidate;
            variationIndex = i;
            return true;
        }

        return false;
    }

    float ResolveBaseVolume(AudioCue cue, AudioCue.Variation variation)
    {
        float randomMultiplier = UnityEngine.Random.Range(
            Mathf.Min(cue.randomVolumeMultiplier.x, cue.randomVolumeMultiplier.y),
            Mathf.Max(cue.randomVolumeMultiplier.x, cue.randomVolumeMultiplier.y));

        float variationMultiplier = Mathf.Approximately(variation.volumeMultiplier, 0f)
            ? 1f
            : variation.volumeMultiplier;

        return Mathf.Max(0f, cue.baseVolume * randomMultiplier * variationMultiplier);
    }

    float ResolveBasePitch(AudioCue cue, AudioCue.Variation variation)
    {
        float randomMultiplier = UnityEngine.Random.Range(
            Mathf.Min(cue.randomPitchMultiplier.x, cue.randomPitchMultiplier.y),
            Mathf.Max(cue.randomPitchMultiplier.x, cue.randomPitchMultiplier.y));

        float variationMultiplier = Mathf.Approximately(variation.pitchMultiplier, 0f)
            ? 1f
            : variation.pitchMultiplier;

        return Mathf.Max(0.01f, cue.basePitch * randomMultiplier * variationMultiplier);
    }

    Vector3 ResolveWorldPosition(AudioPlaybackRequest request)
    {
        if (request.followTarget != null)
            return request.followTarget.position + request.followOffset;

        if (request.hasWorldPosition)
            return request.worldPosition;

        return transform.position;
    }

    void ApplyPlaybackProperties(ActivePlayback playback)
    {
        if (playback == null || playback.source == null || playback.cue == null)
            return;

        float categoryVolume = ResolveCategoryVolume(playback.cue.category);
        float providerVolume = ResolveProviderVolume(playback.cue, playback.cue.category);

        playback.source.volume = playback.baseVolume *
                                 playback.volumeMultiplier *
                                 masterVolume *
                                 categoryVolume *
                                 providerVolume;

        playback.source.pitch = playback.basePitch * playback.pitchMultiplier;
    }

    float ResolveCategoryVolume(AudioCategory category)
    {
        if (_categoryLookup.TryGetValue(category, out var output) && output != null)
            return Mathf.Clamp01(output.volume);

        return 1f;
    }

    AudioMixerGroup ResolveMixerGroup(AudioCategory category)
    {
        if (_categoryLookup.TryGetValue(category, out var output) && output != null)
            return output.mixerGroup;

        return null;
    }

    float ResolveProviderVolume(AudioCue cue, AudioCategory category)
    {
        float scale = 1f;

        for (int i = 0; i < _volumeProviders.Count; i++)
        {
            var provider = _volumeProviders[i];
            if (provider == null)
                continue;

            scale *= Mathf.Max(0f, provider.GetVolumeScale(cue, category));
        }

        return scale;
    }

    void RefreshAllVolumes()
    {
        for (int i = 0; i < _activePlaybacks.Count; i++)
            ApplyPlaybackProperties(_activePlaybacks[i]);
    }

    void CleanupFinishedPlaybacks()
    {
        for (int i = _activePlaybacks.Count - 1; i >= 0; i--)
        {
            var playback = _activePlaybacks[i];
            if (playback == null || playback.source == null)
            {
                RecyclePlayback(playback);
                continue;
            }

            if (playback.isFollowing && playback.followTarget == null)
            {
                RecyclePlayback(playback);
                continue;
            }

            if (!playback.source.loop && !playback.source.isPlaying)
                RecyclePlayback(playback);
        }
    }

    void SyncFollowingSources()
    {
        for (int i = 0; i < _activePlaybacks.Count; i++)
        {
            var playback = _activePlaybacks[i];
            if (playback == null || playback.source == null || !playback.isFollowing || playback.followTarget == null)
                continue;

            playback.source.transform.position = playback.followTarget.position + playback.followOffset;
        }
    }

    void RecyclePlayback(ActivePlayback playback, bool returnToPool = true)
    {
        if (playback == null)
            return;

        _activeById.Remove(playback.instanceId);
        _activePlaybacks.Remove(playback);

        if (playback.source == null)
            return;

        playback.source.Stop();
        playback.source.clip = null;
        playback.source.loop = false;
        playback.source.outputAudioMixerGroup = null;
        playback.source.transform.SetParent(_poolRoot, false);
        playback.source.transform.localPosition = Vector3.zero;
        playback.source.transform.localRotation = Quaternion.identity;
        playback.source.gameObject.SetActive(!returnToPool);

        if (returnToPool)
            _availableSources.Enqueue(playback.source);
    }
}
