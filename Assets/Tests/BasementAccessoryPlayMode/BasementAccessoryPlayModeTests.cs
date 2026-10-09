using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine.TestTools;

public sealed class BasementAccessoryPlayModeTests
{
    object fixture;
    [SetUp] public void Setup()
    {
        fixture=Activator.CreateInstance(Type.GetType("BasementAccessoryTestFixture, Assembly-CSharp-Editor",true));
        Invoke("Setup");
    }
    [TearDown] public void Cleanup() => Invoke("Cleanup");
    object Invoke(string name)
    {
        try { return fixture.GetType().GetMethod(name).Invoke(fixture,null); }
        catch(TargetInvocationException e) { throw e.InnerException; }
    }
    [UnityTest] public IEnumerator OpenPickCancelEquipAndRestore() => (IEnumerator)Invoke(nameof(OpenPickCancelEquipAndRestore));
}
