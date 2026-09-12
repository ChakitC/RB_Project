using UnityEngine;

[CreateAssetMenu(fileName = "PartyComboFeatureFlags", menuName = "Game/Party Combo/Feature Flags")]
public sealed class PartyComboFeatureFlags : ScriptableObject
{
    public bool runtimeEnabled;
    public bool phase4aPublishersEnabled;
    [Min(0.1f)] public float maxComboChainLifetimeSeconds = 10f;
    [Range(1, 32)] public int maxComboDepth = 8;
    public bool debugLogging;
}

public static class PartyComboFeatureGate
{
    static PartyComboFeatureFlags current;

    public static bool RuntimeEnabled => current != null && current.runtimeEnabled;
    public static bool PublishersEnabled =>
        current != null && current.runtimeEnabled && current.phase4aPublishersEnabled;
    public static PartyComboFeatureFlags Current => current;

    public static void Bind(PartyComboFeatureFlags flags)
    {
        current = flags;
    }

    public static void Unbind(PartyComboFeatureFlags flags)
    {
        if (current == flags)
            current = null;
    }
}
