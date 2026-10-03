using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

public sealed class AnimationArchitectureRuntimeTests
{
    [UnityTest]
    public IEnumerator LiveAndCrawlConsumeMovementInTheWritingFrame()
    {
        Type type = Type.GetType("CharacterAnimBrainSmokeTests, Assembly-CSharp-Editor", true);
        object fixture = Activator.CreateInstance(type);
        try
        {
            type.GetMethod("SetUp").Invoke(fixture, null);
            yield return (IEnumerator)type.GetMethod("RuntimeMovementIntentOrder").Invoke(fixture, null);
        }
        finally { type.GetMethod("TearDown").Invoke(fixture, null); }
    }
}
