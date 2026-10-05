using Opsive.GraphDesigner.Runtime.Variables;

/// <summary>Consumer-side validation only. Owner discovery belongs to InitializeAIContext.</summary>
public static class AIContextBinding
{
    public static bool IsBound(SharedVariable<CharacteContext> variable) =>
        variable != null && variable.Name == "Ctx" &&
        variable.Scope == SharedVariable.SharingScope.Graph;

    public static CharacteContext Read(SharedVariable<CharacteContext> variable) =>
        IsBound(variable) && variable.Value != null && variable.Value.isActiveAndEnabled
            ? variable.Value : null;

    public static bool IsCurrent(SharedVariable<CharacteContext> variable,
        CharacteContext owner, int generation) =>
        owner != null && Read(variable) == owner && owner.LifeGeneration == generation;
}
