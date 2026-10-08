using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class StatScopeTestFixture
{
    readonly List<Object> owned = new();
    readonly PassiveCompositionTestFixture actors = new();
    const string TestFolder = "Assets/Tests/StatScopes/MigrationScratch";
    T Own<T>(T value) where T : Object { owned.Add(value); return value; }
    public void Setup() => actors.Setup();
    public void Cleanup()
    {
        actors.Cleanup();
        for (int i = owned.Count - 1; i >= 0; i--) if (owned[i] != null) Object.DestroyImmediate(owned[i]);
        owned.Clear();
        if (AssetDatabase.IsValidFolder(TestFolder)) AssetDatabase.DeleteAsset(TestFolder);
    }
    CharacteContext Actor(bool nested = false) => (CharacteContext)typeof(PassiveCompositionTestFixture)
        .GetMethod("Actor", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(actors, new object[] { nested });
    GunConfig Gun()
    {
        var gun = Own(ScriptableObject.CreateInstance<GunConfig>());
        gun.damage = 80; gun.critRate = 10; gun.critMultiplier = 2;
        gun.maxMagazine = 10; gun.maxReserveAmmo = 0; gun.infiniteReserveAmmo = false;
        gun.reloadTime = 2; gun.fireRate = .2f; gun.stability = 50; gun.BulletSpeed = 20;
        return gun;
    }
    static RuntimeStatModifier Mod(StatType stat, ModifierOp op, float value, bool weapon = false, string id = null) =>
        new(stat, op, value, scope: weapon ? StatModifierScope.WeaponOnly : StatModifierScope.Global, weaponInstanceId: id);
    CharacterStatInputs Inputs(GunConfig gun, string id = "A") => new()
    { Weapon = gun, WeaponInstanceId = id, CharacterDamage = 20, CharacterCritRate = 5, CharacterCritMultiplier = 1.5f,
        CharacterMaxHealth = 100, CharacterMoveSpeed = 10, CharacterMaxStamina = 100 };

    [Test] public void WeaponThenGlobalMatchesAcceptedExampleRegardlessOfOrder()
    {
        var inputs = Inputs(Gun());
        var mods = new List<RuntimeStatModifier> { Mod(StatType.Damage, ModifierOp.AddPercent, 20, true, "A"),
            Mod(StatType.Damage, ModifierOp.Flat, 10), Mod(StatType.Damage, ModifierOp.AddPercent, 30) };
        for (int i = 0; i < mods.Count; i++)
        {
            var totals = CharacterStatFormula.Compute(inputs, mods);
            Assert.That(totals.Damage, Is.EqualTo(163.8f).Within(.001));
            Assert.That(totals.SkillBaseDamage, Is.EqualTo(39f).Within(.001));
            var first = mods[0]; mods.RemoveAt(0); mods.Add(first);
        }
    }
    [Test] public void GlobalFlatIsNotScaledByWeaponPercentAndSeparateMultipliersMultiply()
    {
        var mods = new List<RuntimeStatModifier> { Mod(StatType.Damage, ModifierOp.Flat, 20, true),
            Mod(StatType.Damage, ModifierOp.AddPercent, 50, true), Mod(StatType.Damage, ModifierOp.Multiply, 1.2f, true),
            Mod(StatType.Damage, ModifierOp.Flat, 10), Mod(StatType.Damage, ModifierOp.Multiply, 1.2f), Mod(StatType.Damage, ModifierOp.Multiply, 1.3f) };
        Assert.That(CharacterStatFormula.Compute(Inputs(Gun()), mods).Damage, Is.EqualTo(327.6f).Within(.001));
    }
    [Test] public void SameGunConfigDifferentInstanceCannotBorrowBoundModifiers()
    {
        var gun = Gun();
        var mods = new List<RuntimeStatModifier> { Mod(StatType.Damage, ModifierOp.AddPercent, 50, true, "A"), Mod(StatType.Damage, ModifierOp.Flat, 10, true) };
        Assert.That(CharacterStatFormula.Compute(Inputs(gun, "A"), mods).Damage, Is.EqualTo(155));
        Assert.That(CharacterStatFormula.Compute(Inputs(gun, "B"), mods).Damage, Is.EqualTo(110));
        Assert.That(CharacterStatFormula.Compute(Inputs(gun, null), mods).Damage, Is.EqualTo(110));
        Assert.That(CharacterStatFormula.Compute(Inputs(null), mods).Damage, Is.EqualTo(20));
    }
    [Test] public void FloorsUseOriginalBaseOnlyOnceAndNeverWeaponAdjustedBase()
    {
        var mods = new List<RuntimeStatModifier> { Mod(StatType.Damage, ModifierOp.Multiply, 10, true), Mod(StatType.Damage, ModifierOp.Multiply, 0) };
        Assert.That(CharacterStatFormula.Compute(Inputs(Gun()), mods).Damage, Is.EqualTo(20));
        Assert.That(CharacterStatFormula.Compute(Inputs(Gun()), mods).SkillBaseDamage, Is.EqualTo(4));
        mods = new() { Mod(StatType.MaxHP, ModifierOp.Multiply, 0), Mod(StatType.MaxStamina, ModifierOp.Multiply, 0), Mod(StatType.MoveSpeed, ModifierOp.Multiply, 0) };
        var totals = CharacterStatFormula.Compute(Inputs(Gun()), mods);
        Assert.That(totals.MaxHealth, Is.EqualTo(20)); Assert.That(totals.MaxStamina, Is.EqualTo(20)); Assert.That(totals.MoveSpeed, Is.EqualTo(2));
    }
    [Test] public void CritAndWeaponTimingUseBothLayersBeforeClamps()
    {
        var mods = new List<RuntimeStatModifier> { Mod(StatType.CritRate, ModifierOp.AddPercent, 100, true), Mod(StatType.CritRate, ModifierOp.Flat, 3),
            Mod(StatType.CritMultiplier, ModifierOp.Flat, 1, true), Mod(StatType.CritMultiplier, ModifierOp.Multiply, 2),
            Mod(StatType.ReloadTime, ModifierOp.AddPercent, -50, true), Mod(StatType.ReloadTime, ModifierOp.Flat, .2f),
            Mod(StatType.FireInterval, ModifierOp.Multiply, .5f, true), Mod(StatType.Stability, ModifierOp.Flat, 200, true),
            Mod(StatType.BulletSpeed, ModifierOp.Multiply, 2, true) };
        var totals = CharacterStatFormula.Compute(Inputs(Gun()), mods);
        Assert.That(totals.CritRatePercent, Is.EqualTo(28)); Assert.That(totals.CritMultiplier, Is.EqualTo(7));
        Assert.That(totals.ReloadTime, Is.EqualTo(1.2f).Within(.001)); Assert.That(totals.FireInterval, Is.EqualTo(.1f).Within(.001));
        Assert.That(totals.Stability, Is.EqualTo(100)); Assert.That(totals.BulletSpeed, Is.EqualTo(40));
    }
    [Test] public void MagazineRoundsOnceAndReserveUsesFinalMagazine()
    {
        var gun = Gun();
        var mods = new List<RuntimeStatModifier> { Mod(StatType.MaxMagazine, ModifierOp.Flat, .4f, true), Mod(StatType.MaxMagazine, ModifierOp.Flat, .4f),
            Mod(StatType.MaxReserveAmmo, ModifierOp.Flat, .4f, true), Mod(StatType.MaxReserveAmmo, ModifierOp.Flat, .4f) };
        var totals = CharacterStatFormula.Compute(Inputs(gun), mods);
        Assert.That(totals.MaxMagazine, Is.EqualTo(11)); Assert.That(totals.MaxReserveAmmo, Is.EqualTo(34));
        gun.infiniteReserveAmmo = true;
        Assert.That(CharacterStatFormula.Compute(Inputs(gun), mods).MaxReserveAmmo, Is.Zero);
    }
    [Test] public void StackMathCoversNeutralSingleMultipleAndNegativeFactors()
    {
        Assert.That(StatModifierRules.StackValue(ModifierOp.Multiply, 1.2f, 0), Is.EqualTo(1));
        Assert.That(StatModifierRules.StackValue(ModifierOp.Multiply, 1.2f, 1), Is.EqualTo(1.2f).Within(.001));
        Assert.That(StatModifierRules.StackValue(ModifierOp.Multiply, 1.2f, 3), Is.EqualTo(1.6f).Within(.001));
        Assert.That(StatModifierRules.StackValue(ModifierOp.Multiply, .8f, 3), Is.EqualTo(.4f).Within(.001));
        Assert.That(StatModifierRules.StackValue(ModifierOp.Multiply, .8f, 10), Is.Zero);
        Assert.That(StatModifierRules.StackValue(ModifierOp.Flat, 5, 3), Is.EqualTo(15));
        Assert.That(StatModifierRules.StackValue(ModifierOp.AddPercent, 20, 3), Is.EqualTo(60));
    }
    [Test] public void UpgradeStepsShareStackMathIncludingZeroFactor()
    {
        var curve = Own(ScriptableObject.CreateInstance<WeaponUpgradeCurve>());
        curve.statBonuses = new() { new() { statType = StatType.Damage, operation = ModifierOp.Multiply, valuePerStep = 1.2f, scope = StatModifierScope.WeaponOnly } };
        var list = new List<RuntimeStatModifier>(); var gun = Gun();
        var instance = new WeaponInstanceData { instanceId = "A", upgradeLevel = 0 };
        curve.AppendStatModifiers(list, gun, instance, "upgrade"); Assert.That(list, Is.Empty);
        instance.upgradeLevel = 3; curve.AppendStatModifiers(list, gun, instance, "upgrade");
        Assert.That(list.Single().Value, Is.EqualTo(1.6f).Within(.001)); Assert.That(list.Single().WeaponInstanceId, Is.EqualTo("A"));
        curve.statBonuses[0].valuePerStep = 0; list.Clear(); curve.AppendStatModifiers(list, gun, instance, "upgrade");
        Assert.That(list.Single().Value, Is.Zero);
    }
    [Test] public void PassiveStacksCapExpireRefreshAndKeepIndependentGroups()
    {
        var actor = Actor();
        var action = new PassiveActionDefinition { grantedStacks = 1, maxStacks = 3, durationSeconds = 2, stackPolicy = PassiveModifierStackPolicy.AddStacks,
            modifiers = new() { new() { statType = StatType.Damage, operation = ModifierOp.Multiply, value = 1.2f, scope = StatModifierScope.WeaponOnly } } };
        for (int i = 0; i < 5; i++) actor.PassiveController.TryApplyRuntimeModifier(action, "test", 0, weaponInstanceId: "A");
        var list = new List<RuntimeStatModifier>(); actor.PassiveController.AppendStatModifiers(list);
        Assert.That(list.Single().Value, Is.EqualTo(1.6f).Within(.001)); Assert.That(list.Single().WeaponInstanceId, Is.EqualTo("A"));
        actor.PassiveController.Tick(1); action.stackPolicy = PassiveModifierStackPolicy.RefreshDuration;
        actor.PassiveController.TryApplyRuntimeModifier(action, "test", 1, weaponInstanceId: "A");
        actor.PassiveController.Tick(1.1f); list.Clear(); actor.PassiveController.AppendStatModifiers(list); Assert.That(list, Has.Count.EqualTo(1));
        actor.PassiveController.Tick(1); list.Clear(); actor.PassiveController.AppendStatModifiers(list); Assert.That(list, Is.Empty);
        action.stackPolicy = PassiveModifierStackPolicy.Independent;
        actor.PassiveController.TryApplyRuntimeModifier(action, "test", 0); actor.PassiveController.TryApplyRuntimeModifier(action, "test", 0);
        list.Clear(); actor.PassiveController.AppendStatModifiers(list); Assert.That(list, Has.Count.EqualTo(2));
        Assert.That(CharacterStatFormula.ApplyWeaponModifiers(list, StatType.Damage, 100), Is.EqualTo(144).Within(.001));
    }
    [Test] public void StatusStacksAndClonePreserveScopeAndBinding()
    {
        var actor = Actor(true); var definition = Own(ScriptableObject.CreateInstance<StatusEffectDef>());
        definition.effectId = "stat-scope-status"; definition.maxStacks = 3; definition.stackMode = StackMode.AddStackAndRefresh;
        definition.modifiers = new() { new() { statType = StatType.Damage, operation = ModifierOp.Multiply, value = 1.2f, scope = StatModifierScope.WeaponOnly } };
        var spec = new StatusApplicationSpec { effect = definition, stacks = 1, weaponInstanceId = "A" };
        Assert.That(spec.Clone().weaponInstanceId, Is.EqualTo("A"));
        for (int i = 0; i < 4; i++) actor.StatusEffects.ApplyEffect(spec, actor.gameObject);
        var list = new List<RuntimeStatModifier>(); actor.StatusEffects.AppendStatModifiers(list);
        Assert.That(list.Single().Value, Is.EqualTo(1.6f).Within(.001));
        Assert.That(list.Single().Scope, Is.EqualTo(StatModifierScope.WeaponOnly)); Assert.That(list.Single().WeaponInstanceId, Is.EqualTo("A"));
        Assert.That(definition.modifiers.Single().value, Is.EqualTo(1.2f));
        Assert.That(StatusEffectInstance.ComputeStrengthScore(definition.modifiers, 3), Is.EqualTo(.6f).Within(.001));
    }
    [Test] public void UnsupportedWeaponScopeIsRejectedByAuthoringAndNeverActsGlobally()
    {
        var recipe = Own(ScriptableObject.CreateInstance<PassiveRecipe>());
        recipe.rules.Add(new() { trigger = PassiveRuleTrigger.Equipped, steps = new() { new PassiveDynamicModifierStep { stat = StatType.MaxHP, scope = StatModifierScope.WeaponOnly, value = new(100) } } });
        Assert.That(PassiveRecipeValidator.Validate(recipe).Any(e => e.Contains("unsupported WeaponOnly")), Is.True);
        Assert.That(StatScopeMigration.ValidateScopes(recipe), Has.Count.EqualTo(1));
        Assert.That(CharacterStatFormula.Compute(Inputs(Gun()), new() { Mod(StatType.MaxHP, ModifierOp.Flat, 100, true) }).MaxHealth, Is.EqualTo(100));
    }
    [Test] public void DynamicAndEquippedPreviewMatchRuntimeWithoutDoubleStacking()
    {
        var actor = Actor(); var definition = Own(ScriptableObject.CreateInstance<ComposedPassiveDef>());
        definition.recipe = Own(ScriptableObject.CreateInstance<PassiveRecipe>());
        var flat = new PassiveModifierStep { id = "flat", whileEquipped = true,
            modifier = new() { modifiers = new() { new() { statType = StatType.Damage, value = 10, scope = StatModifierScope.WeaponOnly } } } };
        definition.recipe.rules.Add(new() { id = "equipped", trigger = PassiveRuleTrigger.Equipped, steps = new() { flat,
            new PassiveDynamicModifierStep { id = "dynamic", stat = StatType.Damage, scope = StatModifierScope.WeaponOnly, operation = ModifierOp.AddPercent, value = new(60) } } });
        var grant = new PassiveGrant(definition, "test", "test", options: new PassiveGrantOptions("A"));
        actor.PassiveController.SetRuntimeGrants(new[] { grant });
        var live = new List<RuntimeStatModifier>(); var preview = new List<RuntimeStatModifier>();
        actor.PassiveController.AppendStatModifiers(live); PassiveStatPreview.Append(grant, preview);
        var inputs = Inputs(Gun());
        Assert.That(CharacterStatFormula.Compute(inputs, live).Damage, Is.EqualTo(164).Within(.001));
        Assert.That(CharacterStatFormula.Compute(inputs, preview).Damage, Is.EqualTo(164).Within(.001));
        Assert.That(live.All(m => m.WeaponInstanceId == "A"), Is.True);
        actor.PassiveController.SetRuntimeGrants(Array.Empty<PassiveGrant>()); live.Clear(); actor.PassiveController.AppendStatModifiers(live); Assert.That(live, Is.Empty);
    }
    [Test] public void StatsHubDetectsScopeAndBindingChangesOnRootAndNestedPeers()
    {
        foreach (bool nested in new[] { false, true })
        {
            var actor = Actor(nested); actor.baseStats.Damage = 20;
            var weapon = actor.gameObject.AddComponent<WeaponSystem>(); actor.WeaponSystem = weapon;
            var gun = Gun(); var instance = new WeaponInstanceData { instanceId = "A" };
            typeof(WeaponSystem).GetField("currentWeapon", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(weapon, gun);
            typeof(WeaponSystem).GetField("currentWeaponInstance", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(weapon, instance);
            typeof(StatsHub).GetMethod("ResolveReferences", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(actor.StatsHub, null);
            var definition = Own(ScriptableObject.CreateInstance<ComposedPassiveDef>());
            definition.recipe = Own(ScriptableObject.CreateInstance<PassiveRecipe>());
            var step = new PassiveDynamicModifierStep { id = "scope", stat = StatType.Damage, operation = ModifierOp.AddPercent, value = new(20) };
            definition.recipe.rules.Add(new() { trigger = PassiveRuleTrigger.Equipped, steps = new() { step } });
            actor.PassiveController.SetRuntimeGrants(new[] { new PassiveGrant(definition, "scope-test", "test", options: new PassiveGrantOptions("A")) });
            Assert.That(actor.StatsHub.Damage, Is.EqualTo(120).Within(.001));
            int revision = actor.StatsHub.Revision; step.scope = StatModifierScope.WeaponOnly;
            Assert.That(actor.StatsHub.RefreshCacheIfInputsChanged(), Is.True);
            Assert.That(actor.StatsHub.Revision, Is.GreaterThan(revision));
            Assert.That(actor.StatsHub.Damage, Is.EqualTo(116).Within(.001));
            Assert.That(actor.StatsHub.GetSkillBaseDamage(), Is.EqualTo(20).Within(.001));
            instance.instanceId = "B";
            Assert.That(actor.StatsHub.Damage, Is.EqualTo(100).Within(.001));
            var other = Gun(); Assert.That(actor.StatsHub.GetDamage(other), Is.EqualTo(100).Within(.001));
        }
    }

    [Test] public void LobbyReadsComposedRecipeInsteadOfLegacyAlwaysOnFields()
    {
        var definition = Own(ScriptableObject.CreateInstance<ComposedPassiveDef>());
        definition.recipe = Own(ScriptableObject.CreateInstance<PassiveRecipe>());
        definition.recipe.rules.Add(new() { trigger = PassiveRuleTrigger.Equipped,
            steps = new() { new PassiveDynamicModifierStep { stat = StatType.Damage, value = new(7) } } });
        var list = new List<RuntimeStatModifier>();
        typeof(LobbyCharacterStatPreview).GetMethod("AppendPassiveModifiers", BindingFlags.NonPublic | BindingFlags.Static)
            .Invoke(null, new object[] { list, new List<PassiveDefinition> { definition } });
        Assert.That(list.Single().Value, Is.EqualTo(7));
    }

    [Test] public void StatusRecipeAllowsZeroFactorButRejectsInvalidInheritedScope()
    {
        var status = Own(ScriptableObject.CreateInstance<StatusEffectDef>());
        status.modifiers = new() { new() { statType = StatType.Damage, operation = ModifierOp.Multiply, value = 0, scope = StatModifierScope.WeaponOnly } };
        var recipe = Own(ScriptableObject.CreateInstance<PassiveRecipe>());
        recipe.rules.Add(new() { trigger = PassiveRuleTrigger.Equipped, steps = new() { new PassiveStatusStep { spec = new() { effect = status } } } });
        Assert.That(PassiveRecipeValidator.Validate(recipe), Is.Empty);
        status.modifiers[0].statType = StatType.MaxHP;
        Assert.That(PassiveRecipeValidator.Validate(recipe).Any(e => e.Contains("unsupported WeaponOnly")), Is.True);
    }

    [Test] public void MigrationCopiesSharedGraphsPreservesIdsAndIsIdempotent()
    {
        AssetDatabase.CreateFolder("Assets/Tests/StatScopes", "MigrationScratch");
        var affix = ScriptableObject.CreateInstance<WeaponAffixDefinition>(); AssetDatabase.CreateAsset(affix, TestFolder + "/Affix.asset");
        var definition = ScriptableObject.CreateInstance<ComposedPassiveDef>(); AssetDatabase.CreateAsset(definition, TestFolder + "/Shared.asset");
        definition.passiveId = "stable-passive";
        var recipe = ScriptableObject.CreateInstance<PassiveRecipe>(); AssetDatabase.AddObjectToAsset(recipe, definition);
        var fragment = ScriptableObject.CreateInstance<PassiveFragment>(); AssetDatabase.AddObjectToAsset(fragment, definition);
        fragment.steps.Add(new PassiveDynamicModifierStep { id = "stable-step", stat = StatType.Damage, operation = ModifierOp.AddPercent, value = new(20) });
        recipe.rules.Add(new() { id = "stable-rule", trigger = PassiveRuleTrigger.Equipped, steps = new() { new PassiveFragmentStep { id = "fragment", fragment = fragment } } });
        definition.recipe = recipe; affix.passiveDefinition = definition;
        string id = definition.DefinitionId, guid = AssetDatabase.AssetPathToGUID(TestFolder + "/Affix.asset");
        var report = new StatScopeMigration.Report(); StatScopeMigration.MigrateAffix(affix, true, report);
        Assert.That(affix.passiveDefinition, Is.SameAs(definition)); Assert.That(affix.statScopeSchemaVersion, Is.Zero);
        StatScopeMigration.MigrateAffix(affix, false, new());
        Assert.That(affix.passiveDefinition, Is.Not.SameAs(definition)); Assert.That(affix.passiveDefinition.DefinitionId, Is.EqualTo(id));
        Assert.That(AssetDatabase.AssetPathToGUID(TestFolder + "/Affix.asset"), Is.EqualTo(guid));
        Assert.That(((PassiveDynamicModifierStep)fragment.steps[0]).scope, Is.EqualTo(StatModifierScope.Global));
        var copied = (PassiveFragmentStep)affix.passiveDefinition.recipe.rules[0].steps[0];
        Assert.That(((PassiveDynamicModifierStep)copied.fragment.steps[0]).scope, Is.EqualTo(StatModifierScope.WeaponOnly));
        Assert.That(copied.fragment.steps[0].id, Is.EqualTo("stable-step"));
        var migrated = affix.passiveDefinition;
        var second = new StatScopeMigration.Report(); StatScopeMigration.MigrateAffix(affix, false, second);
        Assert.That(second.changedAssets, Is.Zero); Assert.That(affix.passiveDefinition, Is.SameAs(migrated));
    }
}
