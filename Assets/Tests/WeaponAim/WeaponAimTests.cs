using System;
using System.Reflection;
using NUnit.Framework;

public sealed class WeaponAimTests
{
    object fixture;
    [SetUp] public void Setup() => fixture = Activator.CreateInstance(Type.GetType("WeaponAimTestFixture, Assembly-CSharp-Editor", true));
    [TearDown] public void Cleanup() => Invoke("Cleanup");
    void Invoke(string name)
    {
        try { fixture.GetType().GetMethod(name).Invoke(fixture, null); }
        catch (TargetInvocationException e) { throw e.InnerException; }
    }
    [Test] public void CloseAndElevatedTargetsConverge() => Invoke(nameof(CloseAndElevatedTargetsConverge));
    [Test] public void PlayerAndAISourcesUseSameSpread() => Invoke(nameof(PlayerAndAISourcesUseSameSpread));
    [Test] public void AIRejectsMissingTargetAndOldLifetime() => Invoke(nameof(AIRejectsMissingTargetAndOldLifetime));
    [Test] public void AuthoringBindingsAreComplete() => Invoke(nameof(AuthoringBindingsAreComplete));
    [Test] public void ExistingHurtboxAndCoverRegressions() => Invoke(nameof(ExistingHurtboxAndCoverRegressions));
    [Test] public void SpreadUsesMovementStabilityAndBloom() => Invoke(nameof(SpreadUsesMovementStabilityAndBloom));
}
