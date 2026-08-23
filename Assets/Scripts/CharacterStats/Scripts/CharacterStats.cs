using System.Collections.Generic;
using Opsive.BehaviorDesigner.Runtime;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public enum CharacterWeaponHandMode
{
    RightHand = 0,
    LeftHand = 1,
    BothHands = 2,
    None = 3
}

public enum CharacterCombatRole
{
    Generic = 0,
    Tank = 1,
    Healer = 2,
    Support = 3,
    Sniper = 4,
    Assault = 5
}

[System.Serializable]
public sealed class CharacterEventVoiceLine
{
    [LabelText("Voice Cue"), AssetsOnly]
    public AudioCue cue;

    [Range(0f, 1f)]
    public float chance = 1f;

    [MinValue(0f), SuffixLabel("s")]
    public float cooldown = 0f;
}

[System.Serializable]
public sealed class CharacterVoiceProfile
{
    [FoldoutGroup("Skill Voice", Expanded = false), LabelText("Default Skill Voice Cue"), AssetsOnly]
    public AudioCue defaultSkillVoiceCue;

    [FoldoutGroup("Skill Voice", Expanded = false), LabelText("Default Chance"), Range(0f, 1f)]
    public float defaultSkillVoiceChance = 1f;

    [FoldoutGroup("Skill Voice", Expanded = false), LabelText("Default Cooldown"), MinValue(0f), SuffixLabel("s")]
    public float defaultSkillVoiceCooldown = 0f;

    [FoldoutGroup("Skill Voice", Expanded = false), LabelText("Global Cooldown"), MinValue(0f), SuffixLabel("s")]
    public float globalSkillVoiceCooldown = 0.25f;

    [FoldoutGroup("Skill Voice", Expanded = false), LabelText("Skill Cast Lines")]
    [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true, DraggableItems = true, ShowPaging = false, NumberOfItemsPerPage = 0)]
    public List<CharacterSkillVoiceLine> skillCastLines = new();

    [FoldoutGroup("Event Voice", Expanded = false), LabelText("Dash")]
    public CharacterEventVoiceLine dashVoice = new();

    [FoldoutGroup("Event Voice", Expanded = false), LabelText("Knockback")]
    public CharacterEventVoiceLine knockbackVoice = new();

    [FoldoutGroup("Event Voice", Expanded = false), LabelText("Select Character")]
    public CharacterEventVoiceLine selectCharacterVoice = new();

    [FoldoutGroup("Event Voice", Expanded = false), LabelText("Pick Up Character")]
    public CharacterEventVoiceLine pickupCharacterVoice = new();

    [FoldoutGroup("Low HP Voice", Expanded = false), LabelText("Voice")]
    public CharacterEventVoiceLine lowHpVoice = new();

    [FoldoutGroup("Low HP Voice", Expanded = false), LabelText("Threshold"), Range(0f, 1f)]
    public float lowHpThreshold = 0.3f;
}

[System.Serializable]
public sealed class CharacterSkillVoiceLine
{
    [HorizontalGroup("Match", Width = 160), LabelText("Skill"), AssetsOnly]
    public SkillGemDefinition skill;

    [HorizontalGroup("Match"), LabelText("Tags")]
    public SkillTag tags = SkillTag.None;

    [LabelText("Voice Cue"), AssetsOnly]
    public AudioCue cue;

    [Range(0f, 1f)]
    public float chance = 1f;

    [MinValue(0f), SuffixLabel("s")]
    public float cooldown = 0f;

    public bool MatchesExactSkill(SkillGemDefinition skillDef)
    {
        return skill != null && skill == skillDef;
    }

    public bool MatchesTags(SkillGemDefinition skillDef)
    {
        return skill == null &&
               tags != SkillTag.None &&
               skillDef != null &&
               (skillDef.tags & tags) != 0;
    }
}



[HideMonoScript]
[CreateAssetMenu(menuName = "Character Stats")]
public class CharacterStats : ScriptableObject
{
    private string DisplayName => string.IsNullOrWhiteSpace(characterName) ? name : characterName;

    [PropertyOrder(-200)]
    [ShowInInspector, ReadOnly, FoldoutGroup("Overview", Expanded = true), LabelText("Character")]
    private string OverviewCharacter => string.IsNullOrWhiteSpace(characterId)
        ? DisplayName
        : $"{DisplayName} ({characterId})";

    [PropertyOrder(-199)]
    [ShowInInspector, ReadOnly, FoldoutGroup("Overview", Expanded = true), LabelText("Base Stats")]
    private string OverviewBaseStats => $"HP {maxHP:0.##} | DMG {Damage:0.##} | ARM {armor:0.##} | SPD {speed:0.##}";

    [PropertyOrder(-198)]
    [ShowInInspector, ReadOnly, FoldoutGroup("Overview", Expanded = true), LabelText("Growth / Lv")]
    private string OverviewScaling => $"HP +{MAXHPScaling:0.##} | DMG +{DamageScaling:0.##} | SPD +{SpeedScaling:0.##}";

    [PropertyOrder(-197)]
    [ShowInInspector, FoldoutGroup("Overview", Expanded = true), LabelText("Preview Level"), MinValue(1)]
    private int overviewPreviewLevel = 1;

    [PropertyOrder(-196)]
    [ShowInInspector, ReadOnly, FoldoutGroup("Overview", Expanded = true), LabelText("Preview Result")]
    private string OverviewPreviewStatsPrimary =>
        $"Lv {overviewPreviewLevel:0} | HP {GetPreviewStat(maxHP, MAXHPScaling):0.##} | DMG {GetPreviewStat(Damage, DamageScaling):0.##} | ARM {GetPreviewStat(armor, ArmorScaling):0.##} | SPD {GetPreviewStat(speed, SpeedScaling):0.##}";

    [PropertyOrder(-195)]
    [ShowInInspector, ReadOnly, FoldoutGroup("Overview", Expanded = true), LabelText("Preview Combat")]
    private string OverviewPreviewStatsSecondary =>
        $"STA {GetPreviewStat(maxStamina, StaminaScaling):0.##} | ENG {GetPreviewStat(Enagy, EnagyScaling):0.##} | CR {GetPreviewStat(critRate, CritrateScaling):0.##} | CD {GetPreviewCritDamage():0.##}";

    private int PreviewLevelOffset => Mathf.Max(0, overviewPreviewLevel - 1);

    private float GetPreviewStat(float baseValue, float scalingPerLevel)
    {
        return baseValue + scalingPerLevel * PreviewLevelOffset;
    }

    private float GetPreviewCritDamage()
    {
        return Mathf.Max(1f, critMultiplier + CritDamageScaling * PreviewLevelOffset);
    }

    [PropertyOrder(-161)]
    [FoldoutGroup("Character Detail", Expanded = true), HideLabel, PreviewField(80, ObjectFieldAlignment.Left), AssetsOnly]
    public Sprite icon;

    [PropertyOrder(-160)]
    [FoldoutGroup("Character Detail", Expanded = true), LabelText("Character ID")]
    public string characterId;

    [PropertyOrder(-159)]
    [FoldoutGroup("Character Detail", Expanded = true), LabelText("Character Name")]
    public string characterName;

    [PropertyOrder(-158)] [FoldoutGroup("Character Detail", Expanded = true), LabelText("WeaponType")]
    public WeaponType CharacterWeaponType;

    [PropertyOrder(-157)]
    [FoldoutGroup("Character Detail", Expanded = true), LabelText("Character Prefab"), AssetsOnly]
    public GameObject CharacterPrefab;

    [PropertyOrder(-156)]
    [FoldoutGroup("Character Detail", Expanded = true), LabelText("Basement Prefab"), AssetsOnly]
    public GameObject CharacterPrefabBasement;

    [PropertyOrder(-155)]
    [FoldoutGroup("Character Detail", Expanded = true), LabelText("Avatar"), AssetsOnly]
    public Avatar characterAvatar;

    [PropertyOrder(-154)]
    [FoldoutGroup("Character Detail", Expanded = true), LabelText("Animator Controller"), AssetsOnly]
    public RuntimeAnimatorController controller;

    [PropertyOrder(-153)]
    [FoldoutGroup("Character Detail", Expanded = true), LabelText("Anim Profile"), AssetsOnly]
    public CharacterAnimProfileSO animProfile;

    [PropertyOrder(-152)]
    [FoldoutGroup("AI", Expanded = false), LabelText("Behavior Subtree"), AssetsOnly]
    public Subtree behaviorSubtree;

    [PropertyOrder(-152)]
    [FoldoutGroup("Third Person", Expanded = false), LabelText("TPS Profile")]
    public ThirdPersonCharacterProfile thirdPersonProfile = ThirdPersonCharacterProfile.CreateDefault();

    [PropertyOrder(-140)]
    [FoldoutGroup("Weapon Visual", Expanded = true), LabelText("Weapon Hand Mode")]
    public CharacterWeaponHandMode weaponHandMode = CharacterWeaponHandMode.RightHand;

    [PropertyOrder(-139)]
    [FoldoutGroup("Combat Role", Expanded = true), LabelText("Combat Role")]
    public CharacterCombatRole combatRole = CharacterCombatRole.Generic;

    [PropertyOrder(-138)]
    [FoldoutGroup("Chain Attack", Expanded = false), LabelText("Default Chain Skill"), AssetsOnly]
    public SkillGemDefinition chainAttackSkill;

    [PropertyOrder(-137)]
    [FoldoutGroup("Chain Attack", Expanded = false), LabelText("Intro Chain Cutscene"), AssetsOnly]
    public CutsceneDefSO introChainCutscene;

    public bool HasIntroChainCutscene =>
        introChainCutscene != null &&
        introChainCutscene.cutscene != null &&
        introChainCutscene.cutscene.characterCutsceneClip != null &&
        introChainCutscene.cutscene.characterCutsceneClip.IsValid;

    /// <summary>
    /// What this character is used as in the party. Decides which half of the Skill Loadout
    /// applies - a Stryker casts from command slots, a Helper only ever performs triggered assists.
    /// </summary>
    [PropertyOrder(-137)]
    [FoldoutGroup("Skill Loadout", Expanded = false), LabelText("Party Role"), EnumToggleButtons]
    public CharacterPartyRole partyRole = CharacterPartyRole.Stryker;

    [PropertyOrder(-136)]
    [FoldoutGroup("Skill Loadout", Expanded = false), LabelText("Skill Slots")]
    [ShowIf(nameof(partyRole), CharacterPartyRole.Stryker)]
    [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true, DraggableItems = true, ShowPaging = false, NumberOfItemsPerPage = 0)]
    public List<CharacterSkillLoadoutSlot> skillSlots = new();

    [PropertyOrder(-135)]
    [FoldoutGroup("Skill Loadout", Expanded = false), LabelText("Skill Points Per Level"), MinValue(0)]
    public int skillPointsPerLevel = 1;

    /// <summary>
    /// Helper assist slots this character brings to the party.
    ///
    /// These belong here, not on a prefab: every party-slot rig and the helper actor itself are
    /// shared components that any character can be loaded into, so a proc authored on one of them
    /// would fire for whoever happens to occupy that slot. Authoring it on the character asset is
    /// the same rule <see cref="skillSlots"/> already follows.
    ///
    /// Each slot holds its own variants, and every variant keeps separate Skill Tree progress
    /// while drawing from the one shared <see cref="skillPointsPerLevel"/> pool.
    /// </summary>
    [PropertyOrder(-134)]
    [FoldoutGroup("Skill Loadout", Expanded = false), LabelText("Helper Proc Slots")]
    [ShowIf(nameof(partyRole), CharacterPartyRole.Helper)]
    [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true, DraggableItems = true, ShowPaging = false, NumberOfItemsPerPage = 0)]
    public List<HelperProcLoadoutSlot> helperProcSlots = new();

    /// <summary>
    /// The assist this character performs when the player issues a manual party command.
    ///
    /// Same ownership rule as <see cref="helperProcSlots"/>: the helper actor is a shared rig, so
    /// the skill has to travel with the character asset rather than sit on the prefab. A slot with
    /// no configured option is a valid authoring choice - it means this helper has no manual
    /// command, and nothing falls back to a prefab-authored skill.
    /// </summary>
    [PropertyOrder(-133)]
    [FoldoutGroup("Skill Loadout", Expanded = false), LabelText("Helper Command Slot")]
    [ShowIf(nameof(partyRole), CharacterPartyRole.Helper)]
    public CharacterSkillLoadoutSlot helperCommandSlot = new();

    /// <summary>True when this character contributes helper assists rather than command slots.</summary>
    public bool IsHelperRole => partyRole == CharacterPartyRole.Helper;
    
    
    [PropertyOrder(-120)]
    [FoldoutGroup("Base Stats", Expanded = true), HorizontalGroup("Base Stats/Row 01"), LabelText("Max HP")]
    public float maxHP = 100;

    [PropertyOrder(-119)]
    [FoldoutGroup("Base Stats", Expanded = true), HorizontalGroup("Base Stats/Row 01"), LabelText("Max Stamina")]
    public float maxStamina = 80;

    [PropertyOrder(-118)]
    [FoldoutGroup("Base Stats", Expanded = true), HorizontalGroup("Base Stats/Row 02"), LabelText("Damage")]
    public float Damage = 10;

    [PropertyOrder(-117)]
    [FoldoutGroup("Base Stats", Expanded = true), HorizontalGroup("Base Stats/Row 02"), LabelText("Armor")]
    public float armor = 1;

    [PropertyOrder(-116)]
    [FoldoutGroup("Base Stats", Expanded = true), HorizontalGroup("Base Stats/Row 03"), LabelText("Crit Damage")]
    public float critMultiplier = 1;

    [PropertyOrder(-115)]
    [FoldoutGroup("Base Stats", Expanded = true), HorizontalGroup("Base Stats/Row 03"), LabelText("Crit Rate")]
    public float critRate = 0;

    [PropertyOrder(-114)]
    [FoldoutGroup("Base Stats", Expanded = true), HorizontalGroup("Base Stats/Row 04"), LabelText("Energy")]
    public float Enagy = 100;

    [PropertyOrder(-113)]
    [FoldoutGroup("Base Stats", Expanded = true), HorizontalGroup("Base Stats/Row 04"), LabelText("Move Speed")]
    public float speed = 4.5f;
    
    [PropertyOrder(-100)]
    [FoldoutGroup("Base Stats Down", Expanded = false), LabelText("Down Move Speed")]
    public float speedDown = 1;
    
    [PropertyOrder(-80)]
    [FoldoutGroup("Level Scaling", Expanded = false), HorizontalGroup("Level Scaling/Row 01"), LabelText("Damage"), SuffixLabel("/ Lv")]
    public float DamageScaling;

    [PropertyOrder(-79)]
    [FoldoutGroup("Level Scaling", Expanded = false), HorizontalGroup("Level Scaling/Row 01"), LabelText("Armor"), SuffixLabel("/ Lv")]
    public float ArmorScaling;

    [PropertyOrder(-78)]
    [FoldoutGroup("Level Scaling", Expanded = false), HorizontalGroup("Level Scaling/Row 02"), LabelText("Max HP"), SuffixLabel("/ Lv")]
    public float MAXHPScaling;

    [PropertyOrder(-77)]
    [FoldoutGroup("Level Scaling", Expanded = false), HorizontalGroup("Level Scaling/Row 02"), LabelText("Stamina"), SuffixLabel("/ Lv")]
    public float StaminaScaling;

    [PropertyOrder(-76)]
    [FoldoutGroup("Level Scaling", Expanded = false), HorizontalGroup("Level Scaling/Row 03"), LabelText("Crit Rate"), SuffixLabel("/ Lv")]
    public float CritrateScaling;

    [PropertyOrder(-75)]
    [FoldoutGroup("Level Scaling", Expanded = false), HorizontalGroup("Level Scaling/Row 03"), LabelText("Crit Damage"), SuffixLabel("/ Lv")]
    public float CritDamageScaling;

    [PropertyOrder(-74)]
    [FoldoutGroup("Level Scaling", Expanded = false), HorizontalGroup("Level Scaling/Row 04"), LabelText("Energy"), SuffixLabel("/ Lv")]
    public float EnagyScaling;

    [PropertyOrder(-73)]
    [FoldoutGroup("Level Scaling", Expanded = false), HorizontalGroup("Level Scaling/Row 04"), LabelText("Move Speed"), SuffixLabel("/ Lv")]
    public float SpeedScaling;

    [PropertyOrder(-20)]
    [FoldoutGroup("Audio", Expanded = false), LabelText("Dash Cue"), AssetsOnly]
    public AudioCue dashCue;

    [PropertyOrder(-13)]
    [FoldoutGroup("Audio", Expanded = false), LabelText("Voice Profile")]
    public CharacterVoiceProfile voiceProfile = new();

    [PropertyOrder(-19)]
    [FoldoutGroup("Audio", Expanded = false), LabelText("Melee Light Cue"), AssetsOnly]
    public AudioCue meleeLightCue;

    [PropertyOrder(-18)]
    [FoldoutGroup("Audio", Expanded = false), LabelText("Melee Heavy Cue"), AssetsOnly]
    public AudioCue meleeHeavyCue;

    [PropertyOrder(-17)]
    [FoldoutGroup("Audio", Expanded = false), LabelText("Damaged Cue"), AssetsOnly]
    public AudioCue damagedCue;

    [PropertyOrder(-16)]
    [FoldoutGroup("Audio", Expanded = false), LabelText("Down Cue"), AssetsOnly]
    public AudioCue downCue;

    [PropertyOrder(-15)]
    [FoldoutGroup("Audio", Expanded = false), LabelText("Death Cue"), AssetsOnly]
    public AudioCue deathCue;

    [PropertyOrder(-14)]
    [FoldoutGroup("Audio", Expanded = false), LabelText("Revive Cue"), AssetsOnly]
    public AudioCue reviveCue;
}
