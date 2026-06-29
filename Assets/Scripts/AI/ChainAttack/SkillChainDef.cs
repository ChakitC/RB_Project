using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "SkillChainDef", menuName = "Game/Chain Attack/Skill Chain")]
public sealed class SkillChainDef : ScriptableObject
{
    [Header("Identity")]
    [FormerlySerializedAs("procId")]
    public string chainId;
    public string displayName;
    [TextArea] public string description;

    [Header("Trigger")]
    public PassiveEventType triggerEvent = PassiveEventType.Hit;
    public PassiveOriginFilter originFilter = PassiveOriginFilter.ExternalOnly;
    [Range(0f, 1f)] public float procChance = 1f;
    [Min(0f)] public float internalCooldownSeconds = 0f;
    public bool requireTarget = true;
    public bool requireAttackId;
    public bool oncePerAttackId = true;
    public bool requireOwnerAlive = true;

    [Header("Cost")]
    [Min(0f)] public float commandPointCost = 1f;

    [Header("Execution")]
    [FormerlySerializedAs("sequence")]
    public ChainAttackSequenceDef chainSequence;
    [FormerlySerializedAs("blockWhileSequenceActive")]
    public bool blockWhileChainBusy = true;

    [Header("Debug")]
    public bool debugLogging;

    public string RuntimeId => string.IsNullOrWhiteSpace(chainId) ? name : chainId;
    public float ClampedCommandPointCost => Mathf.Max(0f, commandPointCost);
    public bool HasExecutionConfigured => chainSequence != null && chainSequence.HasAnySteps;

    [Header("ChainReady Intro Cutscene")]
    [Tooltip("When enabled, the ChainReady manual-F chain plays an intro cutscene before step 1. The cutscene is a CutsceneDefSO asset referenced per character by CharacterStats.introChainCutscene.")]
    public bool enableChainReadyIntroCutscene;
}
