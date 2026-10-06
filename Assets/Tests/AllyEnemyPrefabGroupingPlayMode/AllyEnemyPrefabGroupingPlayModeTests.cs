using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

public sealed class AllyEnemyPrefabGroupingPlayModeTests
{
    object fixture;
    Type type;
    [UnityTest] public IEnumerator ProductionBindingsAndReactivation()
    {
        type = Type.GetType("AllyEnemyPrefabGroupingFixture, Assembly-CSharp-Editor", true);
        fixture = Activator.CreateInstance(type);
        yield return (IEnumerator)type.GetMethod("RuntimeProductionBindingsAndReactivation").Invoke(fixture, null);
    }
    [TearDown] public void Cleanup()
    {
        if (fixture != null) type.GetMethod("Cleanup").Invoke(fixture, null);
        fixture = null;
    }
}
