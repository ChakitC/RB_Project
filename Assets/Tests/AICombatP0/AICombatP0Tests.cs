using System;
using NUnit.Framework;

public sealed class AICombatP0Tests
{
    object fixture;
    [SetUp] public void Setup() => fixture = Activator.CreateInstance(Type.GetType("AICombatP0Checks, Assembly-CSharp-Editor", true));
    [TearDown] public void Cleanup() => Invoke("TearDown");
    void Invoke(string name)
    {
        try { fixture.GetType().GetMethod(name).Invoke(fixture, null); }
        catch (System.Reflection.TargetInvocationException e) { throw e.InnerException; }
    }
    [Test] public void ConditionalsArePureAndReevaluateIdentically() => Invoke(nameof(ConditionalsArePureAndReevaluateIdentically));
    [Test] public void RangeAndAmmoAdmission() => Invoke(nameof(RangeAndAmmoAdmission));
    [Test] public void PureSightQueryUsesObstacleAndOwnerRules() => Invoke(nameof(PureSightQueryUsesObstacleAndOwnerRules));
    [Test] public void ObservationConsumptionAndPooledLife() => Invoke(nameof(ObservationConsumptionAndPooledLife));
    [Test] public void StateQuotaAndTargetReplacement() => Invoke(nameof(StateQuotaAndTargetReplacement));
    [Test] public void TargetEligibilityRejectsDeadDownAndUntargetable() => Invoke(nameof(TargetEligibilityRejectsDeadDownAndUntargetable));
    [Test] public void GraphRoundTripAndPrefabBindings() => Invoke(nameof(GraphRoundTripAndPrefabBindings));
    [Test] public void ContextCacheRestartAndPool() => Invoke(nameof(ContextCacheRestartAndPool));
    [Test] public void ContextFailuresNeverDiscoverOwner() => Invoke(nameof(ContextFailuresNeverDiscoverOwner));
    [Test] public void ContextEventsRebindWithoutDuplicates() => Invoke(nameof(ContextEventsRebindWithoutDuplicates));
    [Test] public void ContextValidatorRejectsUnsafeGraphs() => Invoke(nameof(ContextValidatorRejectsUnsafeGraphs));
    [Test] public void ContextSpecialPointBindings() => Invoke(nameof(ContextSpecialPointBindings));
    [Test] public void ContextInitializerOrderSurvivesGraphSave() => Invoke(nameof(ContextInitializerOrderSurvivesGraphSave));
}
