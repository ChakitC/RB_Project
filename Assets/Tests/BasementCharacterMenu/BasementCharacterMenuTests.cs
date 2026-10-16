using System;
using System.Reflection;
using NUnit.Framework;

public sealed class BasementCharacterMenuTests
{
    [Test] public void TrackingRetainsRollAcrossCameraUpdates()
    {
        var type=Type.GetType("BasementCharacterMenuTestFixture, Assembly-CSharp-Editor",true);
        var fixture=Activator.CreateInstance(type);
        try { type.GetMethod(nameof(TrackingRetainsRollAcrossCameraUpdates)).Invoke(fixture,null); }
        catch(TargetInvocationException e) { throw e.InnerException; }
        finally { type.GetMethod("Cleanup").Invoke(fixture,null); }
    }
    [Test] public void BlendTracksTargetAndPreservesEndpointRoll()
    {
        var type=Type.GetType("BasementCharacterMenuTestFixture, Assembly-CSharp-Editor",true);
        var fixture=Activator.CreateInstance(type);
        try { type.GetMethod(nameof(BlendTracksTargetAndPreservesEndpointRoll)).Invoke(fixture,null); }
        catch(TargetInvocationException e) { throw e.InnerException; }
        finally { type.GetMethod("Cleanup").Invoke(fixture,null); }
    }
    [Test] public void AuthoringHasStatusBindingsAndLoopingAires()
    {
        var type=Type.GetType("BasementCharacterMenuTestFixture, Assembly-CSharp-Editor",true);
        var fixture=Activator.CreateInstance(type);
        try { type.GetMethod(nameof(AuthoringHasStatusBindingsAndLoopingAires)).Invoke(fixture,null); }
        catch(TargetInvocationException e) { throw e.InnerException; }
        finally { type.GetMethod("Cleanup").Invoke(fixture,null); }
    }
}
