using Opsive.GraphDesigner.Runtime.Variables;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Conditionals;
using UnityEngine;

/// <summary>
/// Checks whether the configured skill slot can start a cast without casting it.
/// </summary>
public class SkillReady : ConditionalNode
{
    public SharedVariable<CharacteContext> context;
    [SerializeField, Min(0)] int SlotIndex;

    private CharacteContext ctx;

    public override TaskStatus OnUpdate()
    {
        ctx = AIContextBinding.Read(context);

        if (ctx == null)
            return TaskStatus.Failure;


        return ctx.SkillManager != null && ctx.SkillManager.CanStartCastSlot(SlotIndex)
            ? TaskStatus.Success
            : TaskStatus.Failure;
    }
}
