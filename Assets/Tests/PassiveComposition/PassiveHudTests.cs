using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using NUnit.Framework;

public sealed class PassiveHudTests
{
    static Type Fixture => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("PassiveHudTestFixture")).First(t => t != null);
    static IEnumerable Cases()
    {
        foreach (var method in Fixture.GetMethods().Where(m => m.IsDefined(typeof(TestAttribute), true)))
            yield return new TestCaseData(method.Name).SetName("PassiveHud." + method.Name);
    }
    [TestCaseSource(nameof(Cases))]
    public void Run(string name)
    {
        var fixture = Activator.CreateInstance(Fixture);
        try { Invoke("Setup"); Invoke(name); }
        finally { Invoke("Cleanup"); }
        void Invoke(string method)
        {
            try { Fixture.GetMethod(method).Invoke(fixture, null); }
            catch (TargetInvocationException e) when (e.InnerException != null) { ExceptionDispatchInfo.Capture(e.InnerException).Throw(); }
        }
    }
}
