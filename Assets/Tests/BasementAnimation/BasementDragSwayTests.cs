using System;
using System.Reflection;
using NUnit.Framework;

public sealed class BasementDragSwayTests
{
    object fixture;
    [SetUp] public void Setup() => fixture = Activator.CreateInstance(Type.GetType("BasementDragSwayTestFixture, Assembly-CSharp-Editor", true));
    [TearDown] public void Cleanup() => Invoke("Cleanup");
    void Invoke(string name)
    {
        try { fixture.GetType().GetMethod(name).Invoke(fixture, null); }
        catch (TargetInvocationException e) { throw e.InnerException; }
    }
    [Test] public void DirectionReverseClampAndSettle() => Invoke(nameof(DirectionReverseClampAndSettle));
    [Test] public void SizeFrameRateAndResolution() => Invoke(nameof(SizeFrameRateAndResolution));
    [Test] public void AnimatedAnchorScaleFocusAndReset() => Invoke(nameof(AnimatedAnchorScaleFocusAndReset));
    [Test] public void AllEightRigsSizesWeaponsAndLifecycle() => Invoke(nameof(AllEightRigsSizesWeaponsAndLifecycle));
    [Test] public void MissingRigStillAllowsPresentation() => Invoke(nameof(MissingRigStillAllowsPresentation));
}
