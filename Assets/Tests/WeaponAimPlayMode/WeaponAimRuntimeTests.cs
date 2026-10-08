using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine.TestTools;

public sealed class WeaponAimRuntimeTests
{
    object fixture;
    [SetUp] public void Setup() => fixture = Activator.CreateInstance(Type.GetType("WeaponAimTestFixture, Assembly-CSharp-Editor", true));
    [TearDown] public void Cleanup() => fixture.GetType().GetMethod("Cleanup").Invoke(fixture, null);
    IEnumerator Run(string name)
    {
        IEnumerator run;
        try { run = (IEnumerator)fixture.GetType().GetMethod(name).Invoke(fixture, null); }
        catch (TargetInvocationException e) { throw e.InnerException; }
        while (run.MoveNext()) yield return run.Current;
    }
    [UnityTest] public IEnumerator PlayerSwitchAndPool() => Run(nameof(PlayerSwitchAndPool));
    [UnityTest] public IEnumerator AllySwitchAndPool() => Run(nameof(AllySwitchAndPool));
    [UnityTest] public IEnumerator EnemySwitchAndPool() => Run(nameof(EnemySwitchAndPool));
    [UnityTest] public IEnumerator TurretSwitchAndPool() => Run(nameof(TurretSwitchAndPool));
}
