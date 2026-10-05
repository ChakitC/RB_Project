using System;
using System.Collections;

internal static class PlayerLocomotionTestRunner
{
    private static Type activeType;
    private static object activeFixture;

    public static void Cleanup()
    {
        var fixture = activeFixture;
        var type = activeType;
        activeFixture = null;
        activeType = null;
        if (fixture != null) type.GetMethod("TearDown").Invoke(fixture, null);
    }

    public static IEnumerator Run(string scenario)
    {
        var type = Type.GetType("PlayerSlideTestFixture, Assembly-CSharp-Editor", true);
        var fixture = Activator.CreateInstance(type);
        activeType = type;
        activeFixture = fixture;
        try { yield return (IEnumerator)type.GetMethod("Run").Invoke(fixture, new object[] { scenario }); }
        finally { Cleanup(); }
    }
}
