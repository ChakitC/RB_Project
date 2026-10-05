using UnityEngine;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Opsive.BehaviorDesigner.Runtime.Tasks.Actions;
using Opsive.GraphDesigner.Runtime.Variables;

[Opsive.Shared.Utility.Category("AI/Context")]
public sealed class InitializeAIContext : Action
{
    public SharedVariable<CharacteContext> context;
    CharacteContext owner;
    int generation;
    bool restart = true;
    bool resuming;
    bool reportedError;

    public override void OnBehaviorTreeStarted()
    {
        if (!resuming) restart = true;
        resuming = false;
    }

    public override TaskStatus OnUpdate()
    {
        if (!AIContextBinding.IsBound(context)) return Fail("Bind context to the Graph variable Ctx.");
        if (restart || owner == null || !owner.isActiveAndEnabled || owner.LifeGeneration != generation ||
            !transform.IsChildOf(owner.transform))
        {
            owner = CharacterContextModuleLookup.ResolveContext(transform);
            if (owner == null || !owner.isActiveAndEnabled) return Fail("No active owning CharacteContext.");
            owner.ResolveReferences();
            generation = owner.LifeGeneration;
            restart = false;
        }
        context.Value = owner;
        return TaskStatus.Success;
    }

    TaskStatus Fail(string reason)
    {
        if (context != null) context.Value = null;
        if (!reportedError)
        {
            Debug.LogError($"[InitializeAIContext] {reason}", gameObject);
            reportedError = true;
        }
        return TaskStatus.Failure;
    }

    public override void OnBehaviorTreeStopped(bool paused)
    {
        resuming = paused;
        if (!paused) restart = true;
    }
}
