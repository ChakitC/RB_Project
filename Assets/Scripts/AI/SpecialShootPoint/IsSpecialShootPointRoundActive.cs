using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Conditionals;

/// <summary>
/// Reports whether a Special Shoot Point round is currently running on this enemy.
///
/// Pure: <see cref="OnUpdate"/> and <see cref="OnReevaluateUpdate"/> return the same answer, so the
/// task behaves identically as a plain guard and under conditional abort.
/// </summary>
[Opsive.Shared.Utility.Category("Enemy/Special Shoot Point")]
[Opsive.Shared.Utility.Description(
    "Success while the round is in Telegraph, Active, or Resolving. Failure when Idle, on " +
    "cooldown, disabled, or when this enemy has no Special Shoot Point controller.")]
public class IsSpecialShootPointRoundActive : Conditional
{
    private SpecialShootPointController controller;

    public override void OnStart()
    {
        CacheReferences();
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

        return controller.IsRoundActive ? TaskStatus.Success : TaskStatus.Failure;
    }

    void CacheReferences()
    {
        if (controller != null)
            return;

        controller = SpecialShootPointTaskUtility.Resolve(gameObject);
    }

    public override void Reset()
    {
        controller = null;
    }
}
