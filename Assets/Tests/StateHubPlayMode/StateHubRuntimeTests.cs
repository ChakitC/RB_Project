using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

public sealed class StateHubRuntimeTests
{
    [UnityTest]
    public IEnumerator ReloadAndActorLifecycle() => Run("RuntimeReloadAndLifecycle");
    [UnityTest]
    public IEnumerator UiAndDashOwnership() => Run("RuntimeUiAndDashOwnership");

    IEnumerator Run(string name)
    {
        var type = Type.GetType("StateHubArchitectureTests, Assembly-CSharp-Editor", true);
        var fixture = Activator.CreateInstance(type);
        try
        {
            type.GetMethod("SetUp").Invoke(fixture, null);
            yield return (IEnumerator)type.GetMethod(name).Invoke(fixture, null);
        }
        finally { type.GetMethod("TearDown").Invoke(fixture, null); }
    }
}
