#if UNITY_EDITOR
using NUnit.Framework;

/// <summary>
/// The Special Point reaction's place in the animation priority order, asserted as a table against
/// the pure <see cref="CharacterAnimationTransitionPolicy"/>.
///
/// The locked order is
/// <c>Death/Down &gt; Cutscene &gt; active Chain Attack &gt; Special Point Mini Stun &gt; every
/// other combat reaction</c>. Encoding it here rather than as scattered caller checks is what makes
/// it reviewable.
/// </summary>
public sealed class SpecialPointReactionPrioritySmokeTests
{
    const CharacterAnimationMode Reaction = CharacterAnimationMode.SpecialReaction;
    const CharacterAnimationTransitionReason ReactionReason =
        CharacterAnimationTransitionReason.SpecialReactionOverride;

    // ---- The reaction may start over lower-priority modes --------------------------------------

    [TestCase(CharacterAnimationMode.Locomotion)]
    [TestCase(CharacterAnimationMode.Crawl)]
    [TestCase(CharacterAnimationMode.Dash)]
    [TestCase(CharacterAnimationMode.FullBodyReload)]
    [TestCase(CharacterAnimationMode.Melee)]
    [TestCase(CharacterAnimationMode.Skill)]
    [TestCase(CharacterAnimationMode.Utility)]
    [TestCase(CharacterAnimationMode.Knockback)]
    [TestCase(CharacterAnimationMode.SoftStatus)]
    [TestCase(CharacterAnimationMode.HardStatus)]
    public void ReactionStartsOverEveryOtherCombatReaction(CharacterAnimationMode current)
    {
        Assert.That(
            CharacterAnimationTransitionPolicy.CanStart(current, Reaction, ReactionReason),
            Is.True,
            $"The Special Point reaction must be able to replace {current}.");
    }

    // ---- The reaction yields to what outranks it ------------------------------------------------

    [Test]
    public void ReactionCannotInterruptAnActiveChainAttack()
    {
        Assert.That(
            CharacterAnimationTransitionPolicy.CanStart(CharacterAnimationMode.Chain, Reaction, ReactionReason),
            Is.False);
    }

    [Test]
    public void ReactionCannotStartOverACinematic()
    {
        Assert.That(
            CharacterAnimationTransitionPolicy.CanStart(CharacterAnimationMode.StageIntro, Reaction, ReactionReason),
            Is.False);
    }

    [Test]
    public void ReactionCannotStartOnADeadOrDownedCharacter()
    {
        Assert.That(
            CharacterAnimationTransitionPolicy.CanStart(CharacterAnimationMode.Dead, Reaction, ReactionReason),
            Is.False);

        Assert.That(
            CharacterAnimationTransitionPolicy.CanStart(
                CharacterAnimationMode.Locomotion,
                Reaction,
                ReactionReason,
                isDowned: true),
            Is.False);
    }

    // ---- Once running, only life state and cinematics may cut it short -------------------------

    [TestCase(CharacterAnimationMode.Dead, CharacterAnimationTransitionReason.LifeStateOverride, true)]
    [TestCase(CharacterAnimationMode.StageIntro, CharacterAnimationTransitionReason.CinematicOverride, true)]
    [TestCase(CharacterAnimationMode.Chain, CharacterAnimationTransitionReason.NormalCommand, false)]
    [TestCase(CharacterAnimationMode.Skill, CharacterAnimationTransitionReason.NormalCommand, false)]
    [TestCase(CharacterAnimationMode.Dash, CharacterAnimationTransitionReason.NormalCommand, false)]
    [TestCase(CharacterAnimationMode.Melee, CharacterAnimationTransitionReason.NormalCommand, false)]
    [TestCase(CharacterAnimationMode.Knockback, CharacterAnimationTransitionReason.NormalCommand, false)]
    [TestCase(CharacterAnimationMode.HardStatus, CharacterAnimationTransitionReason.StatusOverride, false)]
    [TestCase(CharacterAnimationMode.SoftStatus, CharacterAnimationTransitionReason.StatusOverride, false)]
    public void RunningReactionAdmitsOnlyLifeStateAndCinematic(
        CharacterAnimationMode requested,
        CharacterAnimationTransitionReason reason,
        bool expected)
    {
        Assert.That(
            CharacterAnimationTransitionPolicy.CanStart(Reaction, requested, reason),
            Is.EqualTo(expected));
    }

    [Test]
    public void ExternalControlLossDoesNotOutrankTheReaction()
    {
        // Ordinary stagger takeover is an ExternalControlLoss, and it is exactly what the reaction
        // must not lose to — the Mini Stun is the stagger reaction.
        Assert.That(
            CharacterAnimationTransitionPolicy.CanStart(
                Reaction,
                CharacterAnimationMode.HardStatus,
                CharacterAnimationTransitionReason.ExternalControlLoss),
            Is.False);
    }

    // ---- Nothing about the existing table moved -------------------------------------------------

    [Test]
    public void GenericStatusStillCannotInterruptAChain()
    {
        Assert.That(
            CharacterAnimationTransitionPolicy.CanStart(
                CharacterAnimationMode.Chain,
                CharacterAnimationMode.HardStatus,
                CharacterAnimationTransitionReason.StatusOverride),
            Is.False);
    }

    [Test]
    public void DeathIsStillAbsorbing()
    {
        Assert.That(
            CharacterAnimationTransitionPolicy.CanStart(
                CharacterAnimationMode.Dead,
                CharacterAnimationMode.Dead,
                CharacterAnimationTransitionReason.LifeStateOverride),
            Is.True);
    }

    // ---- Root motion shape ----------------------------------------------------------------------

    [Test]
    public void YawAndVerticalTranslationCanBeRequestedIndependently()
    {
        RootMotionPolicy active = RootMotionPolicy.Inactive.WithActive(true);

        RootMotionPolicy reaction = active.WithShape(
            planarOnly: false,
            applyYaw: true,
            ignoreCharacterCollision: false,
            environmentSafe: true);

        Assert.That(reaction.PlanarOnly, Is.False, "Vertical translation must survive.");
        Assert.That(reaction.ApplyYaw, Is.True, "Animation yaw must still be applied.");
        Assert.That(reaction.EnvironmentSafe, Is.True);
    }

    [Test]
    public void LegacyShapeHelperStillCouplesYawToPlanarOnly()
    {
        RootMotionPolicy active = RootMotionPolicy.Inactive.WithActive(true);

        RootMotionPolicy planar = active.WithShape(planarOnly: true, ignoreCharacterCollision: false);
        Assert.That(planar.ApplyYaw, Is.True);
        Assert.That(planar.EnvironmentSafe, Is.False, "Existing playbacks must not silently become swept.");

        RootMotionPolicy full = active.WithShape(planarOnly: false, ignoreCharacterCollision: false);
        Assert.That(full.ApplyYaw, Is.False);
        Assert.That(full.EnvironmentSafe, Is.False);
    }
}
#endif
