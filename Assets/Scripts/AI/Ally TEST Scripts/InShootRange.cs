using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Conditionals;

/// <summary>
/// A custom conditional node.
/// </summary>
public class InShootRange : ConditionalNode
{
    /// <summary>
    /// Executes the task.
    /// </summary>
    /// <returns>The execution status of the task.</returns>
    public override TaskStatus OnUpdate()
    {
        return TaskStatus.Failure;
    }
}