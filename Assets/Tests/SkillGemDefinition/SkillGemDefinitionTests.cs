using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Animancer;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Gameplay uses Assembly-CSharp, so this dedicated test assembly accesses it through reflection.
public sealed class SkillGemDefinitionTests
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    readonly List<Object> created = new List<Object>();
    readonly List<string> assetPaths = new List<string>();
    static Type T(string name) => Type.GetType(name + ", Assembly-CSharp", true);
    static object Get(object owner, string name) => owner.GetType().GetField(name, Flags)?.GetValue(owner)
        ?? owner.GetType().GetProperty(name, Flags)?.GetValue(owner);
    static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Flags).SetValue(owner, value);
    static object Call(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, Flags).Invoke(owner, args);
    U Track<U>(U value) where U : Object { created.Add(value); return value; }
    ScriptableObject Asset(string type) => Track(ScriptableObject.CreateInstance(T(type)));

    [TearDown]
    public void Cleanup()
    {
        foreach (var path in assetPaths) AssetDatabase.DeleteAsset(path);
        assetPaths.Clear();
        for (int i = created.Count - 1; i >= 0; i--)
            if (created[i] != null) Object.DestroyImmediate(created[i]);
        created.Clear();
    }

    ScriptableObject Skill(string payloadType = "HealAreaSkillPayloadDef")
    {
        var skill = Asset("SkillGemDefinition");
        Set(skill, "skillId", "test.review." + Guid.NewGuid().ToString("N"));
        Set(skill, "payload", Asset(payloadType));
        var clip = Track(new AnimationClip());
        clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0, 0, 1, 0));
        Set(skill, "skillClip", new ClipTransition { Clip = clip });
        Set(skill, "castPointNormalized", .4f);
        return skill;
    }

    ScriptableObject Combo(params ScriptableObject[] skills)
    {
        var combo = Asset("SkillGemDefinition");
        Set(combo, "skillId", "test.combo." + Guid.NewGuid().ToString("N"));
        Set(combo, "comboEnabled", true);
        var steps = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(T("SkillComboStep")));
        for (int i = 0; i < skills.Length; i++)
            steps.Add(Activator.CreateInstance(T("SkillComboStep"), skills[i], "step-" + i, new Vector2(.5f, .8f), false));
        Set(combo, "comboSteps", steps);
        return combo;
    }

    static List<string> Issues(object skill, string method = "CollectRequiredTimelineValidationIssues")
    {
        var issues = new List<string>();
        Call(skill, method, issues);
        return issues;
    }

    void Marker(object skill, string name, float time)
    {
        var marker = Track(ScriptableObject.CreateInstance<StringAsset>());
        marker.name = name;
        var transition = (ClipTransition)Get(skill, "skillClip");
        transition.SerializedEvents ??= new AnimancerEvent.Sequence.Serializable();
        transition.SerializedEvents.AddEvent(time, name: marker);
    }

    [Test]
    public void ComboRejectsBrokenStepAndIdentifiesItsIndex()
    {
        var combo = Combo(Skill(), Skill("ProjectileSkillPayloadDef"));
        object[] args = { null };
        Assert.That(Call(combo, "ValidateMelee", args), Is.False);
        StringAssert.Contains("Step 2 (step-1)", (string)args[0]);
        StringAssert.Contains("projectile prefab", (string)args[0]);
        Assert.That(Get(combo, "ConfigStatusLabel"), Is.EqualTo("Error"));
    }

    [Test]
    public void ComboRejectsMissingRequiredMarker()
    {
        var combo = Combo(Skill("TauntSkillPayloadDef"));
        object[] args = { null };
        Assert.That(Call(combo, "ValidateMelee", args), Is.False);
        StringAssert.Contains("TauntApply", (string)args[0]);
    }

    [Test]
    public void ValidComboReportsComboModeWithoutRootPayload()
    {
        var combo = Combo(Skill(), Skill());
        Assert.That(Call(combo, "ValidateMelee", new object[] { null }), Is.True);
        Assert.That(Get(combo, "ConfigStatusLabel"), Is.EqualTo("Valid"));
        Assert.That(Get(combo, "PrimaryExecutionSourceLabel"), Is.EqualTo("Combo (2 steps)"));
        Assert.That(Call(combo, "GetResolvedRuntimeModeLabel"), Is.EqualTo("Combo (2 steps)"));
        Assert.That(Get(combo, "RuntimeSummaryLine"), Is.EqualTo("Combo (2 steps)"));
    }

    [Test]
    public void SwitchingModesRestoresNormalPayloadValidation()
    {
        var combo = Combo(Skill());
        Set(combo, "comboEnabled", false);
        Assert.That(Get(combo, "BlockingErrorSummary").ToString(), Does.Contain("Execution payload is required"));
        Set(combo, "payload", Asset("HealAreaSkillPayloadDef"));
        Assert.That(Call(combo, "GetResolvedRuntimeModeLabel"), Is.EqualTo("Payload -> Heal Area"));
        Set(combo, "comboEnabled", true);
        Assert.That(Get(combo, "BlockingErrorSummary").ToString(), Does.Contain("remove the root payload"));
    }

    [TestCase(.2f, true)]
    [TestCase(.4f, true)]
    [TestCase(.4001f, false)]
    [TestCase(.7f, false)]
    public void TauntRequiresMarkerStrictlyAfterCastPoint(float time, bool invalid)
    {
        var skill = Skill("TauntSkillPayloadDef");
        Marker(skill, "TauntApply", time);
        var issues = Issues(skill);
        Assert.That(issues.Count > 0, Is.EqualTo(invalid), string.Join("\n", issues));
        if (invalid) StringAssert.Contains("after Cast Point", string.Join("\n", issues));
    }

    [Test]
    public void CompositePropagatesTauntTimingRequirement()
    {
        var skill = Skill("CompositeSkillPayloadDef");
        object step = Activator.CreateInstance(T("PayloadStep"));
        Call(step, "SetPayload", Asset("TauntSkillPayloadDef"));
        Call(Get(skill, "payload"), "AddStep", step);
        Marker(skill, "TauntApply", .2f);
        Assert.That(Issues(skill), Has.Count.EqualTo(1));
        StringAssert.Contains("after Cast Point", Issues(skill)[0]);
    }

    [Test]
    public void EveryRepeatedTauntMarkerIsValidated()
    {
        var skill = Skill("TauntSkillPayloadDef");
        Marker(skill, "TauntApply", .7f);
        Marker(skill, "TauntApply", .2f);
        Assert.That(Issues(skill), Has.Count.EqualTo(1));
    }

    [TestCase(.2f, false, true)]
    [TestCase(.4f, false, true)]
    [TestCase(.6f, false, false)]
    [TestCase(.2f, true, true)]
    [TestCase(.4f, true, true)]
    [TestCase(.6f, true, false)]
    public void DeliveryListenerTimingIsValidatedThroughComposite(float time, bool composite, bool invalid)
    {
        var skill = Skill(composite ? "CompositeSkillPayloadDef" : "TargetedDeliverySkillPayloadDef");
        if (composite)
        {
            var step = Activator.CreateInstance(T("PayloadStep"));
            Call(step, "SetPayload", Asset("TargetedDeliverySkillPayloadDef"));
            Call(Get(skill, "payload"), "AddStep", step);
        }
        Marker(skill, "DeliveryRelease", time);
        Assert.That(Issues(skill).Count > 0, Is.EqualTo(invalid));
        var meleeIssues = new List<string>();
        Call(skill, "CollectTimelineValidationIssues", Kind("BasicMelee"), meleeIssues);
        Assert.That(meleeIssues, Is.Empty);
    }

    [Test]
    public void HitboxMarkersCanPrecedeCastPoint()
    {
        var skill = Skill("PrefabHitboxSkillPayloadDef");
        var serialized = new SerializedObject((Object)Get(skill, "payload"));
        serialized.FindProperty("steps").arraySize = 1;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        Marker(skill, "HitStart", 0f);
        Marker(skill, "HitEnd", .2f);
        Assert.That(Issues(skill), Is.Empty);
    }

    [Test]
    public void EditorValidatorAcceptsComboRootAndChecksChildOwnership()
    {
        var combo = Combo(Skill());
        var steps = (IList)Get(combo, "comboSteps");
        var child = (ScriptableObject)Get(steps[0], "executionSkill");
        string path = "Assets/Tests/SkillGemDefinition/Ownership-" + Guid.NewGuid().ToString("N") + ".asset";
        assetPaths.Add(path);
        AssetDatabase.CreateAsset(child, path);
        var issues = new List<string>();
        var validator = Type.GetType("SkillPayloadValidationTool, Assembly-CSharp-Editor", true);
        validator.GetMethod("ValidateSkill", Flags).Invoke(null, new object[] { combo, issues });
        Assert.That(issues, Has.Some.Contains("Step 1 (step-0): Referenced payload"));
        Assert.That(Get(combo, "BlockingErrorSummary").ToString(), Does.Contain(issues[0]));
        Assert.That(issues, Has.None.Contains("Execution payload is missing"));
    }

    object Kind(string name) => Enum.Parse(T("SkillExecutionKind"), name);

    [TestCase(0f)]
    [TestCase(.2f)]
    [TestCase(.4f)]
    public void BasicMeleeArmsTauntBeforePlayback(float time)
    {
        var skill = Skill("TauntSkillPayloadDef");
        Marker(skill, "TauntApply", time);
        var issues = new List<string>();
        Call(skill, "CollectTimelineValidationIssues", Kind("BasicMelee"), issues);
        Assert.That(issues, Is.Empty);
        Assert.That(Issues(skill), Has.Count.EqualTo(1), "Standard skills still arm at Cast Point.");
    }

    [TestCase(-.1f)]
    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    public void BasicMeleeRejectsUnreachableListenerTime(float time) =>
        Assert.That(Call(Skill(), "IsTimelineListenerReadyAt", Kind("BasicMelee"), time), Is.False);

    [Test]
    public void InspectorPreviewUsesExplicitExecutionKindWithoutChangingCastPoint()
    {
        var skill = Skill("TauntSkillPayloadDef");
        var status = Asset("StatusEffectDef");
        Set(status, "effectId", "test.preview.taunt");
        Set(status, "separatePerSource", true);
        ((IList)Get(status, "tags")).Add("Taunt");
        Set(Get(Get(skill, "payload"), "tauntStatus"), "effect", status);
        Marker(skill, "TauntApply", .2f);
        Assert.That(Get(skill, "ConfigStatusLabel"), Is.EqualTo("Error"));
        skill.GetType().GetProperty("ValidationExecutionKind").SetValue(skill, Kind("BasicMelee"));
        Assert.That(Get(skill, "ConfigStatusLabel"), Is.EqualTo("Valid"));
        Assert.That(Get(skill, "castPointNormalized"), Is.EqualTo(.4f));
    }

    [Test]
    public void ComboCannotExposeLeftoverRootExecutionOrPolicies()
    {
        var combo = Combo(Skill());
        var payload = Asset("TargetedDeliverySkillPayloadDef");
        Set(payload, "targetStandOffDistanceAtCastPoint", 2f);
        Set(combo, "payload", payload);
        var serialized = new SerializedObject(payload);
        serialized.FindProperty("helperFacingMode").enumValueIndex = 1;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        object[] args = { null };
        Assert.That(Call(combo, "TryGetExecution", args), Is.False);
        Assert.That(args[0], Is.Null);
        Assert.That(Call(combo, "TryGetCombo", args), Is.True);
        Assert.That(Get(combo, "HelperFacingMode").ToString(), Is.EqualTo("KeepCurrentFacing"));
        Assert.That(Call(combo, "TryGetTargetPlacement", new object[] { null }), Is.False);
        Assert.That(Call(combo, "ValidateExecution", Kind("StandardSkill"), null), Is.False);
    }

    object AddDelivery(object root, float distance, string gate = null)
    {
        var child = Asset("TargetedDeliverySkillPayloadDef");
        Set(child, "targetStandOffDistanceAtCastPoint", distance);
        var step = Activator.CreateInstance(T("PayloadStep"));
        Call(step, "SetPayload", child);
        step.GetType().GetProperty("RequiredUpgradeId").SetValue(step, gate);
        Call(root, "AddStep", step);
        return step;
    }

    [TestCase(false)]
    [TestCase(true)]
    public void CompositeNeverChoosesPlacementByChildOrder(bool reversed)
    {
        var skill = Skill("CompositeSkillPayloadDef");
        var root = Get(skill, "payload");
        AddDelivery(root, reversed ? 3f : 1f);
        AddDelivery(root, reversed ? 1f : 3f);
        Assert.That(Call(skill, "TryGetTargetPlacement", new object[] { null }), Is.False);
        Assert.That(Issues(root, "CollectValidationIssues"), Has.Some.Contains("Multiple composite payloads"));
        Set(root, "overrideTargetPlacement", true);
        Set(root, "targetStandOffDistanceAtCastPoint", 2f);
        object[] args = { null };
        Assert.That(Call(skill, "TryGetTargetPlacement", args), Is.True);
        Assert.That(Get(args[0], "TargetStandOffDistanceAtCastPoint"), Is.EqualTo(2f));
        Assert.That(Issues(root, "CollectValidationIssues"), Has.None.Contains("Multiple composite payloads"));
    }

    [Test]
    public void GatedChildCannotMoveCasterUntilRootPlacementIsAuthored()
    {
        var skill = Skill("CompositeSkillPayloadDef");
        var root = Get(skill, "payload");
        AddDelivery(root, 2f, "locked.upgrade");
        Assert.That(Call(skill, "TryGetTargetPlacement", new object[] { null }), Is.False);
        Assert.That(Issues(root, "CollectValidationIssues"), Has.Some.Contains("gates target placement"));
    }

    [Test]
    public void MalformedCompositeCycleReportsErrorWithoutRecursing()
    {
        var skill = Skill("CompositeSkillPayloadDef");
        var root = Get(skill, "payload");
        var step = Activator.CreateInstance(T("PayloadStep"));
        Call(step, "SetPayload", root);
        Call(root, "AddStep", step);
        Assert.That(Issues(skill, "CollectExecutionValidationIssues"), Has.Some.Contains("another CompositeSkillPayloadDef"));
    }

    [TestCase(0f)]
    [TestCase(.2f)]
    public void BasicMeleeTauntReceivesActualTimelineEvent(float time) =>
        RunAnimationCheck("AssertBasicMeleeTauntReceivesTimelineEvent", time);

    [Test]
    public void InvalidDefinitionIsRejectedBeforeResourceReservation() =>
        RunAnimationCheck("AssertInvalidDefinitionRejectedBeforeReservation", false);

    [TestCase("NodeCentricPayloadValidatorSmokeTests", "Assets/_UnifiedValidationSmokeTests")]
    [TestCase("NodeAbilityAuthoringServiceSmokeTests", "Assets/_NodeAbilityAuthoringSmokeTests")]
    public void AuthoringPreservesPoliciesAndSharesDefinitionValidation(string type, string temporaryFolder)
    {
        Assert.That(AssetDatabase.IsValidFolder(temporaryFolder), Is.False,
            "Refuse to replace an existing smoke-test folder.");
        var suite = Type.GetType(type + ", Assembly-CSharp-Editor", true);
        try { suite.GetMethod("RunFromCommandLine").Invoke(null, null); }
        catch (TargetInvocationException exception)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
        }
    }

    [Test]
    public void StructuredIssuesKeepCodeSeveritySkillAndComboStep()
    {
        var child = Skill("ProjectileSkillPayloadDef");
        var combo = Combo(child);
        var issues = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(T("SkillValidationIssue")));
        Call(combo, "CollectValidationIssues", issues, Kind("BasicMelee"), false);
        Assert.That(issues.Count, Is.GreaterThan(0));
        Assert.That(Get(issues[0], "Code"), Is.EqualTo("execution.payload"));
        Assert.That(Get(issues[0], "Severity").ToString(), Is.EqualTo("Error"));
        Assert.That(Get(issues[0], "Skill"), Is.SameAs(child));
        Assert.That(Get(issues[0], "StepId"), Is.EqualTo("step-0"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void InvalidComboStepIsRejectedBeforePlayback(bool missingMarker) =>
        RunAnimationCheck("AssertInvalidComboStepRejectedBeforePlayback", missingMarker);

    [TestCase(false)]
    [TestCase(true)]
    public void TauntAfterCastPointReceivesTimelineEvent(bool crossBothInOneFrame) =>
        RunAnimationCheck("AssertTauntAfterCastPointReceivesTimelineEvent", crossBothInOneFrame);

    static void RunAnimationCheck(string method, object argument)
    {
        // Reuse the existing typed animation/character rig without moving gameplay assemblies.
        var fixtureType = Type.GetType("MeleeSkillIntegrationTests, Assembly-CSharp-Editor", true);
        object fixture = Activator.CreateInstance(fixtureType);
        try { fixtureType.GetMethod(method).Invoke(fixture, new object[] { argument }); }
        catch (TargetInvocationException exception)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
        }
        finally { fixtureType.GetMethod("Cleanup").Invoke(fixture, null); }
    }
}
