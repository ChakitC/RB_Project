using System;
using System.Reflection;
using NUnit.Framework;

public sealed class BasementGunMenuTests
{
    [Test] public void AuthoringUsesEquipmentButtonAndReferenceLens()
    {
        var fixture = Activator.CreateInstance(Type.GetType("BasementGunMenuTestFixture, Assembly-CSharp-Editor", true));
        try { fixture.GetType().GetMethod(nameof(AuthoringUsesEquipmentButtonAndReferenceLens)).Invoke(fixture, null); }
        catch (TargetInvocationException e) { throw e.InnerException; }
        finally { fixture.GetType().GetMethod("Cleanup").Invoke(fixture, null); }
    }
}