using System;
using System.Reflection;
using NUnit.Framework;

public sealed class SkillManagerRefactorTests
{
    static void Run(string method, params object[] arguments)
    {
        var harness = Type.GetType("SkillManagerRefactorTestHarness, Assembly-CSharp-Editor", true);
        try { harness.GetMethod(method).Invoke(null, arguments); }
        catch (TargetInvocationException exception)
        { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(exception.InnerException).Throw(); }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void SlotsShareChargesAcrossReassignmentAndLoad(bool nested) => Run("SharedCharges", nested);

    [TestCase(false)]
    [TestCase(true)]
    public void ExternalPlaybackKeepsItsRequestAndOwner(bool nested) => Run("ExternalPlayback", nested);

    [TestCase("slot", false)]
    [TestCase("slot", true)]
    [TestCase("disable", false)]
    [TestCase("disable", true)]
    [TestCase("death", false)]
    [TestCase("death", true)]
    public void CancelledCastCannotReleaseTheNextRequest(string reason, bool nested) =>
        Run("CancellationAndStaleSignals", reason, nested);

    [TestCase(false)]
    [TestCase(true)]
    public void HelperSwitchCancelsWindupAndPreservesCooldown(bool sameDefinition) => Run("HelperVariant", sameDefinition);
}
