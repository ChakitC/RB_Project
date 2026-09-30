using System;
using System.Collections;
using System.Reflection;
using System.Runtime.ExceptionServices;
using NUnit.Framework;

// Expose the existing default Editor assembly contracts to Unity Test Runner.
public sealed class AnimationTransitionRegressionRunnerTests
{
    public static IEnumerable Cases()
    {
        foreach (string fixtureName in new[]
        {
            "CharacterAnimBrainSmokeTests",
            "CharacterAnimationTransitionPolicyTests",
        })
        {
            Type type = Type.GetType(fixtureName + ", Assembly-CSharp-Editor", true);
            foreach (MethodInfo method in type.GetMethods())
            {
                if (method.IsDefined(typeof(TestAttribute), false))
                    yield return new TestCaseData(fixtureName, method.Name, Array.Empty<object>())
                        .SetName(fixtureName + "." + method.Name);
                int index = 0;
                foreach (TestCaseAttribute test in method.GetCustomAttributes(typeof(TestCaseAttribute), false))
                    yield return new TestCaseData(fixtureName, method.Name, test.Arguments)
                        .SetName(fixtureName + "." + method.Name + "[" + index++ + "]");
            }
        }
    }

    [TestCaseSource(nameof(Cases))]
    public void Run(string fixtureName, string methodName, object[] arguments)
    {
        Type type = Type.GetType(fixtureName + ", Assembly-CSharp-Editor", true);
        object fixture = Activator.CreateInstance(type);
        try
        {
            type.GetMethod("SetUp").Invoke(fixture, null);
            type.GetMethod(methodName).Invoke(fixture, arguments);
        }
        catch (TargetInvocationException error) when (error.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(error.InnerException).Throw();
            throw;
        }
        finally { type.GetMethod("TearDown").Invoke(fixture, null); }
    }
}
