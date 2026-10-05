using System;
using UnityEngine.TestTools;

/// <summary>Native authoring must finish before entering Play Mode.</summary>
public sealed class AIContextGraphPrebuild : IPrebuildSetup, IPostBuildCleanup
{
    public void Setup() => Invoke("PrepareNestedContextGraphFixture");
    public void Cleanup() => Invoke("CleanupNestedContextGraphFixture");
    static void Invoke(string method)
    {
        var type = Type.GetType("AICombatP0Checks, Assembly-CSharp-Editor", true);
        try { type.GetMethod(method).Invoke(null, null); }
        catch (System.Reflection.TargetInvocationException exception) { throw exception.InnerException; }
    }
}
