#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Animancer;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Table-driven coverage for animation transition priority.
///
/// The important test here is <see cref="ObservedTransitionMatrixMatchesTheAuthoredTable"/>: it
/// drives a real Brain through every (current mode, requested mode) pair and compares the result
/// against a literal table. That table was captured from the implementation *before*
/// <see cref="CharacterAnimationTransitionPolicy"/> existed, so it is the contract, not a
/// restatement of the policy. Changing a cell means deliberately changing gameplay priority.
///
/// The observed answer is <c>policy AND state checks</c>. Three cells are worth knowing about
/// because they are asymmetries rather than obvious rules:
///
/// - FullBodyReload -> Chain is blocked while FullBodyReload -> Skill is allowed. A skill calls
///   StopReloadAction() first, which clears the reload's exit lock; chain playback does not.
/// - Skill -> Skill is blocked but Skill -> Utility is allowed. Only skill admission consults
///   IsShootBlockingPlaybackActive, which is what lets a chain warp-out interrupt a skill.
/// - Knockback blocks hard status poses too, not just soft ones.
/// </summary>
public sealed class CharacterAnimationTransitionPolicyTests
{
    const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;

    static readonly string[] FromModes =
    {
        "Locomotion", "Crawl", "Dash", "FullBodyReload", "Skill", "Utility",
        "Chain", "Knockback", "SoftStatus", "HardStatus", "StageIntro", "Dead",
    };

    static readonly string[] ToModes =
    {
        "Dash", "FullBodyReload", "Skill", "Utility", "Chain",
        "Knockback", "SoftStatus", "HardStatus", "StageIntro", "Dead",
    };

    /// <summary>Rows follow <see cref="FromModes"/>, columns follow <see cref="ToModes"/>.</summary>
    static readonly Dictionary<string, string> ExpectedMatrix = new()
    {
        //                        Dash FullBodyReload Skill Utility Chain Knockback SoftStatus HardStatus StageIntro Dead
        { "Locomotion",     "YYYYYYYYYY" },
        { "Crawl",          "YnnnnnYYYY" },
        { "Dash",           "YYYYYYnYYY" },
        { "FullBodyReload", "YYYYnYnYYY" },
        { "Skill",          "YYnYYYnYYY" },
        { "Utility",        "YYnYYYnYYY" },
        { "Chain",          "nnnnnnnnYY" },
        { "Knockback",      "YYYYYYnnYY" },
        { "SoftStatus",     "YYYYYYYYYY" },
        { "HardStatus",     "YYYYYYYYYY" },
        { "StageIntro",     "YYYYYYnYYY" },
        { "Dead",           "nnnnnnnnnY" },
    };

    readonly List<Object> createdObjects = new();
    TimeSlowManager preExistingTimeSlowManager;

    [SetUp]
    public void SetUp()
    {
        preExistingTimeSlowManager = Object.FindAnyObjectByType<TimeSlowManager>();
    }

    [TearDown]
    public void TearDown()
    {
        for (int i = createdObjects.Count - 1; i >= 0; i--)
        {
            if (createdObjects[i] != null)
                Object.DestroyImmediate(createdObjects[i]);
        }

        createdObjects.Clear();

        if (preExistingTimeSlowManager == null)
        {
            TimeSlowManager spawned = Object.FindAnyObjectByType<TimeSlowManager>();
            if (spawned != null)
                Object.DestroyImmediate(spawned.gameObject);
        }

        preExistingTimeSlowManager = null;
    }

    // ---- End-to-end contract ------------------------------------------------------------------

    [Test]
    public void ObservedTransitionMatrixMatchesTheAuthoredTable()
    {
        var failures = new StringBuilder();

        foreach (string from in FromModes)
        {
            string expectedRow = ExpectedMatrix[from];

            for (int i = 0; i < ToModes.Length; i++)
            {
                CharacterAnimBrain brain = CreateBrain();

                Assert.That(Drive(brain, from, 900), Is.True,
                    $"Could not put the Brain into '{from}'; the fixture is wrong, not the policy.");

                bool allowed = Drive(brain, ToModes[i], 500);
                bool expected = expectedRow[i] == 'Y';

                if (allowed != expected)
                {
                    failures
                        .Append(from).Append(" -> ").Append(ToModes[i])
                        .Append(": expected ").Append(expected ? "allowed" : "blocked")
                        .Append(", got ").Append(allowed ? "allowed" : "blocked")
                        .AppendLine();
                }
            }
        }

        Assert.That(failures.ToString(), Is.Empty,
            "Animation transition priority changed. If that was deliberate, update the table above "
            + "in the same commit so the new priority is the reviewed artefact:\n" + failures);
    }

    // ---- Pure policy --------------------------------------------------------------------------

    [Test]
    public void DeathAbsorbsEveryOtherMode()
    {
        foreach (CharacterAnimationMode requested in System.Enum.GetValues(typeof(CharacterAnimationMode)))
        {
            bool allowed = CharacterAnimationTransitionPolicy.CanStart(
                CharacterAnimationMode.Dead,
                requested,
                CharacterAnimationTransitionReason.LifeStateOverride);

            Assert.That(allowed, Is.EqualTo(requested == CharacterAnimationMode.Dead),
                $"Dead -> {requested} must only be allowed for Dead itself.");
        }
    }

    [Test]
    public void ChainYieldsOnlyToLifeStateCinematicAndControlLoss()
    {
        Assert.That(Allows(CharacterAnimationTransitionReason.NormalCommand), Is.False);
        Assert.That(Allows(CharacterAnimationTransitionReason.StatusOverride), Is.False,
            "A stun must not cut a chain attack short.");
        Assert.That(Allows(CharacterAnimationTransitionReason.LifeStateOverride), Is.True);
        Assert.That(Allows(CharacterAnimationTransitionReason.CinematicOverride), Is.True);
        Assert.That(Allows(CharacterAnimationTransitionReason.ExternalControlLoss), Is.True);

        static bool Allows(CharacterAnimationTransitionReason reason) =>
            CharacterAnimationTransitionPolicy.AllowsExternalCommand(CharacterAnimationMode.Chain, reason);
    }

    [Test]
    public void SoftStatusOnlyDecoratesAnOtherwiseIdleCharacter()
    {
        Assert.That(CanStartStatus(CharacterAnimationMode.Locomotion, soft: true), Is.True);
        Assert.That(CanStartStatus(CharacterAnimationMode.Crawl, soft: true), Is.True);
        Assert.That(CanStartStatus(CharacterAnimationMode.SoftStatus, soft: true), Is.True);
        Assert.That(CanStartStatus(CharacterAnimationMode.HardStatus, soft: true), Is.True);

        Assert.That(CanStartStatus(CharacterAnimationMode.Dash, soft: true), Is.False);
        Assert.That(CanStartStatus(CharacterAnimationMode.Skill, soft: true), Is.False);
        Assert.That(CanStartStatus(CharacterAnimationMode.StageIntro, soft: true), Is.False);

        // A hard pose overrides all of those.
        Assert.That(CanStartStatus(CharacterAnimationMode.Dash, soft: false), Is.True);
        Assert.That(CanStartStatus(CharacterAnimationMode.Skill, soft: false), Is.True);
        Assert.That(CanStartStatus(CharacterAnimationMode.StageIntro, soft: false), Is.True);

        // Except against knockback, which drives the body itself.
        Assert.That(CanStartStatus(CharacterAnimationMode.Knockback, soft: false), Is.False);
        Assert.That(CanStartStatus(CharacterAnimationMode.Knockback, soft: true), Is.False);

        static bool CanStartStatus(CharacterAnimationMode current, bool soft) =>
            CharacterAnimationTransitionPolicy.CanStart(
                current,
                soft ? CharacterAnimationMode.SoftStatus : CharacterAnimationMode.HardStatus,
                CharacterAnimationTransitionReason.StatusOverride);
    }

    [Test]
    public void DownedBlocksTheFiveModesThatEverCheckedForIt()
    {
        var blocked = new[]
        {
            CharacterAnimationMode.Skill,
            CharacterAnimationMode.Utility,
            CharacterAnimationMode.Chain,
            CharacterAnimationMode.FullBodyReload,
            CharacterAnimationMode.Knockback,
        };

        foreach (CharacterAnimationMode mode in blocked)
            Assert.That(CanStart(mode), Is.False, $"Downed -> {mode} must be refused.");

        // Dash and melee never carried a downed check; keeping them allowed is deliberate.
        Assert.That(CanStart(CharacterAnimationMode.Dash), Is.True);
        Assert.That(CanStart(CharacterAnimationMode.Melee), Is.True);

        static bool CanStart(CharacterAnimationMode requested) =>
            CharacterAnimationTransitionPolicy.CanStart(
                CharacterAnimationMode.Crawl,
                requested,
                CharacterAnimationTransitionReason.NormalCommand,
                isDowned: true);
    }

    [Test]
    public void SkillIsRefusedOnTopOfAnyRequestOwningPlaybackButUtilityIsNot()
    {
        foreach (CharacterAnimationMode current in new[]
                 {
                     CharacterAnimationMode.Skill,
                     CharacterAnimationMode.Utility,
                 })
        {
            Assert.That(CanStart(current, CharacterAnimationMode.Skill), Is.False);
            Assert.That(CanStart(current, CharacterAnimationMode.Utility), Is.True,
                "A warp is allowed to interrupt a skill; that is what the chain warp-out relies on.");
        }

        static bool CanStart(CharacterAnimationMode current, CharacterAnimationMode requested) =>
            CharacterAnimationTransitionPolicy.CanStart(
                current,
                requested,
                CharacterAnimationTransitionReason.NormalCommand);
    }

    // ---- Fixture ------------------------------------------------------------------------------

    CharacterAnimBrain CreateBrain()
    {
        var go = new GameObject("TransitionMatrixRig");
        createdObjects.Add(go);

        Animator animator = go.AddComponent<Animator>();
        var animancer = go.AddComponent<AnimancerComponent>();
        animancer.Animator = animator;

        var brain = go.AddComponent<CharacterAnimBrain>();
        typeof(CharacterAnimBrain).GetField("animancer", Hidden).SetValue(brain, animancer);
        brain.SetAnimProfileOverride(BuildFullProfile());

        Tick(brain);
        return brain;
    }

    /// <summary>
    /// Puts the Brain into <paramref name="mode"/>, or asks it to. Returns whether the mode
    /// actually took locomotion, which is the single observable both halves of the matrix need.
    /// </summary>
    bool Drive(CharacterAnimBrain brain, string mode, int requestId)
    {
        switch (mode)
        {
            case "Locomotion":
                return true;
            case "Crawl":
                brain.SetDowned(true);
                return StateName(brain) == "LocomotionState_Crawl";
            case "Dash":
                brain.PlayDash(0.2f, Vector2.up);
                return StateName(brain) == "Locomotion_Dash";
            case "FullBodyReload":
                brain.PlayReload(0.5f);
                return StateName(brain) == "Locomotion_Reload";
            case "Skill":
                return brain.TryPlaySkill(requestId, null, 0f);
            case "Utility":
                return brain.TryPlayUtilityWarpOut(requestId);
            case "Chain":
                return brain.TryPlayChainCutscene(requestId, MakeTransition("chainCut" + requestId));
            case "Knockback":
                return brain.PlayKnockback(new KnockbackData(Vector3.forward, 2f, 0.3f, Vector3.zero));
            case "SoftStatus":
                SetStatusIntent(brain, StatusLocomotionPose.Root);
                return StateName(brain) == "Locomotion_StatusEffect";
            case "HardStatus":
                SetStatusIntent(brain, StatusLocomotionPose.Stun);
                return StateName(brain) == "Locomotion_StatusEffect";
            case "StageIntro":
                return brain.TryPlayStageIntro();
            case "Dead":
                brain.PlayDead();
                return StateName(brain) == "Locomotion_Dead";
        }

        Assert.Fail($"Unknown mode '{mode}'.");
        return false;
    }

    static void Tick(CharacterAnimBrain brain) =>
        typeof(CharacterAnimBrain).GetMethod("Update", Hidden).Invoke(brain, null);

    static void SetStatusIntent(CharacterAnimBrain brain, StatusLocomotionPose pose) =>
        typeof(CharacterAnimBrain)
            .GetMethod("SetStatusLocomotionIntent", Hidden)
            .Invoke(brain, new object[] { pose });

    static string StateName(CharacterAnimBrain brain)
    {
        object stateMachine = typeof(CharacterAnimBrain).GetField("locomotionSM", Hidden).GetValue(brain);
        object current = stateMachine.GetType().GetProperty("CurrentState").GetValue(stateMachine);
        return current == null ? "null" : current.GetType().Name;
    }

    CharacterAnimProfileSO BuildFullProfile()
    {
        var profile = ScriptableObject.CreateInstance<CharacterAnimProfileSO>();
        profile.name = "TransitionMatrixProfile";
        createdObjects.Add(profile);

        profile.locomotionDirectionalClips.idle = MakeClip("idle");
        profile.locomotionDirectionalClips.forward = MakeClip("forward");
        profile.locomotionDirectionalClips.backward = MakeClip("backward");
        profile.locomotionDirectionalClips.left = MakeClip("left");
        profile.locomotionDirectionalClips.right = MakeClip("right");
        profile.crawlMixer = profile.ResolveLocomotionMixer();
        profile.crawling = MakeTransition("crawling");

        profile.skillClip = MakeTransition("skill");
        profile.utilityWarpOutClip = MakeTransition("warpOut");
        profile.utilityWarpOutCastPointNormalized = 0f;
        profile.utilityWarpInClip = MakeTransition("warpIn");
        profile.utilityWarpInCastPointNormalized = 0f;
        profile.stageIntroClip = MakeTransition("stageIntro");
        profile.stune = MakeTransition("stun");
        profile.root = MakeTransition("root");
        profile.knockback = MakeTransition("knockback");
        profile.dashF = MakeTransition("dashF");
        profile.dashB = MakeTransition("dashB");
        profile.dashL = MakeTransition("dashL");
        profile.dashR = MakeTransition("dashR");
        profile.reload = MakeTransition("reload");
        profile.reloadBodyMode = CharacterAnimProfileSO.ReloadBodyMode.FullBody;
        profile.dead = MakeTransition("dead");

        return profile;
    }

    ClipTransition MakeTransition(string clipName)
    {
        var transition = new ClipTransition();
        transition.Clip = MakeClip(clipName);
        return transition;
    }

    AnimationClip MakeClip(string clipName)
    {
        var clip = new AnimationClip { name = clipName };
        clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0f, 0f, 1f, 1f));
        createdObjects.Add(clip);
        return clip;
    }
}
#endif
