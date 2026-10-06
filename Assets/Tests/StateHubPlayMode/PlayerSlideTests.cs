using System.Collections;
using UnityEngine.TestTools;

public sealed class PlayerSlideTests
{
    [NUnit.Framework.TearDown] public void Cleanup() => PlayerLocomotionTestRunner.Cleanup();
    [UnityTest] public IEnumerator StaminaDistance() => PlayerLocomotionTestRunner.Run("StaminaDistance");
    [UnityTest] public IEnumerator StaminaRates() => PlayerLocomotionTestRunner.Run("StaminaRates");
    [UnityTest] public IEnumerator StaminaRoma() => PlayerLocomotionTestRunner.Run("StaminaRoma");
    [UnityTest] public IEnumerator StaminaWall() => PlayerLocomotionTestRunner.Run("StaminaWall");
    [UnityTest] public IEnumerator StaminaExhaustion() => PlayerLocomotionTestRunner.Run("StaminaExhaustion");
    [UnityTest] public IEnumerator StaminaEmptyEntry() => PlayerLocomotionTestRunner.Run("StaminaEmptyEntry");
    [UnityTest] public IEnumerator StaminaFree() => PlayerLocomotionTestRunner.Run("StaminaFree");
    [UnityTest] public IEnumerator StaminaTinyCosts() => PlayerLocomotionTestRunner.Run("StaminaTinyCosts");
    [UnityTest] public IEnumerator DashEntry() => PlayerLocomotionTestRunner.Run("DashEntry");
    [UnityTest] public IEnumerator DashEntryNested() => PlayerLocomotionTestRunner.Run("DashEntryNested");
    [UnityTest] public IEnumerator DashEntryRoma() => PlayerLocomotionTestRunner.Run("DashEntryRoma");
    [UnityTest] public IEnumerator DashEntryAir() => PlayerLocomotionTestRunner.Run("DashEntryAir");
    [UnityTest] public IEnumerator DashEntryWall() => PlayerLocomotionTestRunner.Run("DashEntryWall");
    [UnityTest] public IEnumerator Heading() => PlayerLocomotionTestRunner.Run("Heading");
    [UnityTest] public IEnumerator NestedPeers() => PlayerLocomotionTestRunner.Run("NestedPeers");
    [UnityTest] public IEnumerator Steer() => PlayerLocomotionTestRunner.Run("Steer");
    [UnityTest] public IEnumerator Brake() => PlayerLocomotionTestRunner.Run("Brake");
    [UnityTest] public IEnumerator Diagonal() => PlayerLocomotionTestRunner.Run("Diagonal");
    [UnityTest] public IEnumerator Duration() => PlayerLocomotionTestRunner.Run("Duration");
    [UnityTest] public IEnumerator Release() => PlayerLocomotionTestRunner.Run("Release");
    [UnityTest] public IEnumerator Dash() => PlayerLocomotionTestRunner.Run("Dash");
    [UnityTest] public IEnumerator FailedDash() => PlayerLocomotionTestRunner.Run("FailedDash");
    [UnityTest] public IEnumerator Wall() => PlayerLocomotionTestRunner.Run("Wall");
    [UnityTest] public IEnumerator AngledWall() => PlayerLocomotionTestRunner.Run("AngledWall");
    [UnityTest] public IEnumerator Fire() => PlayerLocomotionTestRunner.Run("Fire");
    [UnityTest] public IEnumerator Reload() => PlayerLocomotionTestRunner.Run("Reload");
    [UnityTest] public IEnumerator ReloadRoma() => PlayerLocomotionTestRunner.Run("ReloadRoma");
    [UnityTest] public IEnumerator ReloadEntryRoma() => PlayerLocomotionTestRunner.Run("ReloadEntryRoma");
    [UnityTest] public IEnumerator Suspend() => PlayerLocomotionTestRunner.Run("Suspend");
    [UnityTest] public IEnumerator Ui() => PlayerLocomotionTestRunner.Run("Ui");
    [UnityTest] public IEnumerator Stun() => PlayerLocomotionTestRunner.Run("Stun");
    [UnityTest] public IEnumerator Down() => PlayerLocomotionTestRunner.Run("Down");
    [UnityTest] public IEnumerator Dead() => PlayerLocomotionTestRunner.Run("Dead");
    [UnityTest] public IEnumerator Disable() => PlayerLocomotionTestRunner.Run("Disable");
    [UnityTest] public IEnumerator Focus() => PlayerLocomotionTestRunner.Run("Focus");
    [UnityTest] public IEnumerator Ledge() => PlayerLocomotionTestRunner.Run("Ledge");
    [UnityTest] public IEnumerator Step() => PlayerLocomotionTestRunner.Run("Step");
    [UnityTest] public IEnumerator Slope() => PlayerLocomotionTestRunner.Run("Slope");
    [UnityTest] public IEnumerator Pause() => PlayerLocomotionTestRunner.Run("Pause");
    [UnityTest] public IEnumerator RomaPrefab() => PlayerLocomotionTestRunner.Run("RomaPrefab");
}
