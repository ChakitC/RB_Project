using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Connects one pending cast to playback signals; resource decisions belong to the orchestrator.</summary>
internal sealed class SkillCastAnimationBinding
{
    readonly Action<int> castMomentReached;
    readonly Action<int> interrupted;
    CharacterAnimBrain subscribedBrain;
    int requestId;

    public SkillCastAnimationBinding(Action<int> castMomentReached, Action<int> interrupted)
    {
        this.castMomentReached = castMomentReached;
        this.interrupted = interrupted;
    }

    public bool TryPlaySkill(CharacterAnimDriver driver, int id, SkillGemDefinition skill,
        float castPoint, List<CombatTimelineEventName> events, bool planarRootMotion,
        SkillExecutionKind executionKind, Vector2 meleeChainWindow, Action onAccepted = null)
        => driver.TryPlaySkill(id, skill, castPoint, events, planarRootMotion, executionKind, meleeChainWindow, onAccepted);

    public bool HasActiveExecutionContext(CharacterAnimDriver driver, int id)
        => driver != null && driver.Brain != null && id > 0 &&
           driver.Brain.TryGetActiveSkillNormalizedTime(id, out _);

    public void ReleaseBasicMelee(CharacterAnimDriver driver, int id) => driver.ReleaseBasicMeleeCast(id);

    // The driver checks the request identity. External playback is never bound/owned here.
    public void Cancel(CharacterAnimDriver driver, int id) => driver?.CancelSkillCastRequest(id);

    public void Bind(CharacterAnimDriver driver, int id)
    {
        Unbind();
        subscribedBrain = driver != null ? driver.Brain : null;
        requestId = id;
        if (subscribedBrain == null)
            return;
        subscribedBrain.SkillCastMomentReached += OnCastMomentReached;
        subscribedBrain.SkillCastInterrupted += OnInterrupted;
    }

    public void Unbind()
    {
        // Unsubscribe from the exact brain we bound, even if the driver's brain changed.
        if (subscribedBrain != null)
        {
            subscribedBrain.SkillCastMomentReached -= OnCastMomentReached;
            subscribedBrain.SkillCastInterrupted -= OnInterrupted;
        }
        subscribedBrain = null;
        requestId = 0;
    }

    void OnCastMomentReached(int id)
    {
        if (id == requestId) castMomentReached(id);
    }

    void OnInterrupted(int id)
    {
        if (id == requestId) interrupted(id);
    }
}
