using System;
using NUnit.Framework;

public sealed class PairOffsetPlayModeTests
{
    [Test]
    public void RomaRigDuringInterruptedSlideFade()
    {
        var type = Type.GetType("CommanBlockAnimationTests, Assembly-CSharp-Editor", true);
        var fixture = Activator.CreateInstance(type);
        try
        {
            type.GetMethod("SetUp").Invoke(fixture, null);
            type.GetMethod("RomaPairOffsetsFollowActualRigDuringSlideFade").Invoke(fixture, null);
        }
        finally { type.GetMethod("TearDown").Invoke(fixture, null); }
    }
}
