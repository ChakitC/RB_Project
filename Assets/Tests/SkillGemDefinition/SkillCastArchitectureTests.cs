using System;
using System.Reflection;
using NUnit.Framework;

public sealed class SkillCastArchitectureTests
{
    static void Run(string method, params object[] arguments)
    {
        var harness = Type.GetType("SkillArchitectureTestHarness, Assembly-CSharp-Editor", true);
        try { harness.GetMethod(method).Invoke(null, arguments); }
        catch (TargetInvocationException exception)
        { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerException).Throw(); }
    }

    [TestCase("started", false)]
    [TestCase("started", true)]
    [TestCase("released", false)]
    [TestCase("released", true)]
    [TestCase("payload", false)]
    [TestCase("payload", true)]
    [TestCase("committed", false)]
    [TestCase("committed", true)]
    [TestCase("energy", false)]
    [TestCase("energy", true)]
    [TestCase("direct", false)]
    public void ExceptionsSettleResourcesWithoutUndoingCommittedEffects(string phase, bool animated) =>
        Run("ExceptionCase", phase, animated);

    [TestCase(false)]
    [TestCase(true)]
    public void CastKeepsStatsAndUpgradesFromReservation(bool replace) => Run("SnapshotCase", replace);

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public void AuthoringValidationUsesActualBindings(bool dual, bool profile) => Run("UsageCase", dual, profile);
}
