using UnityEngine;

public abstract class PassiveDefinition : ScriptableObject
{
    [Header("Identity")]
    public string passiveId;

    [TextArea(2, 4)]
    public string description;

    public abstract PassiveKind Kind { get; }

    public string RuntimeId => string.IsNullOrWhiteSpace(passiveId) ? name : passiveId;
}
