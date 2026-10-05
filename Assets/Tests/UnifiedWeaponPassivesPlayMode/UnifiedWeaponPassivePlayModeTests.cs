using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;

public sealed class UnifiedWeaponPassivePlayModeTests
{
    object fixture;
    Type type;
    [UnityTest] public IEnumerator ProductionRomaCommitsWeaponPassivesOnlyAfterPrimarySpawn()
    {
        type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("UnifiedWeaponPassivePlayModeFixture")).First(t => t != null);
        fixture = Activator.CreateInstance(type);
        yield return (IEnumerator)type.GetMethod("Run").Invoke(fixture, null);
    }
    [TearDown] public void Cleanup() { if (fixture != null) type.GetMethod("Cleanup").Invoke(fixture, null); fixture = null; }
}
