using NUnit.Framework;
using UnityEngine;

public sealed class PartyFollowUpCoreSmokeTests
{
    [TestCase("Assets/Data/PartyFollowUp/Test/Test.Feno.EnteredBreakFollowUp.asset")]
    [TestCase("Assets/Data/PartyFollowUp/Test/Test.Aires.FollowUp.asset")]
    public void AuthoredAllyFollowUp_CanStartItsExecution(string path)
    {
        var followUp = UnityEditor.AssetDatabase.LoadAssetAtPath<PartyFollowUpSkillDef>(path);
        Assert.That(followUp, Is.Not.Null, path);
        Assert.That(followUp.executionSkill, Is.Not.Null, path);
        Assert.That(followUp.executionSkill.ValidateExecution(SkillExecutionKind.StandardSkill, out var error),
            Is.True, error);
    }

    [Test]
    public void PassiveEventType_SerializedValuesRemainStable()
    {
        Assert.That((int)PassiveEventType.None, Is.EqualTo(0));
        Assert.That((int)PassiveEventType.ShotFired, Is.EqualTo(1));
        Assert.That((int)PassiveEventType.Hit, Is.EqualTo(2));
        Assert.That((int)PassiveEventType.Kill, Is.EqualTo(3));
        Assert.That((int)PassiveEventType.TakeDamage, Is.EqualTo(4));
        Assert.That((int)PassiveEventType.DamagePrevented, Is.EqualTo(5));
        Assert.That((int)PassiveEventType.PerfectDodge, Is.EqualTo(6));
        Assert.That((int)PassiveEventType.Reload, Is.EqualTo(7));
        Assert.That((int)PassiveEventType.DashStarted, Is.EqualTo(8));
        Assert.That((int)PassiveEventType.DashEnded, Is.EqualTo(9));
        Assert.That((int)PassiveEventType.MovementDistanceReached, Is.EqualTo(10));
        Assert.That((int)PassiveEventType.StatusApplied, Is.EqualTo(11));
        Assert.That((int)PassiveEventType.StatusStackChanged, Is.EqualTo(12));
        Assert.That((int)PassiveEventType.FollowUpSkillCommitted, Is.EqualTo(13));
    }

    [Test]
    public void CombatEventFactory_AssignsFactAndPreservesChildLineage()
    {
        GameObject actor = new GameObject("FollowUpFactActor");
        try
        {
            CombatEventBus bus = actor.AddComponent<CombatEventBus>();
            PassiveEventContext root = bus.CreateExternalContext(PassiveEventType.Hit, target: actor);
            var provenance = new FollowUpExecutionProvenance(1, 2, 3, "followUp.test", 8f);
            PassiveEventContext child = bus.CreateChildContext(
                root,
                PassiveEventType.FollowUpSkillCommitted,
                followUpProvenance: provenance);

            Assert.That(root.FactId, Is.Not.EqualTo(0));
            Assert.That(root.ChainId, Is.Not.EqualTo(0));
            Assert.That(child.FactId, Is.Not.EqualTo(root.FactId));
            Assert.That(child.ChainId, Is.EqualTo(root.ChainId));
            Assert.That(child.Depth, Is.EqualTo(root.Depth + 1));
            Assert.That(child.FollowUpProvenance.ExecutionId, Is.EqualTo(3));
        }
        finally
        {
            Object.DestroyImmediate(actor);
        }
    }

    [TestCase(PartyFollowUpTriggerKind.FollowUpSkillCommitted, true)]
    [TestCase(PartyFollowUpTriggerKind.TargetHasStatus, true)]
    [TestCase(PartyFollowUpTriggerKind.EnteredBreak, true)]
    [TestCase(PartyFollowUpTriggerKind.FinalStrike, false)]
    [TestCase(PartyFollowUpTriggerKind.ArtsReaction, false)]
    [TestCase(PartyFollowUpTriggerKind.InflictionCountReached, false)]
    public void MvpTriggerSupport_IsExplicit(PartyFollowUpTriggerKind kind, bool expected)
    {
        Assert.That(PartyFollowUpTriggerEvaluator.IsSupportedMvpTrigger(kind), Is.EqualTo(expected));
    }

    [Test]
    public void FollowUpRuntimeEntry_IsCachedAndHasNoHelperSnapshot()
    {
        GameObject actor = new GameObject("FollowUpRuntimeEntryActor");
        PartyFollowUpSkillDef followUp = ScriptableObject.CreateInstance<PartyFollowUpSkillDef>();
        SkillGemDefinition skill = ScriptableObject.CreateInstance<SkillGemDefinition>();
        try
        {
            followUp.executionSkill = skill;
            CharacterSkillManager manager = actor.AddComponent<CharacterSkillManager>();
            Assert.That(manager.TryGetPartyFollowUpRuntimeEntry(followUp, out CharacterSkillEntry first), Is.True);
            first.runtimeSkill.upgradeSnapshot = new SkillUpgradeStatSnapshot();
            Assert.That(manager.TryGetPartyFollowUpRuntimeEntry(followUp, out CharacterSkillEntry second), Is.True);

            Assert.That(second, Is.SameAs(first));
            Assert.That(second.runtimeSkill.upgradeSnapshot, Is.Null);
        }
        finally
        {
            Object.DestroyImmediate(actor);
            Object.DestroyImmediate(followUp);
            Object.DestroyImmediate(skill);
        }
    }

    [Test]
    public void ChainAttackTestTarget_ReportsTheHitThatEnteredChainReady()
    {
        GameObject targetObject = new GameObject("FollowUpChainReadyTarget");
        GameObject attacker = new GameObject("FollowUpChainReadyAttacker");
        try
        {
            ChainAttackTestTarget target = targetObject.AddComponent<ChainAttackTestTarget>();
            target.ResetTarget();
            var damage = new DamageContext(
                1f,
                attacker,
                "followUp-test",
                "followUp-test-hit",
                CombatEventBus.NextChainId(),
                0,
                PassiveEventOrigin.External,
                stagger: new StaggerPayload(100f));

            DamageResult result = target.TakeDamage(in damage);

            Assert.That(result.StaggerApplied, Is.GreaterThan(0f));
            Assert.That(result.EnteredChainReady, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(targetObject);
            Object.DestroyImmediate(attacker);
        }
    }

    [Test]
    public void SkillTargetHandle_NonCharacterDamageableRemainsAnAlivePlacementAnchor()
    {
        GameObject targetObject = new GameObject("FollowUpNonCharacterTarget");
        try
        {
            ChainAttackTestTarget target = targetObject.AddComponent<ChainAttackTestTarget>();
            target.ResetTarget();

            SkillTargetHandle handle = SkillTargetHandle.ForNonCharacterDamageable(target);

            Assert.That(handle.WasAssigned, Is.True);
            Assert.That(handle.TryResolveEffectTarget(out _), Is.False,
                "A debug damageable must not masquerade as a character effect target.");
            Assert.That(
                handle.TryResolveAliveTarget(out Transform resolvedTransform, out AITargetIdentity identity),
                Is.True);
            Assert.That(resolvedTransform, Is.EqualTo(target.transform));
            Assert.That(identity, Is.EqualTo(target.TargetIdentity));
        }
        finally
        {
            Object.DestroyImmediate(targetObject);
        }
    }
}
