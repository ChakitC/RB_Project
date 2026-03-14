using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;

/// <summary>
/// A custom action node.
/// </summary>
public class FireWeapon : ActionNode
{
    /// <summary>
    /// Executes the task.
    /// </summary>
    /// <returns>The execution status of the task.</returns>
    public override TaskStatus OnUpdate()
    {
        return TaskStatus.Success;
    }
}