using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Conditionals;

/// <summary>
/// Compares the outcome of the most recently resolved Special Shoot Point round against an expected
/// value.
///
/// The controller stamps every outcome with the round's request id, and this task records which
/// round was in flight when its branch began, so a tree can never read the result of an older
/// activation. The outcome is read straight off the controller and is deliberately not mirrored
/// into a mutable graph variable.
///
/// Pure: <see cref="OnUpdate"/> and <see cref="OnReevaluateUpdate"/> return the same answer.
/// </summary>
[Opsive.Shared.Utility.Category("Enemy/Special Shoot Point")]
[Opsive.Shared.Utility.Description(
    "Success when the latest resolved round matches the expected outcome (Succeeded, TimedOut, or " +
    "Cancelled). A round that started after this branch began must resolve first; a stale result " +
    "from an earlier activation never reports Success.")]
public class CompareSpecialShootPointRoundOutcome : Conditional
{
    [UnityEngine.Tooltip("Outcome this task reports Success for.")]
    public SpecialShootPointOutcome expectedOutcome = SpecialShootPointOutcome.Succeeded;

    private SpecialShootPointController controller;

    /// <summary>
    /// The round that was still running when this branch began, if any. Captured in
    /// <see cref="OnStart"/> rather than during evaluation so the comparison itself stays pure.
    /// </summary>
    private int pendingRequestId;

    public override void OnStart()
    {
        CacheReferences();
        pendingRequestId = controller != null ? controller.CurrentRequestId : 0;
    }

    public override TaskStatus OnUpdate()
    {
        return Evaluate();
    }

    public override TaskStatus OnReevaluateUpdate()
    {
        return Evaluate();
    }

    TaskStatus Evaluate()
    {
        CacheReferences();

        if (controller == null)
            return TaskStatus.Failure;

        int resolvedRequestId = controller.LastOutcomeRequestId;

        // Nothing has ever resolved.
        if (resolvedRequestId <= 0)
            return TaskStatus.Failure;

        // A round was in flight when this branch began: only its own result counts, never an
        // earlier one that happens to still be the latest.
        if (pendingRequestId > 0 && resolvedRequestId < pendingRequestId)
            return TaskStatus.Failure;

        return controller.LastOutcome == expectedOutcome ? TaskStatus.Success : TaskStatus.Failure;
    }

    void CacheReferences()
    {
        if (controller != null)
            return;

        controller = SpecialShootPointTaskUtility.Resolve(gameObject);
    }

    public override void Reset()
    {
        expectedOutcome = SpecialShootPointOutcome.Succeeded;
        controller = null;
        pendingRequestId = 0;
    }
}
