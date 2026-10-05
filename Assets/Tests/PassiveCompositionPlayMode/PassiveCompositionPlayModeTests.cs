using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine.TestTools;

public sealed class PassiveCompositionPlayModeTests
{
    object fixture;
    Type type;
    [UnityTest]
    public IEnumerator RomaAcrossFramesAndTimedBuffRelease()
    {
        type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("PassiveCompositionTestFixture")).First(t => t != null);
        fixture = Activator.CreateInstance(type);
        type.GetMethod("Setup").Invoke(fixture, null);
        yield return (IEnumerator)type.GetMethod("RuntimeRomaAcrossFrames").Invoke(fixture, null);
    }
    [TearDown]
    public void Cleanup()
    {
        if (fixture != null) type.GetMethod("Cleanup").Invoke(fixture, null);
        fixture = null;
    }
}
