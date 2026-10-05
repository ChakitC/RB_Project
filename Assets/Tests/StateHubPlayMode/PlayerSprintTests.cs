using System;
using System.Collections;
using UnityEngine.TestTools;

public sealed class PlayerSprintTests
{
    [UnityTest] public IEnumerator Tap() => Run("Tap");
    [UnityTest] public IEnumerator FailedDash() => Run("FailedDash");
    [UnityTest] public IEnumerator CanceledDash() => Run("CanceledDash");
    [UnityTest] public IEnumerator Direction() => Run("Direction");
    [UnityTest] public IEnumerator NestedPeers() => Run("NestedPeers");
    [UnityTest] public IEnumerator RotationLock() => Run("RotationLock");
    [UnityTest] public IEnumerator YawWrap() => Run("YawWrap");
    [UnityTest] public IEnumerator Wall() => Run("Wall");
    [UnityTest] public IEnumerator AngledWall() => Run("AngledWall");
    [UnityTest] public IEnumerator Fire() => Run("Fire");
    [UnityTest] public IEnumerator Suspend() => Run("Suspend");
    [UnityTest] public IEnumerator Ui() => Run("Ui");
    [UnityTest] public IEnumerator Stun() => Run("Stun");
    [UnityTest] public IEnumerator Disable() => Run("Disable");
    [UnityTest] public IEnumerator Down() => Run("Down");
    [UnityTest] public IEnumerator Dead() => Run("Dead");
    [UnityTest] public IEnumerator Release() => Run("Release");
    [UnityTest] public IEnumerator RomaPrefab()
    {
        var type = Type.GetType("PlayerSprintTestFixture, Assembly-CSharp-Editor", true);
        var fixture = Activator.CreateInstance(type);
        try { yield return (IEnumerator)type.GetMethod("RomaPrefab").Invoke(fixture, null); }
        finally { type.GetMethod("TearDown").Invoke(fixture, null); }
    }

    IEnumerator Run(string scenario)
    {
        var type = Type.GetType("PlayerSprintTestFixture, Assembly-CSharp-Editor", true);
        var fixture = Activator.CreateInstance(type);
        try { yield return (IEnumerator)type.GetMethod("Run").Invoke(fixture, new object[] { scenario }); }
        finally { type.GetMethod("TearDown").Invoke(fixture, null); }
    }
}
