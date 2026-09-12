using UnityEngine;

public enum PartyComboTargetPolicy
{
    EventTarget = 0,
    Self = 1,
    EventActor = 2,
}

public enum PartyComboOfferRefreshPolicy
{
    KeepExisting = 0,
    RefreshSameTarget = 1,
    Replace = 2,
}

public enum PartyComboOwnerBusyPolicy
{
    RejectWhenBusy = 0,
    AllowInterruptibleAiAction = 1,
}

[CreateAssetMenu(fileName = "PartyComboSkill", menuName = "Game/Party Combo/Skill")]
public sealed class PartyComboSkillDef : ScriptableObject
{
    [Header("Identity")]
    public string comboId;
    public string displayName;
    [TextArea] public string description;
    public Sprite iconOverride;

    [Header("Skill")]
    public SkillGemDefinition executionSkill;
    public PartyComboTriggerRule trigger = new PartyComboTriggerRule();

    [Header("Opportunity")]
    [Min(0.1f)] public float offerDurationSeconds = 3f;
    [Min(0.1f)] public float maxOfferLifetimeSeconds = 6f;
    public PartyComboTargetPolicy targetPolicy = PartyComboTargetPolicy.EventTarget;
    public PartyComboOfferRefreshPolicy refreshPolicy = PartyComboOfferRefreshPolicy.RefreshSameTarget;
    public bool requireOwnerAlive = true;
    public bool requireTargetAlive = true;
    public PartyComboOwnerBusyPolicy ownerBusyPolicy = PartyComboOwnerBusyPolicy.AllowInterruptibleAiAction;
    public int priority;

    [Header("Execution")]
    public PartyComboExecutionProfile executionProfile;

    public string RuntimeId => string.IsNullOrWhiteSpace(comboId) ? name : comboId.Trim();
    public Sprite ResolvedIcon => iconOverride != null
        ? iconOverride
        : executionSkill != null ? executionSkill.icon : null;
}
