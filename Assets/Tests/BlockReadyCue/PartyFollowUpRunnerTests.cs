using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using NUnit.Framework;

// Default-assembly fixtures are reached through reflection from the Test Runner assembly.
public sealed class PartyFollowUpRunnerTests
{
    static readonly string[] Fixtures =
    {
        "PartyFollowUpCoreSmokeTests", "PartyFollowUpPlacementTests",
        "PartyFollowUpPresentationTests", "PartyFollowUpCameraHoldTests",
        "GameplayFollowUpHudTests", "PartyHudTests",
    };

    static Type Fixture(string name) => AppDomain.CurrentDomain.GetAssemblies()
        .Select(assembly => assembly.GetType(name)).First(type => type != null);

    static IEnumerable Cases()
    {
        foreach (string fixture in Fixtures)
        {
            foreach (MethodInfo method in Fixture(fixture).GetMethods())
            {
                if (method.IsDefined(typeof(ExplicitAttribute), true)) continue;
                var cases = method.GetCustomAttributes<TestCaseAttribute>().ToArray();
                if (cases.Length > 0)
                {
                    foreach (var test in cases)
                        yield return new TestCaseData(fixture, method.Name, test.Arguments)
                            .SetName($"FollowUp.{fixture}.{method.Name}({string.Join(",", test.Arguments)})");
                }
                else if (method.IsDefined(typeof(TestAttribute), true))
                    yield return new TestCaseData(fixture, method.Name, Array.Empty<object>())
                        .SetName($"FollowUp.{fixture}.{method.Name}");
            }
        }
        yield return new TestCaseData("SkillLoadoutUiTests",
            "ComboProgressReloadsAndUpdatesOnlyItsRuntimeSnapshot", Array.Empty<object>())
            .SetName("FollowUp.SavedUpgradeProgress");
    }

    [TestCaseSource(nameof(Cases))]
    public void Run(string fixtureName, string methodName, object[] args)
    {
        Type type = Fixture(fixtureName);
        object fixture = Activator.CreateInstance(type);
        try
        {
            foreach (var setup in type.GetMethods().Where(m => m.IsDefined(typeof(SetUpAttribute), true)))
                Invoke(setup, fixture, null);
            Invoke(type.GetMethod(methodName), fixture, args);
        }
        finally
        {
            foreach (var cleanup in type.GetMethods().Where(m => m.IsDefined(typeof(TearDownAttribute), true)))
                Invoke(cleanup, fixture, null);
        }
    }

    static void Invoke(MethodInfo method, object fixture, object[] args)
    {
        try { method.Invoke(fixture, args); }
        catch (TargetInvocationException exception) when (exception.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
        }
    }
}
