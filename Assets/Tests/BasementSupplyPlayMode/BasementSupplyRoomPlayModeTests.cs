using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine.TestTools;

public sealed class BasementSupplyRoomPlayModeTests
{
    object fixture;
    [SetUp] public void Setup() => fixture = Activator.CreateInstance(Type.GetType("BasementSupplyRoomTestFixture, Assembly-CSharp-Editor", true));
    [TearDown] public void Cleanup() => Invoke("Cleanup");
    object Invoke(string name)
    {
        try { return fixture.GetType().GetMethod(name).Invoke(fixture, null); }
        catch(TargetInvocationException e) { throw e.InnerException; }
    }
    [UnityTest] public IEnumerator ShopFadeAndBackRestore() => (IEnumerator)Invoke(nameof(ShopFadeAndBackRestore));
    [UnityTest] public IEnumerator InterruptedVisitsRestoreExactState() => (IEnumerator)Invoke(nameof(InterruptedVisitsRestoreExactState));
    [UnityTest] public IEnumerator InvalidBindingsAndMapViewRejectEntry() => (IEnumerator)Invoke(nameof(InvalidBindingsAndMapViewRejectEntry));
}
