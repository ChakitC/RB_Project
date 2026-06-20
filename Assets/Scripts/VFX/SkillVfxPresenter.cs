using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SkillVfxPresenter : MonoBehaviour
{
    readonly Dictionary<int, AnimationVfxSessionToken> _sessionsByRequest =
        new Dictionary<int, AnimationVfxSessionToken>();

    CharacterAnimBrain _animBrain;
    CharacterSkillManager _skillManager;
    AnimationVfxPresenter _presenter;

    void OnEnable()
    {
        ResolveReferences();
        Subscribe();
    }

    void OnDisable()
    {
        Unsubscribe();
        EndAllSessions();
    }

    void ResolveReferences()
    {
        if (!_animBrain)
        {
            TryGetComponent(out _animBrain);
            if (!_animBrain)
                _animBrain = GetComponentInParent<CharacterAnimBrain>();
        }

        if (!_skillManager)
        {
            TryGetComponent(out _skillManager);
            if (!_skillManager)
                _skillManager = GetComponentInParent<CharacterSkillManager>();
        }

        EnsurePresenter();
    }

    void Subscribe()
    {
        if (_animBrain != null)
        {
            _animBrain.SkillAnimationVfxCueRaised += HandleVfxCue;
            _animBrain.PlaybackEvent += HandlePlaybackEvent;
        }

        if (_skillManager != null)
            _skillManager.CastCancelled += HandleCastCancelled;
    }

    void Unsubscribe()
    {
        if (_animBrain != null)
        {
            _animBrain.SkillAnimationVfxCueRaised -= HandleVfxCue;
            _animBrain.PlaybackEvent -= HandlePlaybackEvent;
        }

        if (_skillManager != null)
            _skillManager.CastCancelled -= HandleCastCancelled;
    }

    void HandleVfxCue(CharacterAnimBrain.SkillAnimationVfxCueSignal signal)
    {
        if (signal.Phase != CharacterAnimBrain.SkillAnimationVfxPhase.MainSkill)
            return;
        if (signal.RequestId <= 0 || signal.CueIndex < 0)
            return;
        if (signal.SkillDef == null || !signal.SkillDef.HasSkillVfxEvents)
            return;

        if (!_sessionsByRequest.TryGetValue(signal.RequestId, out AnimationVfxSessionToken token))
        {
            EnsurePresenter();
            if (_presenter == null) return;

            Transform source = _animBrain != null ? _animBrain.transform : transform;
            token = _presenter.BeginSession(
                new SkillVfxCueSource(signal.SkillDef.SkillVfxEvents),
                SkillVfxAnchorResolver.CreateContext(source));
            _sessionsByRequest[signal.RequestId] = token;
        }

        _presenter?.HandleCue(token, signal.CueIndex);
    }

    void HandlePlaybackEvent(CharacterAnimBrain.PlaybackSignal signal)
    {
        if (signal.Kind != CharacterAnimBrain.PlaybackKind.Skill &&
            signal.Kind != CharacterAnimBrain.PlaybackKind.ChainSkill)
            return;

        if (signal.Phase == CharacterAnimBrain.PlaybackPhase.Completed ||
            signal.Phase == CharacterAnimBrain.PlaybackPhase.Interrupted)
        {
            EndRequest(signal.RequestId);
        }
    }

    void HandleCastCancelled(ActiveSkillCastInfo castInfo, SkillCastCancelReason reason)
    {
        EndRequest(castInfo.RequestId);
    }

    void EndRequest(int requestId)
    {
        if (requestId <= 0)
        {
            EndAllSessions();
            return;
        }

        if (!_sessionsByRequest.TryGetValue(requestId, out AnimationVfxSessionToken token))
            return;

        _sessionsByRequest.Remove(requestId);
        _presenter?.EndSession(token);
    }

    void EndAllSessions()
    {
        if (_presenter != null)
        {
            foreach (AnimationVfxSessionToken token in _sessionsByRequest.Values)
                _presenter.EndSession(token);
        }

        _sessionsByRequest.Clear();
    }

    void EnsurePresenter()
    {
        if (_presenter == null)
            _presenter = GetComponent<AnimationVfxPresenter>();

        if (_presenter == null)
            _presenter = gameObject.AddComponent<AnimationVfxPresenter>();
    }
}
