using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

public sealed class VfxLifecyclePlayModeTests
{
    [UnityTest]
    public IEnumerator RuntimeProductionEffects()
    {
        var type = Type.GetType("VfxLifecycleTestFixture, Assembly-CSharp-Editor", true);
        var fixture = Activator.CreateInstance(type);
        try
        {
            type.GetMethod("SetUp").Invoke(fixture, null);
            yield return (IEnumerator)type.GetMethod("RuntimeProductionEffects").Invoke(fixture, null);
        }
        finally { type.GetMethod("TearDown").Invoke(fixture, null); }
    }
    [UnityTest]
    public IEnumerator RuntimeLifecycleCallbacks()
    {
        var type = Type.GetType("VfxLifecycleTestFixture, Assembly-CSharp-Editor", true);
        var fixture = Activator.CreateInstance(type);
        try
        {
            type.GetMethod("SetUp").Invoke(fixture, null);
            yield return (IEnumerator)type.GetMethod("RuntimeLifecycleCallbacks").Invoke(fixture, null);
        }
        finally { type.GetMethod("TearDown").Invoke(fixture, null); }
    }
    [UnityTest]
    public IEnumerator RuntimeLifetimePauseAndWorldSlow()
    {
        var type = Type.GetType("VfxLifecycleTestFixture, Assembly-CSharp-Editor", true);
        var fixture = Activator.CreateInstance(type);
        try
        {
            type.GetMethod("SetUp").Invoke(fixture, null);
            yield return (IEnumerator)type.GetMethod("RuntimeLifetimePauseAndWorldSlow").Invoke(fixture, null);
        }
        finally { type.GetMethod("TearDown").Invoke(fixture, null); }
    }
    [UnityTest]
    public IEnumerator RuntimeTailSceneCleanupAndReuse()
    {
        var type = Type.GetType("VfxLifecycleTestFixture, Assembly-CSharp-Editor", true);
        var fixture = Activator.CreateInstance(type);
        try
        {
            type.GetMethod("SetUp").Invoke(fixture, null);
            yield return (IEnumerator)type.GetMethod("RuntimeTailSceneCleanupAndReuse").Invoke(fixture, null);
        }
        finally { type.GetMethod("TearDown").Invoke(fixture, null); }
    }
}
