using Opsive.GraphDesigner.Runtime.Variables;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;

[Opsive.Shared.Utility.Category("AI/Combat")]
public sealed class AICombatWait : Action
{
    public SharedVariable<CharacteContext> context;
    public float duration = .5f;
    CharacteContext ctx;
    float elapsed;
    int generation;
    public override void OnStart()
    {
        ctx = AIContextBinding.Read(context);
        generation = ctx != null ? ctx.LifeGeneration : 0;
        elapsed = 0f;
    }
    public override TaskStatus OnUpdate()
    {
        if (!AIContextBinding.IsCurrent(context, ctx, generation)) return TaskStatus.Failure;
        elapsed += AICombatTaskUtility.Delta(ctx);
        return elapsed >= duration ? TaskStatus.Success : TaskStatus.Running;
    }
}
