using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SkillVfxPresenter : MonoBehaviour
{
    readonly Dictionary<int, AnimationVfxSessionToken> _sessionsByRequest =
        new Dictionary<int, AnimationVfxSessionToken>();

    CharacterAnimBrain _animBrain;
    AnimationVfxPresenter _presenter;

    public void Initialize(CharacterAnimBrain animBrain)
    {
        _animBrain = animBrain;
        EnsurePresenter();
    }

    public void BeginRequest(int requestId, SkillGemDefinition skill)
    {
        if (requestId <= 0 || skill == null)
            return;

        EndRequest(requestId);
        EnsurePresenter();
        if (_presenter == null)
            return;

        Transform source = _animBrain != null ? _animBrain.transform : transform;
        AnimationVfxSessionToken token = _presenter.BeginSession(
            new SkillVfxCueSource(skill.SkillVfxEvents),
            SkillVfxAnchorResolver.CreateContext(source));
        _sessionsByRequest[requestId] = token;
    }

    public void HandleVfxCue(int requestId, int cueIndex)
    {
        if (requestId <= 0 || cueIndex < 0 || _presenter == null ||
            !_sessionsByRequest.TryGetValue(requestId, out AnimationVfxSessionToken token))
        {
            return;
        }

        _presenter.HandleCue(token, cueIndex);
    }

    public void EndRequest(int requestId)
    {
        if (requestId <= 0)
        {
            if (_presenter != null)
            {
                foreach (AnimationVfxSessionToken sessionToken in _sessionsByRequest.Values)
                    _presenter.EndSession(sessionToken);
            }

            _sessionsByRequest.Clear();
            return;
        }

        if (!_sessionsByRequest.TryGetValue(requestId, out AnimationVfxSessionToken token))
            return;

        _sessionsByRequest.Remove(requestId);
        _presenter?.EndSession(token);
    }

    void OnDisable()
    {
        EndRequest(0);
    }

    void EnsurePresenter()
    {
        if (_presenter == null)
            _presenter = GetComponent<AnimationVfxPresenter>();

        if (_presenter == null)
            _presenter = gameObject.AddComponent<AnimationVfxPresenter>();
    }
}
