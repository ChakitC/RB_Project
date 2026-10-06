using System;
using NUnit.Framework;

public sealed class AllyEnemyPrefabGroupingTests
{
    object fixture;
    Type type;
    [SetUp] public void Setup()
    {
        type = Type.GetType("AllyEnemyPrefabGroupingFixture, Assembly-CSharp-Editor", true);
        fixture = Activator.CreateInstance(type);
    }
    [TearDown] public void Cleanup() => type.GetMethod("Cleanup").Invoke(fixture, null);
    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)] [TestCase(6)]
    public void Layout(int index) => type.GetMethod("Layout").Invoke(fixture, new object[]{index});
    [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)] [TestCase(6)]
    public void InactiveResolution(int index) => type.GetMethod("InactiveContextAndAgentResolution").Invoke(fixture, new object[]{index});
    [TestCase("RootAndSiblingEnemyLevelNotifyOnlyOwner")]
    [TestCase("DropUsesOwnerPoseAndVariantLevel")]
    [TestCase("HelperCachesNestedBehaviorTree")]
    [TestCase("AuthoringUsesExistingCombatGroup")]
    public void Regression(string scenario) => type.GetMethod(scenario).Invoke(fixture, null);
}
