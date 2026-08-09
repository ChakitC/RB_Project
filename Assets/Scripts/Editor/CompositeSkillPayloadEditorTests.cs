#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class CompositeSkillPayloadEditorTests
{
    const string TempFolder = "Assets/__CompositeSkillPayloadEditorTests";

    [SetUp]
    public void SetUp()
    {
        AssetDatabase.DeleteAsset(TempFolder);
        AssetDatabase.CreateFolder("Assets", "__CompositeSkillPayloadEditorTests");
    }

    [TearDown]
    public void TearDown()
    {
        Undo.ClearAll();
        AssetDatabase.DeleteAsset(TempFolder);
    }

    [Test]
    public void ValidatorAllowsMultipleReachableEmbeddedPayloads()
    {
        SkillGemDefinition skill = CreateCompositeSkill("ValidGraph.asset", out CompositeSkillPayloadDef composite);
        ApplyStatusSkillPayloadDef child = AddEmbeddedPayload<ApplyStatusSkillPayloadDef>(skill, "Status Child");
        AddPayloadStep(composite, child);
        Save(skill, composite, child);

        var issues = new List<string>();
        SkillPayloadValidationTool.ValidateSkill(skill, issues);

        Assert.That(issues.Any(issue => issue.Contains("not referenced")), Is.False, string.Join("\n", issues));
        Assert.That(issues.Any(issue => issue.Contains("not embedded")), Is.False, string.Join("\n", issues));
        Assert.That(issues.Any(issue => issue.Contains("exactly one")), Is.False, string.Join("\n", issues));
    }

    [Test]
    public void ValidatorRejectsOrphanExternalAndDuplicateChildren()
    {
        SkillGemDefinition skill = CreateCompositeSkill("InvalidGraph.asset", out CompositeSkillPayloadDef composite);
        ApplyStatusSkillPayloadDef shared = AddEmbeddedPayload<ApplyStatusSkillPayloadDef>(skill, "Shared Child");
        ApplyStatusSkillPayloadDef orphan = AddEmbeddedPayload<ApplyStatusSkillPayloadDef>(skill, "Orphan Child");
        AddPayloadStep(composite, shared);
        AddPayloadStep(composite, shared);

        var external = ScriptableObject.CreateInstance<ApplyStatusSkillPayloadDef>();
        external.name = "External Child";
        AssetDatabase.CreateAsset(external, $"{TempFolder}/External.asset");
        AddPayloadStep(composite, external);
        Save(skill, composite, shared, orphan);

        var issues = new List<string>();
        SkillPayloadValidationTool.ValidateSkill(skill, issues);

        Assert.That(issues.Any(issue => issue.Contains("unique embedded payload")), Is.True, string.Join("\n", issues));
        Assert.That(issues.Any(issue => issue.Contains("not embedded in the same asset")), Is.True, string.Join("\n", issues));
        Assert.That(issues.Any(issue => issue.Contains("Orphan Child") && issue.Contains("not referenced")), Is.True, string.Join("\n", issues));
    }

    [Test]
    public void ReplacingCompositeDestroysItsEmbeddedDescendantsOnly()
    {
        SkillGemDefinition skill = CreateCompositeSkill("Replace.asset", out CompositeSkillPayloadDef composite);
        ApplyStatusSkillPayloadDef child = AddEmbeddedPayload<ApplyStatusSkillPayloadDef>(skill, "Owned Child");
        AddPayloadStep(composite, child);
        Save(skill, composite, child);

        SkillPayloadDef replacement = SkillPayloadAssetUtility.ReplaceWithEmbedded(
            skill,
            typeof(SpawnPickupSkillPayloadDef),
            recordUndo: false);

        Assert.That(composite == null, Is.True);
        Assert.That(child == null, Is.True);
        Assert.That(skill.payload, Is.SameAs(replacement));
        Assert.That(SkillPayloadAssetUtility.GetEmbeddedPayloads(skill), Has.Count.EqualTo(1));
    }

    [Test]
    public void ReplacingCompositeCanBeUndoneWithItsChildren()
    {
        SkillGemDefinition skill = CreateCompositeSkill("UndoReplace.asset", out CompositeSkillPayloadDef composite);
        ApplyStatusSkillPayloadDef child = AddEmbeddedPayload<ApplyStatusSkillPayloadDef>(skill, "Undo Child");
        AddPayloadStep(composite, child);
        Save(skill, composite, child);

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        SkillPayloadAssetUtility.ReplaceWithEmbedded(skill, typeof(SpawnPickupSkillPayloadDef));
        Undo.FlushUndoRecordObjects();
        Undo.CollapseUndoOperations(undoGroup);
        Undo.PerformUndo();

        Assert.That(skill.payload, Is.TypeOf<CompositeSkillPayloadDef>());
        Assert.That(SkillPayloadAssetUtility.GetEmbeddedPayloads(skill).Any(payload => payload is ApplyStatusSkillPayloadDef), Is.True);
    }

    [Test]
    public void RemovingCompositeCanBeUndoneWithItsChildren()
    {
        SkillGemDefinition skill = CreateCompositeSkill("UndoRemove.asset", out CompositeSkillPayloadDef composite);
        ApplyStatusSkillPayloadDef child = AddEmbeddedPayload<ApplyStatusSkillPayloadDef>(skill, "Removed Child");
        AddPayloadStep(composite, child);
        Save(skill, composite, child);

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        SkillPayloadAssetUtility.RemoveExecution(skill);
        Undo.FlushUndoRecordObjects();
        Undo.CollapseUndoOperations(undoGroup);

        Assert.That(skill.payload, Is.Null);
        Assert.That(SkillPayloadAssetUtility.GetEmbeddedPayloads(skill), Is.Empty);

        Undo.PerformUndo();

        Assert.That(skill.payload, Is.TypeOf<CompositeSkillPayloadDef>());
        Assert.That(SkillPayloadAssetUtility.GetEmbeddedPayloads(skill).Any(payload => payload is ApplyStatusSkillPayloadDef), Is.True);
    }

    [Test]
    public void CreatingStepPayloadDoesNotSaveAnotherDirtyAsset()
    {
        SkillGemDefinition skill = CreateCompositeSkill("ScopedSave.asset", out CompositeSkillPayloadDef composite);
        AddPayloadStep(composite, null);
        Save(skill, composite);

        var unrelated = ScriptableObject.CreateInstance<SkillUpgradeTreeDefinition>();
        AssetDatabase.CreateAsset(unrelated, $"{TempFolder}/Unrelated.asset");
        unrelated.name = "Unsaved Change";
        EditorUtility.SetDirty(unrelated);
        Assert.That(EditorUtility.IsDirty(unrelated), Is.True);

        SkillPayloadAssetUtility.CreateEmbeddedStepPayload(
            skill,
            composite,
            0,
            typeof(ApplyStatusSkillPayloadDef),
            null);

        Assert.That(EditorUtility.IsDirty(unrelated), Is.True);
    }

    [Test]
    public void CompositeHitboxAddsTimelineAuthoringLane()
    {
        SkillGemDefinition skill = CreateCompositeSkill("HitboxLane.asset", out CompositeSkillPayloadDef composite);
        PrefabHitboxSkillPayloadDef hitbox = AddEmbeddedPayload<PrefabHitboxSkillPayloadDef>(skill, "Hitbox Child");
        AddPayloadStep(composite, hitbox);

        var source = new SkillVfxTimelineSource(skill);

        Assert.That(source.Lanes.Any(lane => lane.Label == "Hitbox"), Is.True);
    }

    [Test]
    public void MissingRequiredTimelineEventIsAnAuthoringError()
    {
        SkillGemDefinition skill = CreateSkill("MissingTimeline.asset");
        TauntSkillPayloadDef taunt = AddEmbeddedPayload<TauntSkillPayloadDef>(skill, "Taunt Root");
        skill.payload = taunt;

        var issues = new List<string>();
        skill.CollectRequiredTimelineValidationIssues(issues);

        Assert.That(issues.Any(issue => issue.Contains("TauntApply")), Is.True, string.Join("\n", issues));
    }

    [Test]
    public void AiresCompositeMatchesOwnershipGraphAndTimelineRequirements()
    {
        SkillGemDefinition skill = AssetDatabase.LoadAssetAtPath<SkillGemDefinition>(
            "Assets/Data/Skills/Aires/Aires_Skill_3.asset");
        Assert.That(skill, Is.Not.Null);
        Assert.That(skill.payload, Is.TypeOf<CompositeSkillPayloadDef>());

        List<SkillPayloadDef> embedded = SkillPayloadAssetUtility.GetEmbeddedPayloads(skill);
        HashSet<SkillPayloadDef> reachable = SkillPayloadAssetUtility.GetReachablePayloads(skill.payload);
        Assert.That(reachable.SetEquals(embedded), Is.True);

        var timelineIssues = new List<string>();
        skill.CollectRequiredTimelineValidationIssues(timelineIssues);
        Assert.That(timelineIssues, Is.Empty, string.Join("\n", timelineIssues));
    }

    static SkillGemDefinition CreateCompositeSkill(
        string fileName,
        out CompositeSkillPayloadDef composite)
    {
        SkillGemDefinition skill = CreateSkill(fileName);
        composite = AddEmbeddedPayload<CompositeSkillPayloadDef>(skill, "Composite Root");
        skill.payload = composite;
        EditorUtility.SetDirty(skill);
        return skill;
    }

    static SkillGemDefinition CreateSkill(string fileName)
    {
        var skill = ScriptableObject.CreateInstance<SkillGemDefinition>();
        skill.name = fileName.Replace(".asset", string.Empty);
        skill.skillId = $"test.{skill.name}";
        AssetDatabase.CreateAsset(skill, $"{TempFolder}/{fileName}");
        return skill;
    }

    static T AddEmbeddedPayload<T>(SkillGemDefinition skill, string name)
        where T : SkillPayloadDef
    {
        var payload = ScriptableObject.CreateInstance<T>();
        payload.name = name;
        AssetDatabase.AddObjectToAsset(payload, skill);
        EditorUtility.SetDirty(payload);
        EditorUtility.SetDirty(skill);
        return payload;
    }

    static void AddPayloadStep(CompositeSkillPayloadDef composite, SkillPayloadDef payload)
    {
        var step = new PayloadStep();
        step.SetPayload(payload);

        var serializedComposite = new SerializedObject(composite);
        serializedComposite.Update();
        SerializedProperty steps = serializedComposite.FindProperty("steps");
        int index = steps.arraySize;
        steps.InsertArrayElementAtIndex(index);
        steps.GetArrayElementAtIndex(index).managedReferenceValue = step;
        serializedComposite.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(composite);
    }

    static void Save(SkillGemDefinition skill, params Object[] objects)
    {
        EditorUtility.SetDirty(skill);
        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i] != null)
                EditorUtility.SetDirty(objects[i]);
        }

        AssetDatabase.SaveAssetIfDirty(skill);
    }
}
#endif
