using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine.TestTools;

public sealed class BasementSupplyPlayModeTests
{
    object fixture;
    [SetUp] public void Setup() => fixture = Activator.CreateInstance(Type.GetType("BasementSupplyTestFixture, Assembly-CSharp-Editor", true));
    [TearDown] public void Cleanup() => Invoke("Cleanup");
    object Invoke(string name)
    {
        try { return fixture.GetType().GetMethod(name).Invoke(fixture, null); }
        catch (TargetInvocationException e) { throw e.InnerException; }
    }
    [UnityTest] public IEnumerator SelectionFilteringAndReopen() => (IEnumerator)Invoke(nameof(SelectionFilteringAndReopen));
    [UnityTest] public IEnumerator EmptyMissingArtworkAndLongDescription() => (IEnumerator)Invoke(nameof(EmptyMissingArtworkAndLongDescription));
}
