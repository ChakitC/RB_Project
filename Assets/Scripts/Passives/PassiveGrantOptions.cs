using System.Collections.Generic;

/// <summary>Frozen installation inputs; never writes to a recipe asset.</summary>
public sealed class PassiveGrantOptions
{
    readonly Dictionary<string, float> numbers;
    public readonly string WeaponInstanceId;
    public readonly IPassiveStateStore StateStore;
    public readonly bool UsesWorldSlow;
    public readonly System.Action<string, int> Feedback;
    public PassiveGrantOptions(string weaponInstanceId = null, IPassiveStateStore stateStore = null,
        IReadOnlyDictionary<string, float> parameters = null, bool usesWorldSlow = true, System.Action<string, int> feedback = null)
    {
        WeaponInstanceId = weaponInstanceId; StateStore = stateStore; UsesWorldSlow = usesWorldSlow; Feedback = feedback;
        if (parameters != null)
        {
            numbers = new();
            foreach (var pair in parameters) numbers.Add(pair.Key, pair.Value);
        }
    }
    public bool TryGetNumber(string id, out float value)
    { value = 0; return numbers != null && numbers.TryGetValue(id, out value); }
    public void Validate(PassiveRecipe recipe, List<string> errors)
    {
        if (numbers == null) return;
        foreach (var pair in numbers)
        {
            bool declared = false;
            foreach (var parameter in recipe.parameters)
                if (parameter != null && parameter.id == pair.Key && parameter.kind == PassiveParameterKind.Number) { declared = true; break; }
            if (!declared || !float.IsFinite(pair.Value)) errors.Add("Invalid grant number parameter: " + pair.Key);
        }
    }
}
