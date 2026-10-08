using System;
using NUnit.Framework;

public sealed class VfxLifecycleTests
{
    [TestCase("ExplicitAndAutoLifetime")]
    [TestCase("ReuseResetsStateAndRejectsStaleLease")]
    [TestCase("SessionEndDetachesTailAndIsolatesActors")]
    [TestCase("DefaultSessionEndIsImmediateAndDebugSwitchWorks")]
    [TestCase("AuthoringRoundTripAndValidation")]
    [TestCase("AnimatedMeshUsesClipLifetimeAndCanReplay")]
    [TestCase("PreviewUsesExactDeadlineAndSessionPolicy")]
    [TestCase("ProductionCartoonFxReuse")]
    [TestCase("DetachedFollowPreservesOffsetsScaleAndStackChanges")]
    [TestCase("LoopReplacementAndExplicitStopOverrideSessionPolicy")]
    [TestCase("MigratedGameplayImpactUsesCentralClockAndSwitch")]
    public void Run(string method)
    {
        var type = Type.GetType("VfxLifecycleTestFixture, Assembly-CSharp-Editor", true);
        var fixture = Activator.CreateInstance(type);
        try
        {
            type.GetMethod("SetUp").Invoke(fixture, null);
            type.GetMethod(method).Invoke(fixture, null);
        }
        finally { type.GetMethod("TearDown").Invoke(fixture, null); }
    }
}
