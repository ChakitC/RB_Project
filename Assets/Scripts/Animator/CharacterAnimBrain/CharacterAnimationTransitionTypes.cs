/// <summary>What currently owns character locomotion, or what a command is asking to start.</summary>
public enum CharacterAnimationMode
{
    None = 0,
    Locomotion = 1,
    Crawl = 2,
    Dash = 3,
    FullBodyReload = 4,
    Melee = 5,
    Skill = 6,
    Utility = 7,
    Chain = 8,
    Knockback = 9,
    SoftStatus = 10,
    HardStatus = 11,
    StageIntro = 12,
    Dead = 13,
}

/// <summary>
/// Why a transition is being attempted. Authority escalates: a normal gameplay command yields to
/// almost everything, while a life-state or cinematic override takes locomotion from anyone.
/// </summary>
public enum CharacterAnimationTransitionReason
{
    /// <summary>Ordinary gameplay input: dash, reload, melee, skill, utility, knockback.</summary>
    NormalCommand = 0,

    /// <summary>The character lost control of itself (stagger, external motor takeover).</summary>
    ExternalControlLoss = 1,

    /// <summary>A status effect is imposing a pose.</summary>
    StatusOverride = 2,

    /// <summary>Down, revive, or death.</summary>
    LifeStateOverride = 3,

    /// <summary>A scripted sequence owns the character, such as the MapRun stage intro.</summary>
    CinematicOverride = 4,
}

/// <summary>One admission question for <see cref="CharacterAnimationTransitionPolicy"/>.</summary>
public readonly struct CharacterAnimationTransitionRequest
{
    public readonly CharacterAnimationMode Current;
    public readonly CharacterAnimationMode Requested;
    public readonly CharacterAnimationTransitionReason Reason;

    /// <summary>
    /// Downed is tracked separately from <see cref="CharacterAnimationMode.Crawl"/> on purpose: a character
    /// can be downed while knockback still owns locomotion, so the two are not interchangeable.
    /// </summary>
    public readonly bool IsDowned;

    public CharacterAnimationTransitionRequest(
        CharacterAnimationMode current,
        CharacterAnimationMode requested,
        CharacterAnimationTransitionReason reason,
        bool isDowned)
    {
        Current = current;
        Requested = requested;
        Reason = reason;
        IsDowned = isDowned;
    }
}
