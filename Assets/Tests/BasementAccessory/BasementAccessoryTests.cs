using System;
using System.Reflection;
using NUnit.Framework;

public sealed class BasementAccessoryTests
{
    object fixture;
    [SetUp] public void Setup()
    {
        fixture=Activator.CreateInstance(Type.GetType("BasementAccessoryTestFixture, Assembly-CSharp-Editor",true));
        Invoke("Setup");
    }
    [TearDown] public void Cleanup() => Invoke("Cleanup");
    void Invoke(string name)
    {
        try { fixture.GetType().GetMethod(name).Invoke(fixture,null); }
        catch(TargetInvocationException e) { throw e.InnerException; }
    }
    [Test] public void EquipReplaceAndPersistAllFiveSlots() => Invoke(nameof(EquipReplaceAndPersistAllFiveSlots));
    [Test] public void RejectUniqueDuplicateAndOtherOwnerWithoutChangingAssignments() => Invoke(nameof(RejectUniqueDuplicateAndOtherOwnerWithoutChangingAssignments));
    [Test] public void PrefabAndSceneBindingsAreComplete() => Invoke(nameof(PrefabAndSceneBindingsAreComplete));
}
