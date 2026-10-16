using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine.TestTools;

public sealed class BasementAnimationPlayModeTests
{
    object fixture;
    [SetUp] public void Setup() => fixture = Activator.CreateInstance(Type.GetType("BasementAnimationTestFixture, Assembly-CSharp-Editor", true));
    [TearDown] public void Cleanup() => Invoke("Cleanup");
    object Invoke(string name)
    {
        try { return fixture.GetType().GetMethod(name).Invoke(fixture, null); }
        catch (TargetInvocationException e) { throw e.InnerException; }
    }
    [UnityTest] public IEnumerator AllEightPlayAndReactivate() => (IEnumerator)Invoke(nameof(AllEightPlayAndReactivate));
    [UnityTest] public IEnumerator NonLoopPickupHoldsPoseInPlayMode() => (IEnumerator)Invoke(nameof(NonLoopPickupHoldsPoseInPlayMode));
    [UnityTest] public IEnumerator ModelSwapReleasesWeaponsBeforeDestroy() => (IEnumerator)Invoke(nameof(ModelSwapReleasesWeaponsBeforeDestroy));
}
