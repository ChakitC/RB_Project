using UnityEngine;

[CreateAssetMenu(fileName = "PartyFollowUpFeatureFlags", menuName = "Game/Party Follow-Up/Feature Flags")]
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: null, sourceAssembly: "Assembly-CSharp", sourceClassName: "PartyComboFeatureFlags")]
public sealed class PartyFollowUpFeatureFlags : ScriptableObject
{
    public bool runtimeEnabled;
    public bool phase4aPublishersEnabled;
    [UnityEngine.Serialization.FormerlySerializedAs("maxComboChainLifetimeSeconds")]
    [Min(0.1f)] public float maxFollowUpChainLifetimeSeconds = 10f;
    [UnityEngine.Serialization.FormerlySerializedAs("maxComboDepth")]
    [Range(1, 32)] public int maxFollowUpDepth = 8;
    public bool debugLogging;
}

public static class PartyFollowUpFeatureGate
{
    static PartyFollowUpFeatureFlags current;

    public static bool RuntimeEnabled => current != null && current.runtimeEnabled;
    public static bool PublishersEnabled =>
        current != null && current.runtimeEnabled && current.phase4aPublishersEnabled;
    public static PartyFollowUpFeatureFlags Current => current;

    public static void Bind(PartyFollowUpFeatureFlags flags)
    {
        current = flags;
    }

    public static void Unbind(PartyFollowUpFeatureFlags flags)
    {
        if (current == flags)
            current = null;
    }
}
