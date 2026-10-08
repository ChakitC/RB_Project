using System;
using NUnit.Framework;

public sealed class DefensiveBlockPlayerRecoilRunnerTests
{
    [TestCase("FollowsRequestedPlanarDistance")]
    [TestCase("SkillRecoilIsSharedAndCapturedPerGuard")]
    [TestCase("SkillRecoilCopyAndValidation")]
    [TestCase("WallStopsPlayerBeforePenetration")]
    [TestCase("NavMeshEdgeCapsRecoil")]
    [TestCase("MissingNavMeshDoesNotMovePlayer")]
    [TestCase("RecoilCleanupPreservesOtherControlOwnersAndSkipsSelfGuard")]
    public void Run(string name)
    {
        var type = Type.GetType("DefensiveBlockPlayerRecoilTests, Assembly-CSharp-Editor", true);
        var fixture = Activator.CreateInstance(type);
        try { type.GetMethod("SetUp").Invoke(fixture, null); type.GetMethod(name).Invoke(fixture, null); }
        finally { type.GetMethod("TearDown").Invoke(fixture, null); }
    }
}
