using System;
using System.Collections.Generic;
using Animancer;
using UnityEngine;

internal sealed class AnimationTimelineBinding
{
    private readonly Component host;
    private readonly PlaybackSessionCoordinator sessions;
    private CharacteContext ctx;
    private Animator animator;
    private AnimationVfxPresenter _animationVfxPresenter;
    private SkillVfxPresenter skillVfxCompatibility;
    private readonly Dictionary<PlaybackChannel, (PlaybackSessionToken playback, AnimationVfxSessionToken vfx)> skillVfx = new();
    private readonly Dictionary<AnimancerState, ulong> leases = new();
    private ulong nextLease;
    private readonly List<AnimationVfxSessionToken> vfxSessions = new();
    internal int CallbackCount => leases.Count;
    internal int VfxSessionCount => vfxSessions.Count;
    internal AnimationTimelineBinding(Component host, PlaybackSessionCoordinator sessions)
    { this.host = host; this.sessions = sessions; }
    internal void Bind(CharacterAnimationBindingSnapshot binding)
    { ctx = binding.Context; animator = binding.Animator; }
    internal Action Guard(PlaybackChannel channel, Action callback)
    {
        var token = sessions.Capture(channel);
        return () => { if (sessions.IsCurrent(token)) SafeAnimationEvents.Invoke(callback); };
    }
    internal Action<T> Guard<T>(PlaybackChannel channel, Action<T> callback)
    {
        var token = sessions.Capture(channel);
        return value => { if (sessions.IsCurrent(token)) SafeAnimationEvents.Invoke(callback, value); };
    }
    internal ulong BindEnd(AnimancerState state, AnimancerEvent.Sequence events,
        PlaybackChannel channel, Action onEnd, bool invokeAuthored = true)
    {
        if (state == null) return 0;
        ulong lease = ++nextLease;
        leases[state] = lease;
        var token = channel != null ? sessions.Capture(channel) : default;
        int binding = sessions.BindingGeneration;
        Action authored = invokeAuthored ? events.OnEnd : null;
        bool Current() => binding == sessions.BindingGeneration && leases.TryGetValue(state, out ulong current) &&
            current == lease && (channel == null || sessions.IsCurrent(token));
        events.OnEnd = () =>
        {
            if (!Current()) return;
            SafeAnimationEvents.Invoke(authored);
            if (Current()) SafeAnimationEvents.Invoke(onEnd);
        };
        state.SharedEvents = events;
        return lease;
    }
    internal bool IsCurrent(AnimancerState state, ulong lease) => state != null && lease != 0 &&
        leases.TryGetValue(state, out ulong current) && current == lease;
    internal void Release(AnimancerState state, ulong lease)
    {
        if (IsCurrent(state, lease)) Release(state);
    }
    internal void Release(AnimancerState state)
    {
        if (state == null) return;
        leases.Remove(state);
        if (UnityEngine.Playables.PlayableExtensions.IsValid(state.Playable)) state.SharedEvents = null;
    }
    internal void Clear()
    {
        foreach (var pair in leases)
            if (UnityEngine.Playables.PlayableExtensions.IsValid(pair.Key.Playable)) pair.Key.SharedEvents = null;
        leases.Clear();
        foreach (var token in vfxSessions) _animationVfxPresenter?.EndSession(token);
        vfxSessions.Clear();
        skillVfx.Clear();
    }
    internal AnimationVfxSessionToken BeginAnimationVfxSession(IAnimationVfxCueSource source)
    {
        if (source == null)
            return default;

        if (_animationVfxPresenter == null)
            _animationVfxPresenter = host.GetComponent<AnimationVfxPresenter>();
        if (_animationVfxPresenter == null)
            _animationVfxPresenter = host.gameObject.AddComponent<AnimationVfxPresenter>();

        Transform root = ctx != null ? ctx.transform : host.transform;
        SkillUserSystem skillUser = ctx != null ? ctx.EnegySystem : null;
        var token = _animationVfxPresenter.BeginSession(
            source,
            new AnimationVfxAnchorContext(
                root,
                skillUser != null ? skillUser.CastOrigin : root,
                skillUser != null ? skillUser.AimTransform : root,
                animator));
        vfxSessions.Add(token);
        return token;
    }

    internal void HandleAnimationVfxCue(AnimationVfxSessionToken token, int cueIndex)
    {
        _animationVfxPresenter?.HandleCue(token, cueIndex);
    }

    internal void EndAnimationVfxSession(AnimationVfxSessionToken token)
    {
        vfxSessions.Remove(token);
        _animationVfxPresenter?.EndSession(token);
    }

    internal void EnsureSkillVfxPresenter(SkillGemDefinition skillDef)
    {
        if (skillDef == null || !skillDef.HasSkillVfxEvents)
            return;
        if (skillVfxCompatibility == null) skillVfxCompatibility = host.GetComponent<SkillVfxPresenter>();
        if (skillVfxCompatibility == null) skillVfxCompatibility = host.gameObject.AddComponent<SkillVfxPresenter>();
    }

    internal void HandleSkillVfxCue(PlaybackChannel channel, int cueIndex)
    {
        var definition = channel.Request.Definition;
        if (!channel.IsActive || definition == null || !definition.HasSkillVfxEvents) return;
        EnsureSkillVfxPresenter(definition);
        if (!skillVfxCompatibility.isActiveAndEnabled) return;
        if (skillVfx.TryGetValue(channel, out var lease) && !sessions.IsCurrent(lease.playback))
            EndSkillVfxSession(channel);
        if (!skillVfx.TryGetValue(channel, out lease))
        {
            if (_animationVfxPresenter == null) _animationVfxPresenter = host.GetComponent<AnimationVfxPresenter>();
            if (_animationVfxPresenter == null) _animationVfxPresenter = host.gameObject.AddComponent<AnimationVfxPresenter>();
            var vfx = _animationVfxPresenter.BeginSession(new SkillVfxCueSource(definition.SkillVfxEvents),
                SkillVfxAnchorResolver.CreateContext(host.transform));
            vfxSessions.Add(vfx);
            lease = (sessions.Capture(channel), vfx);
            skillVfx[channel] = lease;
        }
        HandleAnimationVfxCue(lease.vfx, cueIndex);
    }

    internal void EndSkillVfxSession(PlaybackChannel channel)
    {
        if (!skillVfx.TryGetValue(channel, out var lease)) return;
        skillVfx.Remove(channel);
        EndAnimationVfxSession(lease.vfx);
    }

    internal void EndSkillVfxSessions(int requestId = 0)
    {
        EndMatching(sessions.Skill);
        EndMatching(sessions.Chain);
        void EndMatching(PlaybackChannel channel)
        {
            if (skillVfx.TryGetValue(channel, out var lease) &&
                (requestId <= 0 || lease.playback.RequestId == requestId)) EndSkillVfxSession(channel);
        }
    }

    internal void BindTimelineEventCallbacks(
        AnimancerEvent.Sequence runtimeEvents,
        IReadOnlyList<CombatTimelineEventName> eventNames,
        ClipTransition clip,
        Action<CombatTimelineEventName> raiseTimelineEvent,
        bool warnMissing)
    {
        if (runtimeEvents == null || eventNames == null || eventNames.Count == 0 || raiseTimelineEvent == null)
            return;

        string clipName = clip != null && clip.Clip != null ? clip.Clip.name : "<none>";

        for (int i = 0; i < eventNames.Count; i++)
        {
            CombatTimelineEventName eventName = eventNames[i];
            if (!CombatTimelineEventNames.IsValid(eventName) || eventName == CombatTimelineEventName.Vfx)
                continue;

            BindTimelineEventCallback(runtimeEvents, eventName, clipName, raiseTimelineEvent, warnMissing);
        }
    }

    internal void BindVfxTimelineEventCallbacks(
        AnimancerEvent.Sequence runtimeEvents,
        SkillGemDefinition skillDef,
        ClipTransition clip,
        Action<int> raiseVfxCue)
    {
        if (runtimeEvents == null || skillDef == null || !skillDef.HasSkillVfxEvents || raiseVfxCue == null)
            return;

        int boundCueCount = AnimationVfxEventBinder.Bind(runtimeEvents, raiseVfxCue);
        if (boundCueCount == 0)
        {
            string clipName = clip != null && clip.Clip != null ? clip.Clip.name : "<none>";
            Debug.LogWarning(
                $"[CharacterAnimBrain] Skill clip '{clipName}' has VFX data but no 'Vfx' timeline event.",
                host);
        }
    }

    internal void BindTimelineEventCallback(
        AnimancerEvent.Sequence runtimeEvents,
        CombatTimelineEventName eventName,
        string clipName,
        Action<CombatTimelineEventName> raiseTimelineEvent,
        bool warnMissing)
    {
        if (runtimeEvents == null ||
            !CombatTimelineEventNames.IsValid(eventName) ||
            raiseTimelineEvent == null)
        {
            return;
        }

        CombatTimelineEventName capturedEventName = eventName;
        StringReference animancerEventName = CombatTimelineEventNames.ToStringReference(capturedEventName);
        if (animancerEventName == null || string.IsNullOrWhiteSpace(animancerEventName.String))
            return;

        int count = runtimeEvents.SetCallbacks(
            animancerEventName,
            () => raiseTimelineEvent(capturedEventName));

        if (count == 0 && warnMissing)
        {
            Debug.LogWarning(
                $"[CharacterAnimBrain] Skill clip '{clipName}' is missing timeline event '{capturedEventName}' ({animancerEventName}).",
                host);
        }
    }
}
