using System;
using System.Collections.Generic;
using Animancer;
using UnityEngine;

public sealed partial class CharacterAnimBrain
{
    private enum ChainPlaybackKind
    {
        None = 0,
        Skill = 1,
        UtilityWarpOut = 2,
        UtilityWarpIn = 3,
    }

    private Locomotion_Chain chain;
    private SkillGemDefinition _activeChainSkillDefinition;
    private int _activeChainRequestId;
    private float _activeChainCastPointNormalized = 0.35f;
    private float _activeChainAdvancePointNormalized = 1f;
    private bool _activeChainReleaseRequested;
    private bool _activeChainReleased;
    private bool _activeChainAdvanceRequested;
    private bool _activeChainAdvanceReleased;
    private ChainPlaybackKind _activeChainKind;
    private bool _chainStateCanExit = true;
    private readonly List<CombatTimelineEventName> _activeChainTimelineEventNames = new List<CombatTimelineEventName>();

    private ClipTransition ActiveChainClip => ResolveChainClip();
    private bool HasActiveChainClip => HasValidChainClip();
    internal int ActiveChainRequestId => _activeChainRequestId;
    internal bool CanExitChainState => _chainStateCanExit;

    public bool IsChainPlaybackActive =>
        _activeChainReleaseRequested ||
        _activeChainRequestId != 0 ||
        (_initialized && locomotionSM.CurrentState == chain);

    public bool IsChainUtilityPlaybackActive =>
        (_activeChainKind == ChainPlaybackKind.UtilityWarpOut ||
         _activeChainKind == ChainPlaybackKind.UtilityWarpIn) &&
        IsChainPlaybackActive;

    public event Action<int> ChainCastMomentReached;
    public event Action<int> ChainAdvanceMomentReached;
    public event Action<int> ChainPlaybackInterrupted;
    public event Action<int> ChainPlaybackCompleted;

    public bool TryPlayChainSkill(
        int requestId,
        SkillGemDefinition skillDef,
        float castPointNormalized,
        bool requestAdvanceMoment,
        float advancePointNormalized)
    {
        if (skillDef == null)
            return false;

        return TryStartChainPlayback(
            requestId,
            ChainPlaybackKind.Skill,
            skillDef,
            castPointNormalized,
            requestAdvanceMoment,
            advancePointNormalized);
    }

    public bool TryPlayChainUtilityWarpOut(int requestId)
    {
        return TryStartChainPlayback(
            requestId,
            ChainPlaybackKind.UtilityWarpOut,
            null,
            UtilityWarpOutCastPointNormalized,
            requestAdvanceMoment: false,
            advancePointNormalized: 1f);
    }

    public bool TryPlayChainUtilityWarpIn(int requestId)
    {
        return TryStartChainPlayback(
            requestId,
            ChainPlaybackKind.UtilityWarpIn,
            null,
            UtilityWarpInCastPointNormalized,
            requestAdvanceMoment: false,
            advancePointNormalized: 1f);
    }

    public void CancelChainPlaybackRequest(int requestId)
    {
        if (requestId <= 0 || requestId != _activeChainRequestId)
            return;

        if (!TryInitialize())
        {
            InterruptActiveChainRequest();
            return;
        }

        AllowChainStateExit();

        if (locomotionSM.CurrentState == chain)
            locomotionSM.TrySetState(IsDowned ? crawlState : locomotion);
        else
            InterruptActiveChainRequest();
    }

    internal void PollActiveChainPlayback(AnimancerState state)
    {
        if (state == null || _activeChainRequestId <= 0)
        {
            return;
        }

        if (_activeChainReleaseRequested &&
            !_activeChainReleased &&
            state.NormalizedTime >= _activeChainCastPointNormalized)
        {
            int requestId = _activeChainRequestId;
            _activeChainReleased = true;
            EmitPlaybackSignal(ResolveActiveChainPlaybackKind(), PlaybackPhase.CastMoment, requestId);
            ChainCastMomentReached?.Invoke(requestId);

            if (requestId != _activeChainRequestId)
                return;
        }

        if (_activeChainAdvanceRequested &&
            !_activeChainAdvanceReleased &&
            state.NormalizedTime >= _activeChainAdvancePointNormalized)
        {
            int requestId = _activeChainRequestId;
            _activeChainAdvanceReleased = true;
            EmitPlaybackSignal(ResolveActiveChainPlaybackKind(), PlaybackPhase.AdvanceMoment, requestId);
            ChainAdvanceMomentReached?.Invoke(requestId);
        }
    }

    internal void NotifyChainPlaybackStateExited(bool completedNormally)
    {
        int requestId = _activeChainRequestId;
        PlaybackKind playbackKind = ResolveActiveChainPlaybackKind();
        bool shouldReleaseOnComplete =
            completedNormally &&
            _activeChainReleaseRequested &&
            !_activeChainReleased &&
            requestId > 0;
        bool interrupted =
            !completedNormally &&
            _activeChainReleaseRequested &&
            requestId > 0;

        if (shouldReleaseOnComplete)
        {
            _activeChainReleased = true;
            EmitPlaybackSignal(playbackKind, PlaybackPhase.CastMoment, requestId);
            ChainCastMomentReached?.Invoke(requestId);
        }

        bool shouldAdvanceOnComplete =
            completedNormally &&
            _activeChainAdvanceRequested &&
            !_activeChainAdvanceReleased &&
            requestId > 0;

        if (shouldAdvanceOnComplete)
        {
            _activeChainAdvanceReleased = true;
            EmitPlaybackSignal(playbackKind, PlaybackPhase.AdvanceMoment, requestId);
            ChainAdvanceMomentReached?.Invoke(requestId);
        }

        ClearActiveChainRequest();

        if (completedNormally && requestId > 0)
        {
            EmitPlaybackSignal(playbackKind, PlaybackPhase.Completed, requestId);
            ChainPlaybackCompleted?.Invoke(requestId);
        }

        if (interrupted)
        {
            EmitPlaybackSignal(playbackKind, PlaybackPhase.Interrupted, requestId);
            ChainPlaybackInterrupted?.Invoke(requestId);

            if (playbackKind == PlaybackKind.ChainSkill)
                SkillCastInterrupted?.Invoke(requestId);
        }
    }

    internal void CompleteActiveChainPlayback()
    {
        AllowChainStateExit();

        if (locomotionSM.CurrentState == chain)
            locomotionSM.TrySetState(IsDowned ? crawlState : locomotion);
    }

    internal void AllowChainStateExit()
    {
        _chainStateCanExit = true;
    }

    internal void AbortActiveChainPlaybackForExternalState()
    {
        if (!IsChainPlaybackActive)
            return;

        AllowChainStateExit();
    }

    private bool TryStartChainPlayback(
        int requestId,
        ChainPlaybackKind kind,
        SkillGemDefinition skillDef,
        float castPointNormalized,
        bool requestAdvanceMoment,
        float advancePointNormalized)
    {
        if (requestId <= 0 || IsChainPlaybackActive)
            return false;

        if (!TryInitialize())
            return false;

        InterruptActiveSkillRequest();
        InterruptActiveUtilityRequest();

        ArmChainRequest(
            requestId,
            kind,
            skillDef,
            castPointNormalized,
            requestAdvanceMoment,
            advancePointNormalized);

        try
        {
            if (locomotionSM.TryResetState(chain))
                return true;
        }
        catch (ArgumentException ex)
        {
            Debug.LogWarning($"[CharacterAnimBrain] Invalid chain clip. {ex.Message}", this);
        }

        ClearActiveChainRequest();
        return false;
    }

    private void ArmChainRequest(
        int requestId,
        ChainPlaybackKind kind,
        SkillGemDefinition skillDef,
        float castPointNormalized,
        bool requestAdvanceMoment,
        float advancePointNormalized)
    {
        _activeChainKind = kind;
        _activeChainSkillDefinition = skillDef;
        _activeChainRequestId = requestId;
        _activeChainCastPointNormalized = Mathf.Clamp(castPointNormalized, 0f, 0.999f);
        _activeChainAdvanceRequested = requestAdvanceMoment;
        _activeChainAdvancePointNormalized = requestAdvanceMoment
            ? Mathf.Clamp(Mathf.Max(_activeChainCastPointNormalized, advancePointNormalized), 0f, 0.999f)
            : 1f;
        _activeChainReleaseRequested = true;
        _activeChainReleased = false;
        _activeChainAdvanceReleased = false;
        _chainStateCanExit = false;
        SetActiveChainTimelineEventNames(kind == ChainPlaybackKind.Skill ? skillDef : null);
        if (kind == ChainPlaybackKind.Skill)
            EnsureSkillVfxPresenter(skillDef);
    }

    private void ClearActiveChainRequest()
    {
        _activeChainKind = ChainPlaybackKind.None;
        _activeChainSkillDefinition = null;
        _activeChainRequestId = 0;
        _activeChainCastPointNormalized = 0.35f;
        _activeChainAdvancePointNormalized = 1f;
        _activeChainReleaseRequested = false;
        _activeChainReleased = false;
        _activeChainAdvanceRequested = false;
        _activeChainAdvanceReleased = false;
        _chainStateCanExit = true;
        _activeChainTimelineEventNames.Clear();
    }

    private void InterruptActiveChainRequest()
    {
        int requestId = _activeChainRequestId;
        PlaybackKind playbackKind = ResolveActiveChainPlaybackKind();
        bool shouldNotify =
            (_activeChainReleaseRequested || _activeChainAdvanceRequested) &&
            requestId > 0;

        ClearActiveChainRequest();

        if (shouldNotify)
        {
            EmitPlaybackSignal(playbackKind, PlaybackPhase.Interrupted, requestId);
            ChainPlaybackInterrupted?.Invoke(requestId);

            if (playbackKind == PlaybackKind.ChainSkill)
                SkillCastInterrupted?.Invoke(requestId);
        }
    }

    internal bool TryGetAnimationSamplingRoot(out GameObject sampleRoot)
    {
        sampleRoot = null;

        if (!TryInitialize() || animancer == null || animancer.Animator == null)
            return false;

        sampleRoot = animancer.Animator.gameObject;
        return sampleRoot != null;
    }

    internal bool TryResolveChainSkillAnimationClip(SkillGemDefinition skillDef, out AnimationClip clip)
    {
        clip = null;

        if (skillDef == null || !TryInitialize())
            return false;

        return TryExtractAnimationClip(ResolveSkillClip(skillDef), out clip);
    }

    internal bool TryResolveChainUtilityWarpOutAnimationClip(
        out AnimationClip clip,
        out float castPointNormalized)
    {
        clip = null;
        castPointNormalized = 0f;

        if (!TryInitialize())
            return false;

        castPointNormalized = UtilityWarpOutCastPointNormalized;
        return TryExtractAnimationClip(UtilityWarpOutClip, out clip);
    }

    internal bool TryResolveChainUtilityWarpInAnimationClip(
        out AnimationClip clip,
        out float castPointNormalized)
    {
        clip = null;
        castPointNormalized = 0f;

        if (!TryInitialize())
            return false;

        castPointNormalized = UtilityWarpInCastPointNormalized;
        return TryExtractAnimationClip(UtilityWarpInClip, out clip);
    }

    static bool TryExtractAnimationClip(ClipTransition transition, out AnimationClip clip)
    {
        clip = transition != null ? transition.Clip : null;
        return transition != null && transition.IsValid && clip != null;
    }

    private ClipTransition ResolveChainClip()
    {
        return _activeChainKind switch
        {
            ChainPlaybackKind.Skill => ResolveSkillClip(_activeChainSkillDefinition),
            ChainPlaybackKind.UtilityWarpOut => UtilityWarpOutClip,
            ChainPlaybackKind.UtilityWarpIn => UtilityWarpInClip,
            _ => null,
        };
    }

    private bool HasValidChainClip()
    {
        ClipTransition clip = ResolveChainClip();
        return clip != null && clip.IsValid;
    }

    private void SetActiveChainTimelineEventNames(SkillGemDefinition skillDef)
    {
        _activeChainTimelineEventNames.Clear();

        if (skillDef == null)
            return;

        skillDef.CollectTimelineEventNames(_activeChainTimelineEventNames);
    }
}
