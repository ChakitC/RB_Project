using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;
using UnityEngine;

public class TryCastSkillFromSlot : ActionNode
{
    [SerializeField] int SlotIndex;

    private CharacteContext CTX;
    private CharacterAnimBrain animBrain;
    private int activeRequestId;
    private bool waitingForAnimation;
    private TaskStatus currentStatus = TaskStatus.Failure;

    public override void OnStart()
    {
        currentStatus = TaskStatus.Failure;
        waitingForAnimation = false;
        activeRequestId = 0;

        if (!TryCacheReferences() || CTX.SkillManager == null)
            return;

        var result = CTX.SkillManager.TryStartCastSlot(SlotIndex);
        switch (result.Kind)
        {
            case PlayerSkillManager.SkillCastStartKind.ImmediateSuccess:
                currentStatus = TaskStatus.Success;
                break;

            case PlayerSkillManager.SkillCastStartKind.WaitingForAnimation:
                if (animBrain == null || result.RequestId <= 0)
                    break;

                activeRequestId = result.RequestId;
                waitingForAnimation = true;
                currentStatus = TaskStatus.Running;
                animBrain.PlaybackEvent -= OnPlaybackEvent;
                animBrain.PlaybackEvent += OnPlaybackEvent;
                break;
        }
    }

    public override TaskStatus OnUpdate()
    {
        return currentStatus;
    }

    public override void OnEnd()
    {
        UnsubscribePlaybackEvent();
        waitingForAnimation = false;
        activeRequestId = 0;
    }

    private bool TryCacheReferences()
    {
        if (CTX == null)
            CTX = gameObject.GetComponent<CharacteContext>();

        if (CTX == null)
            return false;

        if (CTX.SkillManager == null)
            CTX.SkillManager = gameObject.GetComponent<PlayerSkillManager>();

        var resolvedAnimBrain = CTX.AnimBrain != null
            ? CTX.AnimBrain
            : gameObject.GetComponent<CharacterAnimBrain>();

        if (CTX.AnimBrain == null)
            CTX.AnimBrain = resolvedAnimBrain;

        if (animBrain != resolvedAnimBrain)
        {
            UnsubscribePlaybackEvent();
            animBrain = resolvedAnimBrain;
        }

        return CTX.SkillManager != null;
    }

    private void OnPlaybackEvent(CharacterAnimBrain.PlaybackSignal signal)
    {
        if (!waitingForAnimation || signal.RequestId != activeRequestId)
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
    }
}
