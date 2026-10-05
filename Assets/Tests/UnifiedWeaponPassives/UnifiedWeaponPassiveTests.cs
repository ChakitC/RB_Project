using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using NUnit.Framework;

public sealed class UnifiedWeaponPassiveTests
{
    static Type Fixture => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("UnifiedWeaponPassiveTestFixture")).First(t => t != null);
    static IEnumerable Cases()
    {
        foreach (var method in Fixture.GetMethods().Where(m => m.IsDefined(typeof(TestAttribute), true)))
            yield return new TestCaseData(method.Name).SetName("UnifiedWeaponPassive." + method.Name);
    }
    [TestCaseSource(nameof(Cases))]
    public void Run(string name)
    {
        var fixture = Activator.CreateInstance(Fixture);
        try { Invoke(Fixture.GetMethod("Setup"), fixture); Invoke(Fixture.GetMethod(name), fixture); }
        finally { Invoke(Fixture.GetMethod("Cleanup"), fixture); }
    }
    static void Invoke(MethodInfo method, object fixture)
    {
        try { method.Invoke(fixture, null); }
        catch (TargetInvocationException e) when (e.InnerException != null) { ExceptionDispatchInfo.Capture(e.InnerException).Throw(); }
    }
}
