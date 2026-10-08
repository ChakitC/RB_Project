using System;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using NUnit.Framework;

public sealed class StartingAmmoTests
{
    [Test]
    public void StartupFillsAmmoAndEquipPreservesIt()
    {
        var type = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("StartingAmmoTestFixture")).First(t => t != null);
        try { type.GetMethod("Run").Invoke(Activator.CreateInstance(type), null); }
        catch (TargetInvocationException exception) when (exception.InnerException != null)
        { ExceptionDispatchInfo.Capture(exception.InnerException).Throw(); }
    }
}
