using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Animancer;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

// Typed access to Assembly-CSharp. Test entry points live in SkillGemDefinition.Tests.
public static class SkillArchitectureTestHarness
{
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

    public static void ExceptionCase(string phase, bool animated)
    {
        var rig = new MeleeSkillIntegrationTests();
        var energyRig = new SkillCastTransactionSmokeTests();
        var caster = (CharacteContext)typeof(MeleeSkillIntegrationTests).GetMethod("MakeRig", Hidden)
            .Invoke(rig, new object[] { false });
        var user = (SkillUserSystem)typeof(SkillCastTransactionSmokeTests).GetMethod("CreateEnergyUser", Hidden)
            .Invoke(energyRig, new object[] { 100f });
        var skill = ScriptableObject.CreateInstance<SkillGemDefinition>();
        var payload = ScriptableObject.CreateInstance<SkillArchitectureProbePayload>();
        var clip = new AnimationClip();
        clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0, 0, 1, 0));
        skill.payload = payload;
        skill.skillClip = new ClipTransition { Clip = clip };
        skill.baseManaCost = 25f;
        skill.baseCooldown = 10f;
        var instance = new SkillInstance { def = skill };
        var orchestrator = new SkillCastOrchestrator(caster);
        int requestId = 0;
        try
        {
            Action fail = () => throw new InvalidOperationException("architecture probe failure");
            bool notified = false;
            bool failureNotified = false;
            orchestrator.CastExecutionFailed += (_, _) => failureNotified = true;
            if (phase == "payload" || phase == "direct") payload.ThrowOnExecute = true;
            if (phase == "energy")
            {
                bool once = false;
                user.EnergyChanged += (_, _) => { if (!once) { once = true; fail(); } };
                user.EnergyChanged += (_, _) => notified = true;
            }
            if (phase == "released") orchestrator.CastReleased += _ => fail();
            if (phase == "committed")
            {
                orchestrator.CastCommitted += _ => fail();
                orchestrator.CastCommitted += _ => notified = true;
            }
            SkillCastStartResult result = default;
            if (phase == "direct") Assert.Throws<InvalidOperationException>(() => instance.Cast(user));
            else
            {
                LogAssert.Expect(LogType.Exception, new Regex("architecture probe failure"));
                result = orchestrator.TryStartCast(new SkillCastRequest(instance, user, caster.AnimDriver,
                    onStarted: phase == "started" ? fail : null, useAnimationDriver: animated));
            }
            requestId = result.RequestId;
            if (animated && result.Started)
            {
                Assert.That(user.CurrentEnergy, Is.EqualTo(75f));
                var animancer = caster.GetComponent<AnimancerComponent>();
                animancer.Evaluate(0f);
                animancer.Evaluate(.6f);
                animancer.Evaluate(.01f);
            }
            instance.TryGetChargeStatus(user, out var charges);
            Assert.That(orchestrator.HasPendingCast, Is.False);
            if (phase == "committed" || phase == "energy")
            {
                Assert.That(notified, Is.True, "One failing observer must not suppress the next.");
                Assert.That(payload.Executions, Is.EqualTo(1));
                Assert.That(charges.Available, Is.EqualTo(0));
                Assert.That(user.StoredEnergy, Is.EqualTo(75f));
            }
            else
            {
                if (phase == "payload" || (animated && phase == "released"))
                    Assert.That(failureNotified, Is.True, "A failed released cast must notify its owner.");
                Assert.That(charges.Available, Is.EqualTo(charges.Max));
                Assert.That(user.CurrentEnergy, Is.EqualTo(100f));
                Assert.That(user.StoredEnergy, Is.EqualTo(100f));
                Assert.That(instance.CanCast(user), Is.True);
            }
        }
        finally
        {
            orchestrator.CancelPendingCast();
            caster.AnimDriver.CancelSkillCastRequest(requestId);
            rig.Cleanup();
            energyRig.TearDown();
            Object.DestroyImmediate(skill);
            Object.DestroyImmediate(payload);
            Object.DestroyImmediate(clip);
        }
    }

    public static void SnapshotCase(bool replace)
    {
        var host = new GameObject("snapshot-probe");
        var user = host.AddComponent<ProbeSkillUserBehaviour>();
        var skill = ScriptableObject.CreateInstance<SkillGemDefinition>();
        var root = ScriptableObject.CreateInstance<CompositeSkillPayloadDef>();
        var always = ScriptableObject.CreateInstance<SkillArchitectureProbePayload>();
        var gated = ScriptableObject.CreateInstance<SkillArchitectureProbePayload>();
        SkillCastReservation reservation = null;
        try
        {
            var first = new PayloadStep(); first.SetPayload(always); root.AddStep(first);
            var second = new PayloadStep { RequiredUpgradeId = "late" }; second.SetPayload(gated); root.AddStep(second);
            skill.payload = root;
            skill.baseDamage = 10f;
            var instance = new SkillInstance { def = skill, upgradeSnapshot = new SkillUpgradeStatSnapshot() };
            Assert.That(instance.TryReserveCast(user, false, true, out reservation), Is.True);
            if (replace) instance.upgradeSnapshot = new SkillUpgradeStatSnapshot();
            var node = new SkillUpgradeNodeData { grantedUpgradeIds = new List<string> { "late" } };
            instance.upgradeSnapshot.AddNode(node);
            reservation.Stats.damage = 999f;
            Assert.That(instance.ExecuteReserved(reservation, null, 1, out _), Is.True);
            Assert.That(always.Executions, Is.EqualTo(1));
            Assert.That(gated.Executions, Is.Zero);
            Assert.That(always.LastContext.SkillStats.damage, Is.EqualTo(10f));
            Assert.That(always.LastContext.HasUpgrade("late"), Is.False);
            Assert.Throws<InvalidOperationException>(() => reservation.Upgrades.AddNode(node));
            Assert.That(reservation.Upgrades.UpgradeIds, Is.Not.InstanceOf<HashSet<string>>());
        }
        finally
        {
            reservation?.Release();
            Object.DestroyImmediate(skill); Object.DestroyImmediate(root);
            Object.DestroyImmediate(always); Object.DestroyImmediate(gated); Object.DestroyImmediate(host);
        }
    }

    public static void UsageCase(bool dual, bool profileBinding)
    {
        string folder = "Assets/Tests/SkillGemDefinition/Usage_" + Guid.NewGuid().ToString("N");
        AssetDatabase.CreateFolder("Assets/Tests/SkillGemDefinition", folder.Substring(folder.LastIndexOf('/') + 1));
        try
        {
            var skill = ScriptableObject.CreateInstance<SkillGemDefinition>();
            skill.skillId = "test.usage." + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateAsset(skill, folder + "/Step.asset");
            var payload = ScriptableObject.CreateInstance<TauntSkillPayloadDef>();
            AssetDatabase.AddObjectToAsset(payload, skill); skill.payload = payload;
            var status = ScriptableObject.CreateInstance<StatusEffectDef>();
            status.effectId = "test.usage.taunt"; status.tags.Add(StatusEffectTags.Taunt); status.separatePerSource = true;
            AssetDatabase.AddObjectToAsset(status, skill);
            var serialized = new SerializedObject(payload);
            serialized.FindProperty("tauntStatus.effect").objectReferenceValue = status;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            var clip = new AnimationClip();
            clip.SetCurve("", typeof(Transform), "localPosition.x", AnimationCurve.Linear(0, 0, 1, 0));
            AssetDatabase.AddObjectToAsset(clip, skill);
            var marker = ScriptableObject.CreateInstance<StringAsset>(); marker.name = "TauntApply";
            AssetDatabase.AddObjectToAsset(marker, skill);
            skill.skillClip = new ClipTransition { Clip = clip, SerializedEvents = new AnimancerEvent.Sequence.Serializable() };
            skill.skillClip.SerializedEvents.AddEvent(0f, name: marker);
            if (profileBinding)
            {
                var profile = ScriptableObject.CreateInstance<CharacterAnimProfileSO>();
                profile.lightMeleeSkill = skill;
                AssetDatabase.CreateAsset(profile, folder + "/Profile.asset");
            }
            else
            {
                var combo = ScriptableObject.CreateInstance<SkillGemDefinition>(); combo.comboEnabled = true;
                combo.skillId = "test.combo." + Guid.NewGuid().ToString("N");
                typeof(SkillGemDefinition).GetField("comboSteps", Hidden).SetValue(combo,
                    new List<SkillComboStep> { new SkillComboStep(skill, "main", Vector2.zero, false) });
                AssetDatabase.CreateAsset(combo, folder + "/Combo.asset");
            }
            if (dual)
            {
                var stats = ScriptableObject.CreateInstance<CharacterStats>(); stats.chainAttackSkill = skill;
                AssetDatabase.CreateAsset(stats, folder + "/Character.asset");
            }
            EditorUtility.SetDirty(skill); AssetDatabase.SaveAssetIfDirty(skill);
            SkillExecutionUsageIndex.Rebuild();
            var kinds = SkillExecutionUsageIndex.GetKinds(skill);
            Assert.That(kinds.Count, Is.EqualTo(dual ? 2 : 1));
            Assert.That(kinds, Does.Contain(SkillExecutionKind.BasicMelee));
            var issues = new List<SkillValidationIssue>();
            SkillExecutionUsageIndex.CollectIssues(skill, issues);
            Assert.That(issues.Exists(x => x.Severity == SkillValidationSeverity.Error), Is.EqualTo(dual));
            if (dual) Assert.That(issues.Exists(x => x.Location.Contains("StandardSkill") && x.Code == "execution.timeline"), Is.True);
            var tree = ScriptableObject.CreateInstance<SkillUpgradeTreeDefinition>();
            try
            {
                var treeIssues = NodeCentricPayloadValidator.Validate(tree, new List<SkillDefinitionBase> { skill });
                Assert.That(treeIssues.Exists(x => x.Severity == SkillUpgradeValidationSeverity.Error), Is.EqualTo(dual));
            }
            finally { Object.DestroyImmediate(tree); }
        }
        finally { AssetDatabase.DeleteAsset(folder); SkillExecutionUsageIndex.Invalidate(); }
    }
}
