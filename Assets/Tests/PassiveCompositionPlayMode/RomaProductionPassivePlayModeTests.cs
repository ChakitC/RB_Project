using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;

public sealed class RomaProductionPassivePlayModeTests
{
    object fixture;
    Type fixtureType;

    [UnityTest]
    public IEnumerator ProductionRomaLoadsStaminaMomentumFromCharacterSlots()
    {
        fixtureType = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("RomaProductionPassiveTestFixture")).First(t => t != null);
        fixture = Activator.CreateInstance(fixtureType);
        yield return (IEnumerator)fixtureType.GetMethod("Run").Invoke(fixture, null);
    }

    [TearDown]
    public void Cleanup()
    {
        if (fixture != null) fixtureType.GetMethod("Cleanup").Invoke(fixture, null);
        fixture = null;
    }
}
