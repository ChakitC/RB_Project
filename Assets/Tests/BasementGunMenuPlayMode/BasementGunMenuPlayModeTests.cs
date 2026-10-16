using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine.TestTools;

public sealed class BasementGunMenuPlayModeTests
{
    object fixture;
    [SetUp] public void Setup() => fixture = Activator.CreateInstance(Type.GetType("BasementGunMenuTestFixture, Assembly-CSharp-Editor", true));
    [TearDown] public void Cleanup() => Invoke("Cleanup");
    object Invoke(string name)
    {
        try { return fixture.GetType().GetMethod(name).Invoke(fixture, null); }
        catch (TargetInvocationException e) { throw e.InnerException; }
    }
    [UnityTest] public IEnumerator EquipmentOpenBackRestoreAndMutualExclusion() => (IEnumerator)Invoke(nameof(EquipmentOpenBackRestoreAndMutualExclusion));
    [UnityTest] public IEnumerator InterruptedOpenAndLostTargetRestoreState() => (IEnumerator)Invoke(nameof(InterruptedOpenAndLostTargetRestoreState));
    [UnityTest] public IEnumerator PerCharacterMenuCameraAndLiveBlendTiming() => (IEnumerator)Invoke(nameof(PerCharacterMenuCameraAndLiveBlendTiming));
    [UnityTest] public IEnumerator WeaponsEquipPersistAndRejectOtherOwner() => (IEnumerator)Invoke(nameof(WeaponsEquipPersistAndRejectOtherOwner));
}
