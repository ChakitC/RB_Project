using System;
using System.Reflection;
using NUnit.Framework;

public sealed class BasementSupplyTests
{
    [Test]
    public void AuthoringIsReadOnlyAndReferencesAreComplete()
    {
        var fixture = Activator.CreateInstance(Type.GetType("BasementSupplyTestFixture, Assembly-CSharp-Editor", true));
        try { fixture.GetType().GetMethod(nameof(AuthoringIsReadOnlyAndReferencesAreComplete)).Invoke(fixture, null); }
        catch (TargetInvocationException e) { throw e.InnerException; }
        finally { fixture.GetType().GetMethod("Cleanup").Invoke(fixture, null); }
    }
}
