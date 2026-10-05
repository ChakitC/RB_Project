using Opsive.GraphDesigner.Runtime.Variables;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;
using UnityEngine;

public class TryCastSkillFromSlot : ActionNode
{
    public SharedVariable<CharacteContext> context;
    [SerializeField] int SlotIndex;

    private CharacteContext CTX;
    int executionGeneration;
    private CharacterAnimBrain animBrain;
    private CharacterSkillManager observedSkillManager;
    private int activeRequestId;
    private bool waitingForAnimation;
    private TaskStatus currentStatus = TaskStatus.Failure;

    public override void OnStart()
    {
        UnsubscribePlaybackEvent();
        currentStatus = TaskStatus.Failure;
        waitingForAnimation = false;
        activeRequestId = 0;

        if (!TryCacheReferences() || CTX.SkillManager == null)
            return;

        observedSkillManager = CTX.SkillManager;
        var result = CTX.SkillManager.TryStartCastSlot(SlotIndex);
        switch (result.Kind)
        {
            case SkillCastStartKind.ImmediateSuccess:
                currentStatus = TaskStatus.Success;
                break;

            case SkillCastStartKind.WaitingForAnimation:
                if (animBrain == null || result.RequestId <= 0)
                    break;

                activeRequestId = result.RequestId;
                waitingForAnimation = true;
                currentStatus = TaskStatus.Running;
                animBrain.PlaybackEvent -= OnPlaybackEvent;
                animBrain.PlaybackEvent += OnPlaybackEvent;
                observedSkillManager.CastCancelled -= OnCastCancelled;
                observedSkillManager.CastCancelled += OnCastCancelled;
                break;
        }
    }

    public override TaskStatus OnUpdate()
    {
        if (!AIContextBinding.IsCurrent(context, CTX, executionGeneration) ||
            CTX.SkillManager != observedSkillManager || CTX.AnimBrain != animBrain)
        {
            UnsubscribePlaybackEvent();
            currentStatus = TaskStatus.Failure;
        }
        return currentStatus;
    }

    public override void OnEnd()
    {
        UnsubscribePlaybackEvent();
        waitingForAnimation = false;
        activeRequestId = 0;
    }

    public override void OnBehaviorTreeStopped(bool paused) { if (!paused) OnEnd(); }
    public override void OnDestroy() => OnEnd();

    private bool TryCacheReferences()
    {
        CTX = AIContextBinding.Read(context);
        executionGeneration = CTX != null ? CTX.LifeGeneration : 0;
        animBrain = CTX != null ? CTX.AnimBrain : null;
        return CTX != null && CTX.SkillManager != null;
    }

    private void OnPlaybackEvent(CharacterAnimBrain.PlaybackSignal signal)
    {
        if (!AIContextBinding.IsCurrent(context, CTX, executionGeneration) || !waitingForAnimation || signal.RequestId != activeRequestId)
            return;

        if (signal.Kind != CharacterAnimBrain.PlaybackKind.Skill)
            return;

        switch (signal.Phase)
        {
            case CharacterAnimBrain.PlaybackPhase.Completed:
                currentStatus = TaskStatus.Success;
                waitingForAnimation = false;
                UnsubscribePlaybackEvent();
                break;

            case CharacterAnimBrain.PlaybackPhase.Interrupted:
                currentStatus = TaskStatus.Failure;
                waitingForAnimation = false;
                UnsubscribePlaybackEvent();
                break;
        }
    }

    private void UnsubscribePlaybackEvent()
    {
        if (animBrain != null)
            animBrain.PlaybackEvent -= OnPlaybackEvent;

        if (observedSkillManager != null)
            observedSkillManager.CastCancelled -= OnCastCancelled;
    }

    private void OnCastCancelled(ActiveSkillCastInfo castInfo, SkillCastCancelReason reason)
    {
        if (!AIContextBinding.IsCurrent(context, CTX, executionGeneration) || !waitingForAnimation || castInfo.RequestId != activeRequestId)
            return;

        UnsubscribePlaybackEvent();
        currentStatus = TaskStatus.Failure;
        waitingForAnimation = false;
        UnsubscribePlaybackEvent();
    }
}
