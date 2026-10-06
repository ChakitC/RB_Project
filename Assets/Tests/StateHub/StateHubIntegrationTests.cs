using System;
using NUnit.Framework;

public sealed class StateHubIntegrationTests
{
    [TestCase("SkillOnlyBlockDoesNotBlockBasicMelee")]
    [TestCase("ComboSkillBlockBindsHitboxesAndInterruptClearsTheCombo")]
    [TestCase("MissingExecutionSkillFailsWithoutEnteringMeleeState")]
    [TestCase("BasicMeleeNeverTouchesEnergyOrSharedCharges")]
    public void MeleeOwner(string name) => Run("MeleeSkillIntegrationTests", name, "Cleanup");

    [TestCase(false)]
    [TestCase(true)]
    public void MeleeRootAndNested(bool nested) => Run("MeleeSkillIntegrationTests",
        "PlayerPressAdvancesComboAndReusesHost", "Cleanup", nested);

    [TestCase(false)]
    [TestCase(true)]
    public void SkillAcceptanceAndMenu(bool nested) => Run("MeleeSkillIntegrationTests",
        "SkillAcceptancePreservesReloadOnRejectionAndSurvivesMenu", "Cleanup", nested);

    [TestCase("ScopedTokenAddsAndRemovesOnlyItsOwnBlocks")]
    [TestCase("ReleasingAScopedTokenDoesNotClearLegacyExternalBlocks")]
    [TestCase("TwoScopedOwnersReleaseIndependently")]
    [TestCase("ReleasingAnUnknownTokenIsANoOp")]
    [TestCase("AcquiringNoFlagsReturnsAnInertToken")]
    public void ExistingTokenUsers(string name) => Run("StageIntroSmokeTests", name, "TearDown");

    static void Run(string fixtureName, string method, string cleanup, params object[] args)
    {
        var type = Type.GetType(fixtureName + ", Assembly-CSharp-Editor", true);
        var fixture = Activator.CreateInstance(type);
        try { type.GetMethod(method).Invoke(fixture, args); }
        finally { type.GetMethod(cleanup).Invoke(fixture, null); }
    }
}
