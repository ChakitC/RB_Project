using UnityEngine;

[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: null, sourceAssembly: "Assembly-CSharp", sourceClassName: "PartyComboTargetPolicy")]
public enum PartyFollowUpTargetPolicy
{
    EventTarget = 0,
    Self = 1,
    EventActor = 2,
}

[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: null, sourceAssembly: "Assembly-CSharp", sourceClassName: "PartyComboOfferRefreshPolicy")]
public enum PartyFollowUpOfferRefreshPolicy
{
    KeepExisting = 0,
    RefreshSameTarget = 1,
    Replace = 2,
}

[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: null, sourceAssembly: "Assembly-CSharp", sourceClassName: "PartyComboOwnerBusyPolicy")]
public enum PartyFollowUpOwnerBusyPolicy
{
    RejectWhenBusy = 0,
    AllowInterruptibleAiAction = 1,
}

[CreateAssetMenu(fileName = "PartyFollowUpSkill", menuName = "Game/Party Follow-Up/Skill")]
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: null, sourceAssembly: "Assembly-CSharp", sourceClassName: "PartyComboSkillDef")]
public sealed class PartyFollowUpSkillDef : ScriptableObject
{
    [Header("Identity")]
    [UnityEngine.Serialization.FormerlySerializedAs("comboId")]
    public string followUpId;
    public string displayName;
    [TextArea] public string description;
    public Sprite iconOverride;

    [Header("Skill")]
    public SkillGemDefinition executionSkill;
    [Tooltip("Upgrade tree owned by this FollowUp, independent of battle-slot upgrades.")]
    public SkillUpgradeTreeDefinition upgradeTree;
    public PartyFollowUpTriggerRule trigger = new PartyFollowUpTriggerRule();

    [Header("Opportunity")]
    [Min(0.1f)] public float offerDurationSeconds = 3f;
    [Min(0.1f)] public float maxOfferLifetimeSeconds = 6f;
    public PartyFollowUpTargetPolicy targetPolicy = PartyFollowUpTargetPolicy.EventTarget;
    public PartyFollowUpOfferRefreshPolicy refreshPolicy = PartyFollowUpOfferRefreshPolicy.RefreshSameTarget;
    public bool requireOwnerAlive = true;
    public bool requireTargetAlive = true;
    public PartyFollowUpOwnerBusyPolicy ownerBusyPolicy = PartyFollowUpOwnerBusyPolicy.AllowInterruptibleAiAction;
    public int priority;

    [Header("Execution")]
    public PartyFollowUpExecutionProfile executionProfile;

    public string RuntimeId => string.IsNullOrWhiteSpace(followUpId) ? name : followUpId.Trim();
    public const string ProgressSlotId = "party-combo";
    public Sprite ResolvedIcon => iconOverride != null
        ? iconOverride
        : executionSkill != null ? executionSkill.icon : null;
}
