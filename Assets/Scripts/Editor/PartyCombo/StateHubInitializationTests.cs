using NUnit.Framework;
using UnityEngine;

public sealed class StateHubInitializationTests
{
    [Test]
    public void SkillReadinessBeforeAwakeIsUnavailableInsteadOfThrowing()
    {
        var root = new GameObject("Inactive party actor");
        root.SetActive(false);
        try
        {
            StateHub state = root.AddComponent<StateHub>();
            Assert.That(state.IsInitialized, Is.False);
            Assert.That(state.CanUseSkill(), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }
}
