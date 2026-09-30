using System;
using NUnit.Framework;

// The gameplay assembly is predefined, so the discoverable test assembly delegates to typed Editor fixtures.
public sealed class StateHubControlTests
{
    [TestCase("AdmissionMatrix")]
    [TestCase("TokensBeforeAwakeAndIndependentSources")]
    [TestCase("ReadOnlyViewsAndQueriesHaveNoSideEffects")]
    [TestCase("LifeCommandsPublishOnceBeforeReturning")]
    [TestCase("FaultingPresentationDoesNotAbortDeath")]
    [TestCase("UiTokensComposeAndDoNotCreateTimeManagers")]
    [TestCase("RootAndChildPeersPreserveExplicitBindings")]
    public void Run(string name)
    {
        var type = Type.GetType("StateHubArchitectureTests, Assembly-CSharp-Editor", true);
        var fixture = Activator.CreateInstance(type);
        try { type.GetMethod("SetUp").Invoke(fixture, null); type.GetMethod(name).Invoke(fixture, null); }
        finally { type.GetMethod("TearDown").Invoke(fixture, null); }
    }
}
