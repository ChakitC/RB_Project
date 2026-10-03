using System;
using Animancer;
using UnityEngine;

// One slot owns callbacks/VFX, not the request or the outgoing visual pose.
internal sealed class AnimationPlayback
{
    private readonly Func<AnimationPlaybackOptions, AnimancerState> play;
    private readonly AnimationTimelineBinding timeline;
    private readonly PlaybackSessionCoordinator sessions;
    private AnimationVfxSessionToken vfx;
    private ulong generation, lease;
    internal AnimancerState State { get; private set; }

    internal AnimationPlayback(Func<AnimationPlaybackOptions, AnimancerState> play,
        AnimationTimelineBinding timeline, PlaybackSessionCoordinator sessions)
    { this.play = play; this.timeline = timeline; this.sessions = sessions; }

    internal AnimancerState Play(AnimationPlaybackOptions options,
        Action<AnimancerEvent.Sequence> configureEvents = null, Action onEnd = null)
    {
        Stop();
        if (options.Transition == null || !options.Transition.IsValid) return null;
        ulong playback = generation;
        int binding = sessions.BindingGeneration;
        var token = options.Channel != null ? sessions.Capture(options.Channel) : default;
        var state = play(options);
        State = state;
        if (state == null) return null;
        if (options.NormalizedStart.HasValue) state.NormalizedTime = options.NormalizedStart.Value;
        if (options.Duration.HasValue)
            state.Speed = Mathf.Max(.01f, state.Length) / Mathf.Max(.01f, options.Duration.Value);
        if (options.Speed.HasValue) state.Speed = options.Speed.Value;
        var events = new AnimancerEvent.Sequence(options.Transition.Events);
        bool Current() => generation == playback && sessions.BindingGeneration == binding &&
            ReferenceEquals(State, state) && timeline.IsCurrent(state, lease) &&
            (options.Channel == null || sessions.IsCurrent(token));
        try
        {
            configureEvents?.Invoke(events);
            // A binding callback can synchronously replace this slot.
            if (generation != playback || sessions.BindingGeneration != binding ||
                (options.Channel != null && !sessions.IsCurrent(token))) return null;
            if (options.Vfx != null && options.Vfx.CueCount > 0)
            {
                var cueToken = timeline.BeginAnimationVfxSession(options.Vfx);
                vfx = cueToken;
                AnimationVfxEventBinder.Bind(events, index => timeline.HandleAnimationVfxCue(cueToken, index));
            }
            for (int i = 0; i < events.Count; i++)
            {
                var callback = events[i].callback;
                events.SetCallback(i, () => InvokeCurrent(callback));
            }
            Action authored = options.InvokeAuthoredEnd ? events.OnEnd : null;
            bool ended = false;
            lease = timeline.BindEnd(state, events, options.Channel, () =>
            {
                if (ended || !Current()) return;
                ended = true;
                InvokeCurrent(authored);
                if (!Current()) return;
                if (options.HoldLastFrame) { state.Time = state.Length; state.Speed = 0f; }
                InvokeCurrent(onEnd);
            }, false);
            return state;
        }
        catch
        {
            if (generation == playback) Stop();
            throw;
        }

        void InvokeCurrent(Action callback)
        {
            if (callback == null) return;
            foreach (Action subscriber in callback.GetInvocationList())
            {
                if (!Current()) return;
                SafeAnimationEvents.Invoke(subscriber);
            }
        }
    }

    internal void EndVfx()
    {
        var previous = vfx; vfx = default;
        timeline.EndAnimationVfxSession(previous);
    }

    internal void Stop()
    {
        generation++;
        var previous = State; State = null;
        ulong previousLease = lease; lease = 0;
        timeline.Release(previous, previousLease);
        EndVfx();
    }
}
