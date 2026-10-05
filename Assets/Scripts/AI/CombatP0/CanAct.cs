[Opsive.Shared.Utility.Category("AI/Combat")]
public sealed class CanAct : AICombatConditional
{
    bool resetPending;
    bool resuming;
    public override void OnBehaviorTreeStarted()
    {
        if (!resuming) resetPending = true;
        resuming = false;
    }
    public override void OnBehaviorTreeStopped(bool paused)
    {
        resuming = paused;
        if (!paused) resetPending = true;
    }
    public override void OnStart()
    {
        base.OnStart();
        InitializeState(resetPending);
    }
    void InitializeState(bool treeStarted)
    {
        if (combatState == null || ctx == null) return;
        var owner = new AITargetHandle(ctx);
        if (treeStarted || State == null || State.Owner != owner)
            combatState.Value = new AICombatState { Owner = owner };
        resetPending = false;
    }
    protected override bool Evaluate()
    {
        return State != null && State.Owner == new AITargetHandle(ctx) && AICombatTaskUtility.CanAct(ctx);
    }
}
