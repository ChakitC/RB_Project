using System.Collections.Generic;

public sealed partial class CharacterAnimBrain
{
    /// <summary>
    /// Playback signal dispatch and the locomotion state-machine transition scope that owns it.
    ///
    /// Animancer's <see cref="Animancer.FSM.StateMachine{TState}.ForceSetState"/> calls
    /// <c>OnExitState</c> <em>before</em> it reassigns <c>CurrentState</c>, so a signal raised from
    /// inside <c>OnExitState</c> is observed by handlers while the state machine still reports the
    /// state they were just told had finished. Handlers that react by starting the next playback
    /// were rejected by the <c>IsChainPlaybackActive</c> / <c>IsSkillPlaybackActive</c> guards and
    /// had to defer themselves by a frame.
    ///
    /// Every signal raised while a transition is in flight is therefore queued and flushed once the
    /// outermost transition has settled. Queue order is preserved, so the observable event order is
    /// unchanged; only the moment of delivery moves to after the state machine is consistent.
    /// </summary>
    private readonly struct PendingPlaybackSignal
    {
        public readonly PlaybackKind Kind;
        public readonly PlaybackPhase Phase;
        public readonly int RequestId;

        public PendingPlaybackSignal(PlaybackKind kind, PlaybackPhase phase, int requestId)
        {
            Kind = kind;
            Phase = phase;
            RequestId = requestId;
        }
    }

    private readonly List<PendingPlaybackSignal> _pendingPlaybackSignals = new();
    private int _locomotionTransitionDepth;
    private bool _flushingPlaybackSignals;

    // ----- Locomotion transition scope -----

    private bool TrySetLocomotionState(LocomotionState state)
    {
        _locomotionTransitionDepth++;
        try
        {
            return locomotionSM.TrySetState(state);
        }
        finally
        {
            EndLocomotionTransition();
        }
    }

    private bool TryResetLocomotionState(LocomotionState state)
    {
        _locomotionTransitionDepth++;
        try
        {
            return locomotionSM.TryResetState(state);
        }
        finally
        {
            EndLocomotionTransition();
        }
    }

    private void ForceSetLocomotionState(LocomotionState state)
    {
        _locomotionTransitionDepth++;
        try
        {
            locomotionSM.ForceSetState(state);
        }
        finally
        {
            EndLocomotionTransition();
        }
    }

    private void EndLocomotionTransition()
    {
        _locomotionTransitionDepth--;
        if (_locomotionTransitionDepth > 0)
            return;

        _locomotionTransitionDepth = 0;
        FlushPendingPlaybackSignals();
    }

    // ----- Signal dispatch -----

    private void EmitPlaybackSignal(PlaybackKind kind, PlaybackPhase phase, int requestId)
    {
        if (kind == PlaybackKind.None)
            return;

        if (_locomotionTransitionDepth > 0)
        {
            _pendingPlaybackSignals.Add(new PendingPlaybackSignal(kind, phase, requestId));
            return;
        }

        RaisePlaybackSignal(kind, phase, requestId);
    }

    private void FlushPendingPlaybackSignals()
    {
        if (_flushingPlaybackSignals || _pendingPlaybackSignals.Count == 0)
            return;

        _flushingPlaybackSignals = true;
        try
        {
            // Handlers are allowed to start the next playback, which appends to this list while it
            // is being drained. Index over the growing list so those signals still arrive in order.
            for (int i = 0; i < _pendingPlaybackSignals.Count; i++)
            {
                PendingPlaybackSignal signal = _pendingPlaybackSignals[i];
                RaisePlaybackSignal(signal.Kind, signal.Phase, signal.RequestId);
            }
        }
        finally
        {
            _pendingPlaybackSignals.Clear();
            _flushingPlaybackSignals = false;
        }
    }

    /// <summary>
    /// Single fan-out point from the canonical <see cref="PlaybackEvent"/> stream to the legacy
    /// per-subsystem events. Adding a phase or kind only needs a change here.
    /// </summary>
    private void RaisePlaybackSignal(PlaybackKind kind, PlaybackPhase phase, int requestId)
    {
        PlaybackEvent?.Invoke(new PlaybackSignal(kind, phase, requestId));

        bool isChain = IsChainPlaybackKind(kind);
        bool isSkillOrUtility = kind == PlaybackKind.Skill || kind == PlaybackKind.UtilityWarpOut;

        switch (phase)
        {
            case PlaybackPhase.CastMoment:
                if (isChain)
                    ChainCastMomentReached?.Invoke(requestId);
                else if (isSkillOrUtility)
                    SkillCastMomentReached?.Invoke(requestId);
                break;

            case PlaybackPhase.AdvanceMoment:
                if (isChain)
                    ChainAdvanceMomentReached?.Invoke(requestId);
                break;

            case PlaybackPhase.Completed:
                if (isChain)
                    ChainPlaybackCompleted?.Invoke(requestId);
                else if (isSkillOrUtility)
                    SkillCompleted?.Invoke();
                break;

            case PlaybackPhase.Interrupted:
                if (isChain)
                {
                    ChainPlaybackInterrupted?.Invoke(requestId);

                    // A chain skill still owns a skill request, so skill runtimes must unwind too.
                    if (kind == PlaybackKind.ChainSkill)
                        SkillCastInterrupted?.Invoke(requestId);
                }
                else if (isSkillOrUtility)
                {
                    SkillCastInterrupted?.Invoke(requestId);
                }

                break;
        }
    }

    private static bool IsChainPlaybackKind(PlaybackKind kind)
    {
        return kind == PlaybackKind.ChainSkill ||
               kind == PlaybackKind.ChainUtilityWarpOut ||
               kind == PlaybackKind.ChainUtilityWarpIn ||
               kind == PlaybackKind.ChainCutscene;
    }
}
