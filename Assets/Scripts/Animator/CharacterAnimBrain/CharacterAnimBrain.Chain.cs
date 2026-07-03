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
        Cutscene = 4,
    }

    private Locomotion_Chain chain;
    private readonly PlaybackChannel _chainChannel = new();
    private float _activeChainAdvancePointNormalized = 1f;
    private bool _activeChainAdvanceRequested;
    private bool _activeChainAdvanceReleased;
    private bool _activeChainUsesRootMotion;
    private ChainPlaybackKind _activeChainKind;
    private bool _chainStateCanExit = true;
    private ClipTransition _activeChainCutsceneClip;
    private CutsceneDef _activeChainCutsceneDef;

    private ClipTransition ActiveChainClip => ResolveChainClip();
    private bool HasActiveChainClip => HasValidChainClip();
    internal int ActiveChainRequestId => _chainChannel.RequestId;
    internal bool CanExitChainState => _chainStateCanExit;
    internal bool ActiveChainUsesRootMotion => _activeChainUsesRootMotion;
    internal bool ActiveChainUsesPlanarRootMotion => _chainChannel.Request.UsesPlanarRootMotion;
    public bool RootMotionPlanarOnly { get; private set; }
    public bool RootMotionYawActive { get; private set; }

    public bool IsChainPlaybackActive =>
        _chainChannel.IsActive ||
        (_initialized && locomotionSM.CurrentState == chain);

    /// <summary>True when the active chain playback is a utility warp (in or out).</summary>
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
        return TryPlayChainSkill(
            requestId,
            skillDef,
            castPointNormalized,
            requestAdvanceMoment,
            advancePointNormalized,
            usePlanarRootMotion: false);
    }

    public bool TryPlayChainSkill(
        int requestId,
        SkillGemDefinition skillDef,
        float castPointNormalized,
        bool requestAdvanceMoment,
        float advancePointNormalized,
        bool usePlanarRootMotion)
    {
        if (skillDef == null)
            return false;

        return TryStartChainPlayback(
            requestId,
            ChainPlaybackKind.Skill,
            skillDef,
            castPointNormalized,
            requestAdvanceMoment,
            advancePointNormalized,
            usesRootMotion: usePlanarRootMotion || UseRootMotionForChainPlayback,
            usesPlanarRootMotion: usePlanarRootMotion);
    }

    public bool TryPlayChainUtilityWarpOut(int requestId)
    {
        return TryStartChainPlayback(
            requestId,
            ChainPlaybackKind.UtilityWarpOut,
            null,
            UtilityWarpOutCastPointNormalized,
            requestAdvanceMoment: false,
            advancePointNormalized: 1f,
            usesRootMotion: UseRootMotionForChainPlayback,
            usesPlanarRootMotion: false);
    }

    public bool TryPlayChainUtilityWarpIn(int requestId)
    {
        return TryStartChainPlayback(
            requestId,
            ChainPlaybackKind.UtilityWarpIn,
            null,
            UtilityWarpInCastPointNormalized,
            requestAdvanceMoment: false,
            advancePointNormalized: 1f,
            usesRootMotion: UseRootMotionForChainPlayback,
            usesPlanarRootMotion: false);
    }

    public bool TryPlayChainCutscene(int requestId, ClipTransition clip)
    {
        return TryPlayChainCutscene(requestId, clip, null);
    }

    public bool TryPlayChainCutscene(int requestId, CutsceneDef def)
    {
        return def != null
            ? TryPlayChainCutscene(requestId, def.characterCutsceneClip, def)
            : false;
    }

    private bool TryPlayChainCutscene(int requestId, ClipTransition clip, CutsceneDef def)
    {
        if (clip == null || !clip.IsValid) return false;
        if (requestId <= 0 || IsChainPlaybackActive) return false;

        _activeChainCutsceneClip = clip;
        _activeChainCutsceneDef = def;
        bool started = TryStartChainPlayback(
            requestId,
            ChainPlaybackKind.Cutscene,
            skillDef: null,
            castPointNormalized: 0f,
            requestAdvanceMoment: false,
            advancePointNormalized: 1f,
            usesRootMotion: UseRootMotionForChainPlayback,
            usesPlanarRootMotion: false);
        if (!started)
        {
            _activeChainCutsceneClip = null;
            _activeChainCutsceneDef = null;
        }

        return started;
    }

    public void CancelChainPlaybackRequest(int requestId)
    {
        if (requestId <= 0 || requestId != _chainChannel.Request.RequestId)
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
        if (state == null || _chainChannel.Request.RequestId <= 0)
        {
            return;
        }

        if (_chainChannel.Request.ReleaseRequested &&
            !_chainChannel.Request.Released &&
            state.NormalizedTime >= _chainChannel.Request.CastPointNormalized)
        {
            int requestId = _chainChannel.Request.RequestId;
            _chainChannel.Request.Released = true;
            EmitPlaybackSignal(ResolveActiveChainPlaybackKind(), PlaybackPhase.CastMoment, requestId);
            ChainCastMomentReached?.Invoke(requestId);

            if (requestId != _chainChannel.Request.RequestId)
                return;
        }

        if (_activeChainAdvanceRequested &&
            !_activeChainAdvanceReleased &&
            state.NormalizedTime >= _activeChainAdvancePointNormalized)
        {
            int requestId = _chainChannel.Request.RequestId;
            _activeChainAdvanceReleased = true;
            EmitPlaybackSignal(ResolveActiveChainPlaybackKind(), PlaybackPhase.AdvanceMoment, requestId);
            ChainAdvanceMomentReached?.Invoke(requestId);
        }
    }

    internal void NotifyChainPlaybackStateExited(bool completedNormally)
    {
        int requestId = _chainChannel.Request.RequestId;
        PlaybackKind playbackKind = ResolveActiveChainPlaybackKind();
        bool shouldReleaseOnComplete =
            completedNormally &&
            _chainChannel.Request.ReleaseRequested &&
            !_chainChannel.Request.Released &&
            requestId > 0;
        bool interrupted =
            !completedNormally &&
            _chainChannel.Request.ReleaseRequested &&
            requestId > 0;

        if (shouldReleaseOnComplete)
        {
            _chainChannel.Request.Released = true;
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
        float advancePointNormalized,
        bool usesRootMotion,
        bool usesPlanarRootMotion)
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
            advancePointNormalized,
            usesRootMotion,
            usesPlanarRootMotion);

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
        float advancePointNormalized,
        bool usesRootMotion,
        bool usesPlanarRootMotion)
    {
        _activeChainKind = kind;
        _chainChannel.Request.Definition = skillDef;
        _chainChannel.Request.RequestId = requestId;
        _chainChannel.Request.CastPointNormalized = Mathf.Clamp(castPointNormalized, 0f, 0.999f);
        _activeChainAdvanceRequested = requestAdvanceMoment;
        _activeChainAdvancePointNormalized = requestAdvanceMoment
            ? Mathf.Clamp(Mathf.Max(_chainChannel.Request.CastPointNormalized, advancePointNormalized), 0f, 0.999f)
            : 1f;
        _chainChannel.Request.ReleaseRequested = true;
        _chainChannel.Request.Released = false;
        _activeChainAdvanceReleased = false;
        _activeChainUsesRootMotion = usesRootMotion;
        _chainChannel.Request.UsesPlanarRootMotion = usesRootMotion && usesPlanarRootMotion;
        _chainChannel.Request.IgnoresCharacterCollisionDuringRootMotion =
            usesRootMotion &&
            kind == ChainPlaybackKind.Skill &&
            (skillDef == null || skillDef.IgnoreCharacterCollisionDuringRootMotion);
        _chainStateCanExit = false;
        SetActiveChainTimelineEventNames(kind == ChainPlaybackKind.Skill ? skillDef : null);
        if (kind == ChainPlaybackKind.Skill)
            EnsureSkillVfxPresenter(skillDef);
    }

    private void ClearActiveChainRequest()
    {
        _chainChannel.Clear();
        _activeChainKind = ChainPlaybackKind.None;
        _activeChainCutsceneClip = null;
        _activeChainCutsceneDef = null;
        _activeChainAdvancePointNormalized = 1f;
        _activeChainAdvanceRequested = false;
        _activeChainAdvanceReleased = false;
        _activeChainUsesRootMotion = false;
        _chainStateCanExit = true;
    }

    private void InterruptActiveChainRequest()
    {
        int requestId = _chainChannel.Request.RequestId;
        PlaybackKind playbackKind = ResolveActiveChainPlaybackKind();
        bool shouldNotify =
            (_chainChannel.Request.ReleaseRequested || _activeChainAdvanceRequested) &&
            requestId > 0;

        ClearActiveChainRequest();

        if (shouldNotify)
            NotifyInterrupted(playbackKind, requestId, true, playbackKind == PlaybackKind.ChainSkill);
    }

    internal bool TryGetAnimationSamplingRoot(out GameObject sampleRoot)
    {
        sampleRoot = null;

        if (!TryInitialize() || animancer == null || animancer.Animator == null)
            return false;

        sampleRoot = animancer.Animator.gameObject;
        return sampleRoot != null;
    }

    internal bool TryGetRootMotionSamplingAnimator(out Animator samplingAnimator)
    {
        samplingAnimator = null;

        if (!TryInitialize() || animancer == null || animancer.Animator == null)
            return false;

        samplingAnimator = animancer.Animator;
        return true;
    }

    internal void ApplyActiveChainRootMotionPolicy() => SetRootMotionPolicy(
        _chainChannel.Request.UsesPlanarRootMotion,
        _chainChannel.Request.IgnoresCharacterCollisionDuringRootMotion);

    internal void ClearActiveChainRootMotionPolicy()
    {
        ClearActiveRootMotionPolicy();
    }

    internal void ClearActiveRootMotionPolicy() => SetRootMotionPolicy(false, false);

    internal bool TryResolveChainSkillAnimationClip(SkillGemDefinition skillDef, out AnimationClip clip)
    {
        return TryResolveSkillAnimationClip(skillDef, out clip);
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
            ChainPlaybackKind.Skill => ResolveSkillClip(_chainChannel.Request.Definition),
            ChainPlaybackKind.UtilityWarpOut => UtilityWarpOutClip,
            ChainPlaybackKind.UtilityWarpIn => UtilityWarpInClip,
            ChainPlaybackKind.Cutscene => _activeChainCutsceneClip,
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
        _chainChannel.Request.TimelineEventNames.Clear();

        if (skillDef == null)
            return;

        skillDef.CollectTimelineEventNames(_chainChannel.Request.TimelineEventNames);
    }
}
