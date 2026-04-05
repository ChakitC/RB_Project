using System;
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
    private bool _activeChainReleaseRequested;
    private bool _activeChainReleased;
    private ChainPlaybackKind _activeChainKind;
    private bool _chainStateCanExit = true;

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
    public event Action<int> ChainPlaybackInterrupted;
    public event Action<int> ChainPlaybackCompleted;

    public bool TryPlayChainSkill(int requestId, SkillGemDefinition skillDef, float castPointNormalized)
    {
        if (skillDef == null)
            return false;

        return TryStartChainPlayback(
            requestId,
            ChainPlaybackKind.Skill,
            skillDef,
            castPointNormalized);
    }

    public bool TryPlayChainUtilityWarpOut(int requestId)
    {
        return TryStartChainPlayback(
            requestId,
            ChainPlaybackKind.UtilityWarpOut,
            null,
            UtilityWarpOutCastPointNormalized);
    }

    public bool TryPlayChainUtilityWarpIn(int requestId)
    {
        return TryStartChainPlayback(
            requestId,
            ChainPlaybackKind.UtilityWarpIn,
            null,
            UtilityWarpInCastPointNormalized);
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
        if (state == null ||
            !_activeChainReleaseRequested ||
            _activeChainReleased ||
            _activeChainRequestId <= 0)
        {
            return;
        }

        if (state.NormalizedTime < _activeChainCastPointNormalized)
            return;

        _activeChainReleased = true;
        ChainCastMomentReached?.Invoke(_activeChainRequestId);
    }

    internal void NotifyChainPlaybackStateExited(bool completedNormally)
    {
        int requestId = _activeChainRequestId;
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
            ChainCastMomentReached?.Invoke(requestId);
        }

        ClearActiveChainRequest();

        if (completedNormally && requestId > 0)
            ChainPlaybackCompleted?.Invoke(requestId);

        if (interrupted)
            ChainPlaybackInterrupted?.Invoke(requestId);
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
        float castPointNormalized)
    {
        if (requestId <= 0 || IsChainPlaybackActive)
            return false;

        if (!TryInitialize())
            return false;

        InterruptActiveSkillRequest();
        InterruptActiveUtilityRequest();

        ArmChainRequest(requestId, kind, skillDef, castPointNormalized);

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
        float castPointNormalized)
    {
        _activeChainKind = kind;
        _activeChainSkillDefinition = skillDef;
        _activeChainRequestId = requestId;
        _activeChainCastPointNormalized = Mathf.Clamp(castPointNormalized, 0f, 0.999f);
        _activeChainReleaseRequested = true;
        _activeChainReleased = false;
        _chainStateCanExit = false;
    }

    private void ClearActiveChainRequest()
    {
        _activeChainKind = ChainPlaybackKind.None;
        _activeChainSkillDefinition = null;
        _activeChainRequestId = 0;
        _activeChainCastPointNormalized = 0.35f;
        _activeChainReleaseRequested = false;
        _activeChainReleased = false;
        _chainStateCanExit = true;
    }

    private void InterruptActiveChainRequest()
    {
        int requestId = _activeChainRequestId;
        bool shouldNotify = _activeChainReleaseRequested && requestId > 0;

        ClearActiveChainRequest();

        if (shouldNotify)
            ChainPlaybackInterrupted?.Invoke(requestId);
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
}
