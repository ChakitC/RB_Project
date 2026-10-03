using UnityEngine;

// Prefab-compatible facade. AnimationTimelineBinding owns the actual playback/VFX leases.
[DisallowMultipleComponent]
public sealed class SkillVfxPresenter : MonoBehaviour
{
    CharacterAnimBrain _animBrain;
    CharacterSkillManager _skillManager;

    void OnEnable()
    {
        ResolveReferences();
        if (_skillManager != null) _skillManager.CastCancelled += HandleCastCancelled;
    }

    void OnDisable()
    {
        if (_skillManager != null) _skillManager.CastCancelled -= HandleCastCancelled;
        _animBrain?.EndManagedSkillVfxSessions();
    }

    void ResolveReferences()
    {
        var context = GetComponentInParent<CharacteContext>();
        if (context != null)
        {
            context.ResolveReferences();
            if (_animBrain == null) _animBrain = context.AnimBrain;
            if (_skillManager == null) _skillManager = context.SkillManager;
        }
        if (_animBrain == null) _animBrain = GetComponentInParent<CharacterAnimBrain>();
        if (_skillManager == null) _skillManager = GetComponentInParent<CharacterSkillManager>();
    }

    void HandleCastCancelled(ActiveSkillCastInfo castInfo, SkillCastCancelReason reason)
    {
        _animBrain?.EndManagedSkillVfxSessions(castInfo.RequestId);
    }
}
