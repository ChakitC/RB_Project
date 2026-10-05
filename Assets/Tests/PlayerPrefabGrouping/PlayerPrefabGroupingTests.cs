using System;
using NUnit.Framework;

public sealed class PlayerPrefabGroupingTests
{
    object fixture;
    Type type;
    [SetUp] public void Setup()
    {
        type = Type.GetType("PlayerPrefabGroupingFixture, Assembly-CSharp-Editor", true);
        fixture = Activator.CreateInstance(type);
    }
    [TearDown] public void Cleanup() => type.GetMethod("Cleanup").Invoke(fixture, null);
    [TestCase("LayoutAndInputBindings")]
    [TestCase("ContextResolvesWithoutSerializedPeerBindings")]
    [TestCase("PickupResolvesRootAndNestedBodies")]
    [TestCase("StatsIncludeSiblingProvidersAndExcludeOtherActors")]
    [TestCase("GuardUsesActorPoseWithOffsetHost")]
    public void Grouping(string scenario) => type.GetMethod(scenario).Invoke(fixture, null);
}
