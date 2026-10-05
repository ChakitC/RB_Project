using System.Collections;
using UnityEngine.TestTools;

public sealed class PlayerCrouchTests
{
    [NUnit.Framework.TearDown] public void Cleanup() => PlayerLocomotionTestRunner.Cleanup();
    [UnityTest] public IEnumerator WalkAndStand() => PlayerLocomotionTestRunner.Run("Crouch");
    [UnityTest] public IEnumerator Ceiling() => PlayerLocomotionTestRunner.Run("Ceiling");
    [UnityTest] public IEnumerator DashCeiling() => PlayerLocomotionTestRunner.Run("DashCeiling");
    [UnityTest] public IEnumerator ExternalShape() => PlayerLocomotionTestRunner.Run("ExternalShape");
    [UnityTest] public IEnumerator NoAirQueue() => PlayerLocomotionTestRunner.Run("NoAirQueue");
}
