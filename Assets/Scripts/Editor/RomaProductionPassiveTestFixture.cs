using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class RomaProductionPassiveTestFixture
{
    readonly PlayerSlideTestFixture player = new();
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;

    public IEnumerator Run()
    {
        // Reuse the production Player/Roma setup, including its real character loader.
        yield return (IEnumerator)typeof(PlayerSlideTestFixture).GetMethod("Setup", Hidden)
            .Invoke(player, new object[] { false, true });
        var ctx = (PlayerContext)typeof(PlayerSlideTestFixture).GetField("ctx", Hidden).GetValue(player);
        var definition = AssetDatabase.LoadAssetAtPath<PassiveDefinition>(
            "Assets/Character/Roma/Passive/Passive.Roma_StaminaMomentum.asset");
        Assert.That(definition, Is.Not.Null);
        Assert.That(PassiveRecipeValidator.Validate(definition.recipe), Is.Empty);
        var grants = new List<EquippedPassive>();
        ctx.SkillManager.AppendConfiguredPassiveDefinitions(grants);
        Assert.That(grants.Count(g => g.Definition == definition), Is.EqualTo(1));
        Assert.That(ctx.PassiveController.RuntimeCount, Is.GreaterThanOrEqualTo(1));
        Assert.That(ctx.PassiveController.HasCombatSubscription, Is.True);

        ctx.StaminaSystem.RecalculateMaximumStamina(true);
        Assert.That(ctx.StaminaSystem.Spend(10, "production-passive-check"), Is.True);
        float before = ctx.StaminaSystem.Current;
        var dodge = ctx.CombatEventBus.CreateExternalContext(PassiveEventType.PerfectDodge,
            metadata: new CombatEventMetadata(dashExecutionId: 900001));
        ctx.CombatEventBus.Publish(dodge);
        ctx.CombatEventBus.Publish(ctx.CombatEventBus.CreateExternalContext(PassiveEventType.PerfectDodge,
            metadata: new CombatEventMetadata(dashExecutionId: 900001)));
        Assert.That(ctx.StaminaSystem.Current, Is.EqualTo(before + 10).Within(0.001f));
        using (var attack = ctx.PassiveController.ReserveNextAttack("production-check"))
        {
            Assert.That(attack, Is.Not.Null);
            Assert.That(attack.Multiplier, Is.EqualTo(1.2f).Within(0.001f));
            attack.Commit();
        }

        float oldMax = ctx.StatsHub.GetMaximumStamina();
        var bonus = new PassiveActionDefinition { durationSeconds = 0.5f };
        bonus.modifiers.Add(new PassiveStatModifier { statType = StatType.StaminaRegenRate, value = 1 });
        Assert.That(ctx.PassiveController.TryApplyRuntimeModifier(bonus, "production-test-regen", 0), Is.True);
        Assert.That(ctx.StatsHub.GetMaximumStamina(), Is.EqualTo(oldMax + 4).Within(0.001f));
        ctx.StaminaSystem.RecalculateMaximumStamina();
        using (var attack = ctx.PassiveController.ReserveNextAttack("max-change-check"))
            Assert.That(attack.Multiplier, Is.EqualTo(1f));

        Assert.That(ctx.StaminaSystem.Spend(ctx.StaminaSystem.Max * 0.4f), Is.True);
        var modifiers = new List<RuntimeStatModifier>();
        ctx.PassiveController.AppendStatModifiers(modifiers);
        var missingDamage = modifiers.Where(m => m.StatType == StatType.Damage &&
            m.ModifierKey.Contains(definition.DefinitionId)).ToList();
        Assert.That(missingDamage.Count, Is.EqualTo(1));
        Assert.That(missingDamage[0].Value, Is.EqualTo(20f).Within(0.001f));
        yield return null;
    }

    public void Cleanup() => player.TearDown();
}
