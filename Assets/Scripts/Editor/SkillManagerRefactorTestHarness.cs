using System;
using System.Collections.Generic;
using System.Reflection;
using Animancer;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Typed fixture bridge for the dedicated SkillGemDefinition.Tests assembly.
public sealed class SkillManagerRefactorTestHarness : IDisposable
{
    const string PlaySmokeKey = "RB.SkillManagerRefactor.PlaySmoke";

    // SessionState carries this opt-in check across the Play Mode domain reload.
    [InitializeOnLoadMethod]
    static void ResumeScheduledPlaySmoke()
    {
        if (!SessionState.GetBool(PlaySmokeKey, false)) return;
        EditorApplication.playModeStateChanged -= OnPlaySmokeStateChanged;
        EditorApplication.playModeStateChanged += OnPlaySmokeStateChanged;
    }

    public static void SchedulePlayModeSmoke()
    {
        SessionState.SetBool(PlaySmokeKey, true);
        ResumeScheduledPlaySmoke();
    }

    static void OnPlaySmokeStateChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode) return;
        EditorApplication.playModeStateChanged -= OnPlaySmokeStateChanged;
        SessionState.SetBool(PlaySmokeKey, false);
        var results = new List<string>();
        void Check(string name, Action action)
        {
            var errors = new List<string>();
            void Capture(string message, string stack, LogType type)
            {
                if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
                    errors.Add(message + "\n" + stack);
            }
            Application.logMessageReceived += Capture;
            try
            {
                action();
                results.Add(errors.Count == 0 ? "PASS " + name : "FAIL " + name + ": " + string.Join("\n", errors));
            }
            catch (Exception exception) { results.Add("FAIL " + name + ": " + exception); }
            finally { Application.logMessageReceived -= Capture; }
        }
        try
        {
            Check("StandardSkill/shared charges", () => SharedCharges(false));
            Check("StandardSkill/nested modules", () => SharedCharges(true));
            Check("Helper variant wind-up", () => HelperVariant(true));
            Check("External Helper/Chain playback", () => ExternalPlayback(false));
            Check("Cancellation/stale signal", () => CancellationAndStaleSignals("slot", false));
            Check("BasicMelee combo", () =>
            {
                var melee = new MeleeSkillIntegrationTests();
                try { melee.PlayerPressAdvancesComboAndReusesHost(false); }
                finally { melee.Cleanup(); }
            });
        }
        finally
        {
            System.IO.File.WriteAllLines("../BuildArtifacts/SkillManagerRefactorPlayMode.txt", results);
            EditorApplication.delayCall += EditorApplication.ExitPlaymode;
        }
    }

    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    readonly MeleeSkillIntegrationTests rig = new();
    readonly List<Object> assets = new();
    readonly CharacteContext caster;
    CharacterSkillManager Manager => caster.SkillManager;

    public SkillManagerRefactorTestHarness(bool nested)
    {
        caster = (CharacteContext)typeof(MeleeSkillIntegrationTests).GetMethod("MakeRig", Hidden)
            .Invoke(rig, new object[] { nested });
    }

    public void Dispose()
    {
        Manager.TryCancelActiveCast(SkillCastCancelReason.Disabled);
        // Stop live playback while the fixture's animation profile still exists.
        caster.gameObject.SetActive(false);
        rig.Cleanup();
        foreach (var asset in assets) Object.DestroyImmediate(asset);
    }

    SkillGemDefinition MakeSkill()
    {
        var skill = ScriptableObject.CreateInstance<SkillGemDefinition>();
        var payload = ScriptableObject.CreateInstance<SkillArchitectureProbePayload>();
        var clip = new AnimationClip();
        clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0, 0, 1, 0));
        skill.payload = payload;
        skill.skillClip = new ClipTransition { Clip = clip };
        skill.baseCooldown = 60f;
        skill.baseManaCost = 0f;
        assets.Add(skill); assets.Add(payload); assets.Add(clip);
        return skill;
    }

    void SetSlots(params SkillGemDefinition[] skills)
    {
        var slots = Array.ConvertAll(skills, skill => new SkillSlot { skillAsset = skill });
        typeof(CharacterSkillManager).GetField("autonomousSlots", Hidden).SetValue(Manager, slots);
        Manager.RefreshCharacterOwnedLoadout();
    }

    void AdvanceToCastPoint()
    {
        var animancer = caster.GetComponent<AnimancerComponent>();
        animancer.Evaluate(0f); animancer.Evaluate(.6f); animancer.Evaluate(.01f);
    }

    void AssertCharges(int slot, int available)
    {
        Assert.That(Manager.TryGetSlotChargeStatus(slot, out var status), Is.True);
        Assert.That(status.Available, Is.EqualTo(available));
    }

    public static void SharedCharges(bool nested)
    {
        using var test = new SkillManagerRefactorTestHarness(nested);
        var skill = test.MakeSkill();
        test.SetSlots(skill, skill);
        var result = test.Manager.TryStartCastSlot(0);
        Assert.That(result.Kind, Is.EqualTo(SkillCastStartKind.WaitingForAnimation));
        test.AssertCharges(0, 0); test.AssertCharges(1, 0);
        test.AdvanceToCastPoint();
        Assert.That(((SkillArchitectureProbePayload)skill.payload).Executions, Is.EqualTo(1));
        test.Manager.ClearSlot(0);
        test.Manager.AssignSkillToSlot(0, skill);
        test.AssertCharges(0, 0); test.AssertCharges(1, 0);
        test.Manager.OnLoad(null);
        test.AssertCharges(0, 1); test.AssertCharges(1, 1);
    }

    public static void ExternalPlayback(bool nested)
    {
        using var test = new SkillManagerRefactorTestHarness(nested);
        var skill = test.MakeSkill();
        const int requestId = 9001;
        Assert.That(test.caster.AnimDriver.TryPlaySkill(requestId, skill, .4f), Is.True);
        var entry = new CharacterSkillEntry { skillAsset = skill };
        var result = test.Manager.TryStartExternalSkill(entry, "external-refactor",
            externalAnimationRequestId: requestId);
        Assert.That(result.Kind, Is.EqualTo(SkillCastStartKind.ImmediateSuccess));
        Assert.That(result.RequestId, Is.EqualTo(requestId));
        Assert.That(((SkillArchitectureProbePayload)skill.payload).Executions, Is.EqualTo(1));
        Assert.That(test.Manager.TryCancelActiveCast(SkillCastCancelReason.Disabled), Is.False);
        bool stillPlaying = (bool)typeof(CharacterAnimBrain).GetMethod("TryGetActiveSkillNormalizedTime", Hidden)
            .Invoke(test.caster.AnimBrain, new object[] { requestId, 0f });
        Assert.That(stillPlaying, Is.True,
            "The external owner must retain its playback after the cast commits.");
        test.caster.AnimDriver.CancelSkillCastRequest(requestId);
    }

    public static void CancellationAndStaleSignals(string reason, bool nested)
    {
        using var test = new SkillManagerRefactorTestHarness(nested);
        var skill = test.MakeSkill();
        test.SetSlots(skill);
        var first = test.Manager.TryStartCastSlot(0);
        Assert.That(first.Kind, Is.EqualTo(SkillCastStartKind.WaitingForAnimation));
        // Capture the old invocation list to simulate a signal already queued by playback.
        var signalField = typeof(CharacterAnimBrain).GetField("SkillCastMomentReached", Hidden);
        var staleSignal = (Action<int>)signalField.GetValue(test.caster.AnimBrain);
        Assert.That(staleSignal, Is.Not.Null);
        if (reason == "slot") test.Manager.ClearSlot(0);
        else typeof(CharacterSkillManager).GetMethod(reason == "disable" ? "OnDisable" : "OnCharacterDead", Hidden)
            .Invoke(test.Manager, null);
        Assert.That(test.Manager.TryGetActiveCast(out _), Is.False);
        test.Manager.AssignSkillToSlot(0, skill);
        test.AssertCharges(0, 1);
        var second = test.Manager.TryStartCastSlot(0);
        Assert.That(second.Kind, Is.EqualTo(SkillCastStartKind.WaitingForAnimation));
        Assert.That(second.RequestId, Is.Not.EqualTo(first.RequestId));
        staleSignal(first.RequestId);
        Assert.That(test.Manager.TryGetActiveCast(out var active), Is.True);
        Assert.That(active.RequestId, Is.EqualTo(second.RequestId));
        Assert.That(((SkillArchitectureProbePayload)skill.payload).Executions, Is.Zero);
        test.AdvanceToCastPoint();
        Assert.That(((SkillArchitectureProbePayload)skill.payload).Executions, Is.EqualTo(1));
    }

    public static void HelperVariant(bool sameDefinition)
    {
        using var test = new SkillManagerRefactorTestHarness(false);
        var firstSkill = test.MakeSkill();
        var secondSkill = sameDefinition ? firstSkill : test.MakeSkill();
        var first = ScriptableObject.CreateInstance<SkillHelperDef>();
        var second = ScriptableObject.CreateInstance<SkillHelperDef>();
        test.assets.Add(first); test.assets.Add(second);
        first.executionSkill = firstSkill; second.executionSkill = secondSkill;
        var slot = new HelperProcLoadoutSlot
        {
            slotId = "refactor.proc",
            options = new List<HelperProcLoadoutOption>
            {
                new() { optionId = "first", helperProc = first },
                new() { optionId = "second", helperProc = second },
            }
        };
        test.caster.baseStats.partyRole = CharacterPartyRole.Helper;
        test.caster.baseStats.helperProcSlots = new List<HelperProcLoadoutSlot> { slot };
        // Load the authoring change through the normal loadout initialization path.
        typeof(CharacterSkillManager).GetMethod("RebuildAllRuntimeSkills", Hidden).Invoke(test.Manager, null);
        string key = CharacterSkillLoadoutKeys.HelperProcSlotKey(slot, 0);
        Assert.That(test.Manager.TryStartHelperProcSkill(first, "refactor").Started, Is.True);
        Assert.That(test.Manager.TrySelectLoadoutOption(key, "second", false), Is.True);
        Assert.That(test.Manager.TryGetActiveCast(out _), Is.False);
        Assert.That(test.Manager.TryGetHelperProcChargeStatus(second, out var refunded), Is.True);
        Assert.That(refunded.Available, Is.EqualTo(1));
        Assert.That(((SkillArchitectureProbePayload)firstSkill.payload).Executions, Is.Zero);
        Assert.That(test.Manager.TrySelectLoadoutOption(key, "first", false), Is.True);
        Assert.That(test.Manager.TryStartHelperProcSkill(first, "refactor").Started, Is.True);
        test.AdvanceToCastPoint();
        Assert.That(((SkillArchitectureProbePayload)firstSkill.payload).Executions, Is.EqualTo(1));
        Assert.That(test.Manager.TrySelectLoadoutOption(key, "second", false), Is.True);
        Assert.That(test.Manager.TryGetHelperProcChargeStatus(second, out var alternate), Is.True);
        Assert.That(alternate.Available, Is.EqualTo(sameDefinition ? 0 : 1));
        Assert.That(test.Manager.TrySelectLoadoutOption(key, "first", false), Is.True);
        Assert.That(test.Manager.TryGetHelperProcChargeStatus(first, out var original), Is.True);
        Assert.That(original.Available, Is.Zero);
    }
}
