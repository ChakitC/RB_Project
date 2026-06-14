using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SkillVfxPresenter : MonoBehaviour
{
    readonly Dictionary<string, List<GameObject>> _activeLoops = new Dictionary<string, List<GameObject>>();

    CharacterAnimBrain _animBrain;
    SkillGemDefinition _activeSkill;
    int _activeRequestId;

    public void Initialize(CharacterAnimBrain animBrain)
    {
        _animBrain = animBrain;
    }

    public void BeginRequest(int requestId, SkillGemDefinition skill)
    {
        EndRequest(_activeRequestId);
        _activeRequestId = requestId;
        _activeSkill = skill;
    }

    public void HandleVfxCue(int requestId, int cueIndex)
    {
        if (requestId <= 0 || requestId != _activeRequestId || _activeSkill == null || cueIndex < 0)
            return;

        IReadOnlyList<SkillVfxEvent> cues = _activeSkill.SkillVfxEvents;
        var replacedLoopKeys = new HashSet<string>();
        for (int i = 0; i < cues.Count; i++)
        {
            SkillVfxEvent cue = cues[i];
            if (cue == null || cue.cueIndex != cueIndex || cue.action != SkillVfxAction.StartLoop ||
                cue.prefab == null || string.IsNullOrWhiteSpace(cue.loopKey))
            {
                continue;
            }

            string key = cue.loopKey.Trim();
            if (replacedLoopKeys.Add(key))
                StopLoop(key, allowParticlesToFinish: false, extraLife: 0f);
        }

        for (int i = 0; i < cues.Count; i++)
        {
            SkillVfxEvent cue = cues[i];
            if (cue == null || cue.cueIndex != cueIndex)
                continue;

            PlayCue(cue);
        }
    }

    public void EndRequest(int requestId)
    {
        if (requestId > 0 && _activeRequestId > 0 && requestId != _activeRequestId)
            return;

        StopAllLoops();
        _activeRequestId = 0;
        _activeSkill = null;
    }

    void OnDisable()
    {
        EndRequest(_activeRequestId);
    }

    void PlayCue(SkillVfxEvent cue)
    {
        switch (cue.action)
        {
            case SkillVfxAction.StartLoop:
                StartLoop(cue);
                break;

            case SkillVfxAction.StopLoop:
                StopLoop(cue.loopKey, cue.allowParticlesToFinish, cue.extraLife);
                break;

            case SkillVfxAction.OneShot:
            default:
                SpawnOneShot(cue);
                break;
        }
    }

    void SpawnOneShot(SkillVfxEvent cue)
    {
        if (cue.prefab == null || VfxSpawner.Instance == null)
            return;

        Transform anchor = SkillVfxAnchorResolver.Resolve(_animBrain != null ? _animBrain.transform : transform, cue);
        SkillVfxAnchorResolver.ResolvePose(anchor, cue, out Vector3 position, out Quaternion rotation);
        GameObject instance = VfxSpawner.Instance.SpawnVfx(cue.prefab, position, rotation, cue.extraLife);
        ApplyTransformOptions(instance, anchor, cue);
    }

    void StartLoop(SkillVfxEvent cue)
    {
        if (cue.prefab == null || string.IsNullOrWhiteSpace(cue.loopKey) || VfxSpawner.Instance == null)
            return;

        string key = cue.loopKey.Trim();

        Transform anchor = SkillVfxAnchorResolver.Resolve(_animBrain != null ? _animBrain.transform : transform, cue);
        SkillVfxAnchorResolver.ResolvePose(anchor, cue, out Vector3 position, out Quaternion rotation);
        Transform parent = cue.parentToAnchor ? anchor : null;
        GameObject instance = VfxSpawner.Instance.SpawnLoopingVfx(cue.prefab, position, rotation, parent);
        ApplyScale(instance, cue.localScale);

        if (instance == null)
            return;

        if (!_activeLoops.TryGetValue(key, out List<GameObject> instances))
        {
            instances = new List<GameObject>();
            _activeLoops[key] = instances;
        }

        instances.Add(instance);
    }

    void StopLoop(string loopKey, bool allowParticlesToFinish, float extraLife)
    {
        if (string.IsNullOrWhiteSpace(loopKey))
            return;

        string key = loopKey.Trim();
        if (!_activeLoops.TryGetValue(key, out List<GameObject> instances))
            return;

        _activeLoops.Remove(key);
        for (int i = 0; i < instances.Count; i++)
            StopLoopInstance(instances[i], allowParticlesToFinish, extraLife);
    }

    void StopAllLoops()
    {
        if (_activeLoops.Count == 0)
            return;

        var groups = new List<List<GameObject>>(_activeLoops.Values);
        _activeLoops.Clear();

        for (int i = 0; i < groups.Count; i++)
        {
            List<GameObject> instances = groups[i];
            for (int j = 0; j < instances.Count; j++)
                StopLoopInstance(instances[j], allowParticlesToFinish: false, extraLife: 0f);
        }
    }

    void StopLoopInstance(GameObject instance, bool allowParticlesToFinish, float extraLife)
    {
        if (instance == null)
            return;

        if (VfxSpawner.Instance != null)
            VfxSpawner.Instance.StopLoopingVfx(instance, allowParticlesToFinish, extraLife);
        else
            Destroy(instance);
    }

    static void ApplyTransformOptions(GameObject instance, Transform anchor, SkillVfxEvent cue)
    {
        if (instance == null || cue == null)
            return;

        if (cue.parentToAnchor && anchor != null)
            instance.transform.SetParent(anchor, true);

        ApplyScale(instance, cue.localScale);
    }

    static void ApplyScale(GameObject instance, Vector3 scaleMultiplier)
    {
        if (instance == null)
            return;

        instance.transform.localScale = Vector3.Scale(instance.transform.localScale, scaleMultiplier);
    }
}
