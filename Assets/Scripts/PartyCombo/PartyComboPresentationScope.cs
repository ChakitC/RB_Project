using System;
using UnityEngine;

// Owned by one accepted cast. Presentation ends at release, independently of
// payload success/charge commit and the ally's recovery reservation.
internal sealed class PartyComboPresentationScope : IDisposable
{
    readonly CharacteContext actor;
    readonly CharacterSkillManager manager;
    readonly CharacterAnimBrain brain;
    readonly GameplayCameraController camera;
    readonly TimeSlowManager slow;
    readonly PartyComboPresentationProfile profile;
    readonly int requestId;
    readonly float castPoint;
    readonly int slowHandle;
    readonly int actorLife;
    bool disposed;

    public PartyComboPresentationScope(
        CharacteContext actor,
        Transform target,
        PartyComboPresentationProfile profile,
        in ActiveSkillCastInfo cast)
    {
        this.actor = actor;
        actorLife = actor.LifeGeneration;
        this.profile = profile;
        manager = actor.SkillManager;
        brain = cast.AnimationDriver != null ? cast.AnimationDriver.Brain : null;
        requestId = cast.RequestId;
        castPoint = cast.CastPointNormalized;
        actor.PushWorldSlowExemption();
        slow = TimeSlowManager.Instance;
        slowHandle = slow.BeginPresentationSlow(profile.EvaluateWorldScale(0f));
        camera = GameplayCameraController.Instance;
        camera?.BeginComboFocus(this, actor.transform, target, profile);
        manager.CastReleased += OnReleased;
    }

    public void Tick()
    {
        if (disposed)
            return;
        if (actor == null || !actor.isActiveAndEnabled ||
            actor.LifeGeneration != actorLife ||
            actor.HealthSystem == null || !actor.HealthSystem.IsAlive ||
            CutsceneDirector.IsCinematicPlaying || NpcPresentationController.IsActive)
        {
            Dispose();
            return;
        }
        if (GlobalTimeScaleManager.Instance.IsPaused)
            return;
        if (brain != null && brain.TryGetActiveSkillNormalizedTime(requestId, out float time))
        {
            float progress = castPoint > 0f ? Mathf.Clamp01(time / castPoint) : 1f;
            slow.UpdatePresentationSlow(slowHandle, profile.EvaluateWorldScale(progress));
            if (progress >= 1f)
                Release(true);
        }
    }

    void OnReleased(ActiveSkillCastInfo cast)
    {
        if (cast.RequestId == requestId)
            Release(true);
    }

    public void Dispose()
    {
        Release(false);
    }

    void Release(bool holdCamera)
    {
        if (disposed)
            return;
        disposed = true;
        if (manager != null)
            manager.CastReleased -= OnReleased;
        if (slow != null)
            slow.EndPresentationSlow(slowHandle);
        if (camera != null)
            camera.EndComboFocus(this, holdCamera);
        if (actor != null)
            actor.PopWorldSlowExemption();
    }
}
