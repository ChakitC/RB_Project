using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Conditionals;
using UnityEngine;

/// <summary>
/// Checks whether the configured skill slot can start a cast without casting it.
/// </summary>
public class SkillReady : ConditionalNode
{
    [SerializeField, Min(0)] int SlotIndex;

    private CharacteContext ctx;

    public override TaskStatus OnUpdate()
    {
        if (ctx == null)
            ctx = gameObject.GetComponentInParent<CharacteContext>();

        if (ctx == null)
            return TaskStatus.Failure;

        if (ctx.SkillManager == null)
            ctx.ResolveReferences();

        return ctx.SkillManager != null && ctx.SkillManager.CanStartCastSlot(SlotIndex)
            ? TaskStatus.Success
            : TaskStatus.Failure;
    }
}
