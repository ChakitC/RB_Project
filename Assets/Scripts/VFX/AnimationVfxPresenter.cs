using System;
using System.Collections.Generic;
using Animancer;
using UnityEngine;

public readonly struct AnimationVfxSessionToken : IEquatable<AnimationVfxSessionToken>
{
    readonly long _value;

    internal AnimationVfxSessionToken(long value)
    {
        _value = value;
    }

    internal bool IsValid => _value != 0;

    public bool Equals(AnimationVfxSessionToken other) => _value == other._value;
    public override bool Equals(object obj) => obj is AnimationVfxSessionToken other && Equals(other);
    public override int GetHashCode() => _value.GetHashCode();
    public static bool operator ==(AnimationVfxSessionToken left, AnimationVfxSessionToken right) => left.Equals(right);
    public static bool operator !=(AnimationVfxSessionToken left, AnimationVfxSessionToken right) => !left.Equals(right);
}

[DisallowMultipleComponent]
public sealed class AnimationVfxPresenter : MonoBehaviour
{
    sealed class Session
    {
        public readonly IAnimationVfxCueSource Source;
        public readonly AnimationVfxAnchorContext Anchors;
        public readonly Dictionary<string, List<GameObject>> ActiveLoops =
            new Dictionary<string, List<GameObject>>(StringComparer.Ordinal);

        public Session(IAnimationVfxCueSource source, AnimationVfxAnchorContext anchors)
        {
            Source = source;
            Anchors = anchors;
        }
    }

    readonly Dictionary<AnimationVfxSessionToken, Session> _sessions =
        new Dictionary<AnimationVfxSessionToken, Session>();

    long _nextTokenValue = 1;

    public AnimationVfxSessionToken BeginSession(
        IAnimationVfxCueSource source,
        AnimationVfxAnchorContext anchors)
    {
        if (source == null)
            return default;

        AnimationVfxSessionToken token = CreateToken();
        _sessions.Add(token, new Session(source, anchors.WithFallbackRoot(transform)));
        return token;
    }

    public void HandleCue(AnimationVfxSessionToken token, int cueIndex)
    {
        if (!token.IsValid || cueIndex < 0 || !_sessions.TryGetValue(token, out Session session))
            return;

        var replacedLoopKeys = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < session.Source.CueCount; i++)
        {
            IAnimationVfxCue cue = session.Source.GetCue(i);
            if (cue == null || cue.CueIndex != cueIndex || cue.Action != AnimationVfxAction.StartLoop ||
                cue.Prefab == null || string.IsNullOrWhiteSpace(cue.LoopKey))
            {
                continue;
            }

            string key = cue.LoopKey.Trim();
            if (replacedLoopKeys.Add(key))
                StopLoop(session, key, allowParticlesToFinish: false, extraLife: 0f);
        }

        for (int i = 0; i < session.Source.CueCount; i++)
        {
            IAnimationVfxCue cue = session.Source.GetCue(i);
            if (cue != null && cue.CueIndex == cueIndex)
                PlayCue(session, cue);
        }
    }

    public void EndSession(AnimationVfxSessionToken token)
    {
        if (!token.IsValid || !_sessions.TryGetValue(token, out Session session))
            return;

        _sessions.Remove(token);
        StopAllLoops(session);
    }

    public void EndAllSessions()
    {
        if (_sessions.Count == 0)
            return;

        var sessions = new List<Session>(_sessions.Values);
        _sessions.Clear();
        for (int i = 0; i < sessions.Count; i++)
            StopAllLoops(sessions[i]);
    }

    void OnDisable()
    {
        EndAllSessions();
    }

    AnimationVfxSessionToken CreateToken()
    {
        while (true)
        {
            if (_nextTokenValue <= 0)
                _nextTokenValue = 1;

            var token = new AnimationVfxSessionToken(_nextTokenValue++);
            if (!_sessions.ContainsKey(token))
                return token;
        }
    }

    void PlayCue(Session session, IAnimationVfxCue cue)
    {
        switch (cue.Action)
        {
            case AnimationVfxAction.StartLoop:
                StartLoop(session, cue);
                break;

            case AnimationVfxAction.StopLoop:
                StopLoop(session, cue.LoopKey, cue.AllowParticlesToFinish, cue.ExtraLife);
                break;

            case AnimationVfxAction.OneShot:
            default:
                SpawnOneShot(session, cue);
                break;
        }
    }

    void SpawnOneShot(Session session, IAnimationVfxCue cue)
    {
        if (cue.Prefab == null)
            return;

        VfxSpawner spawner = VfxSpawner.Instance;
        if (spawner == null)
            return;

        Transform anchor = AnimationVfxAnchorResolver.Resolve(session.Anchors, cue);
        AnimationVfxAnchorResolver.ResolvePose(anchor, cue, out Vector3 position, out Quaternion rotation);
        GameObject instance = spawner.SpawnVfx(cue.Prefab, position, rotation, cue.ExtraLife);
        if (instance == null)
            return;

        ApplyFollowMode(instance, anchor, cue);

        ApplyScale(instance, cue.LocalScale);
        PlayAnimClip(instance, cue.AnimClip);
    }

    void StartLoop(Session session, IAnimationVfxCue cue)
    {
        if (cue.Prefab == null || string.IsNullOrWhiteSpace(cue.LoopKey))
            return;

        VfxSpawner spawner = VfxSpawner.Instance;
        if (spawner == null)
            return;

        string key = cue.LoopKey.Trim();
        Transform anchor = AnimationVfxAnchorResolver.Resolve(session.Anchors, cue);
        AnimationVfxAnchorResolver.ResolvePose(anchor, cue, out Vector3 position, out Quaternion rotation);
        bool usesGenericBoneFollower = UsesGenericBoneFollower(cue);
        Transform parent = cue.AnchorMode == AnimationVfxAnchorMode.FollowAnchor && !usesGenericBoneFollower
            ? anchor
            : null;
        GameObject instance = spawner.SpawnLoopingVfx(cue.Prefab, position, rotation, parent);
        if (instance == null)
            return;

        if (usesGenericBoneFollower)
            ConfigureGenericBoneFollower(instance, anchor, cue);
        ApplyScale(instance, cue.LocalScale);
        PlayAnimClip(instance, cue.AnimClip);

        if (!session.ActiveLoops.TryGetValue(key, out List<GameObject> instances))
        {
            instances = new List<GameObject>();
            session.ActiveLoops.Add(key, instances);
        }

        instances.Add(instance);
    }

    void StopLoop(Session session, string loopKey, bool allowParticlesToFinish, float extraLife)
    {
        if (session == null || string.IsNullOrWhiteSpace(loopKey))
            return;

        string key = loopKey.Trim();
        if (!session.ActiveLoops.TryGetValue(key, out List<GameObject> instances))
            return;

        session.ActiveLoops.Remove(key);
        for (int i = 0; i < instances.Count; i++)
            StopLoopInstance(instances[i], allowParticlesToFinish, extraLife);
    }

    void StopAllLoops(Session session)
    {
        if (session == null || session.ActiveLoops.Count == 0)
            return;

        var groups = new List<List<GameObject>>(session.ActiveLoops.Values);
        session.ActiveLoops.Clear();
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

        VfxSpawner spawner = VfxSpawner.Instance;
        if (spawner != null)
            spawner.StopLoopingVfx(instance, allowParticlesToFinish, extraLife);
        else
            Destroy(instance);
    }

    static void ApplyScale(GameObject instance, Vector3 scaleMultiplier)
    {
        if (instance != null)
            instance.transform.localScale = Vector3.Scale(instance.transform.localScale, scaleMultiplier);
    }

    static void ApplyFollowMode(GameObject instance, Transform anchor, IAnimationVfxCue cue)
    {
        if (instance == null || anchor == null || cue == null ||
            cue.AnchorMode != AnimationVfxAnchorMode.FollowAnchor)
            return;

        if (UsesGenericBoneFollower(cue))
            ConfigureGenericBoneFollower(instance, anchor, cue);
        else
            instance.transform.SetParent(anchor, true);
    }

    static bool UsesGenericBoneFollower(IAnimationVfxCue cue)
    {
        return cue != null &&
               cue.Anchor == AnimationVfxAnchor.GenericBone &&
               cue.AnchorMode == AnimationVfxAnchorMode.FollowAnchor;
    }

    static void ConfigureGenericBoneFollower(GameObject instance, Transform anchor, IAnimationVfxCue cue)
    {
        if (instance == null || anchor == null || cue == null)
            return;

        AnimationVfxFollowAnchor follower = instance.GetComponent<AnimationVfxFollowAnchor>();
        if (follower == null)
            follower = instance.AddComponent<AnimationVfxFollowAnchor>();
        follower.Configure(anchor, cue.LocalPosition, Quaternion.Euler(cue.LocalEulerAngles));
    }

    static void PlayAnimClip(GameObject instance, AnimationClip clip)
    {
        if (instance == null || clip == null)
            return;
        Animator animator = instance.GetComponentInChildren<Animator>(true);
        if (animator == null)
            return;
        AnimancerComponent animancer = animator.GetComponent<AnimancerComponent>();
        if (animancer == null)
            animancer = animator.gameObject.AddComponent<AnimancerComponent>();
        animancer.Play(clip);
    }
}
