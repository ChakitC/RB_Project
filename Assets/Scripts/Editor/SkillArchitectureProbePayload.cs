using System;

// Used by the dedicated test assembly through the Editor harness.
public sealed class SkillArchitectureProbePayload : SkillPayloadDef
{
    public bool ThrowOnExecute;
    public int Executions;
    public SkillCastContext LastContext;

    public override SkillExecutionResult ExecuteWithResult(SkillCastContext context)
    {
        if (ThrowOnExecute) throw new InvalidOperationException("architecture probe failure");
        Executions++;
        LastContext = context;
        return SkillExecutionResult.Succeeded;
    }
}
