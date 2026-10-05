using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

public sealed class PlayerPrefabGroupingPlayModeTests
{
    object fixture;
    Type type;
    [UnityTest] public IEnumerator ProductionPlayerBindingsAndVisibility()
    {
        type = Type.GetType("PlayerPrefabGroupingFixture, Assembly-CSharp-Editor", true);
        fixture = Activator.CreateInstance(type);
        yield return (IEnumerator)type.GetMethod("RuntimeBindingsAndVisibility").Invoke(fixture, null);
    }
    [TearDown] public void Cleanup() { if (fixture != null) type.GetMethod("Cleanup").Invoke(fixture, null); fixture = null; }
}
