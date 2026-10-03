using System;
using System.Collections;
using UnityEngine.TestTools;

public sealed class AITargetSensorRuntimeTests
{
    [UnityTest] public IEnumerator PerceptionAndFiring() => Run("RuntimePerceptionAndFiring");
    [UnityTest] public IEnumerator ClockAndScanCosts() => Run("RuntimeClockAndScanCosts");
    [UnityTest] public IEnumerator AuthoredPrefabs() => Run("RuntimeAuthoredPrefabs");

    IEnumerator Run(string name)
    {
        Type type = Type.GetType("AITargetSensorRuntimeChecks, Assembly-CSharp-Editor", true);
        object fixture = Activator.CreateInstance(type);
        try { yield return (IEnumerator)type.GetMethod(name).Invoke(fixture, null); }
        finally { type.GetMethod("TearDown").Invoke(fixture, null); }
    }
}
