#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class WeaponAffixFrameworkTests
{
    const string DatabasePath = "Assets/Scripts/Weapons/WeaponAffixDatabase.asset";

    sealed class EndpointRollSource : IWeaponAffixRollSource
    {
        readonly bool maximum;
        public EndpointRollSource(bool maximum) { this.maximum = maximum; }
        public float Range(float minimum, float maximumValue) => maximum ? maximumValue : minimum;
    }

    [Test]
    public void DatabaseContainsAllApprovedAffixesAndPassesValidation()
    {
        WeaponAffixDatabase database = AssetDatabase.LoadAssetAtPath<WeaponAffixDatabase>(DatabasePath);
        Assert.That(database, Is.Not.Null);
        Assert.That(database.Affixes, Has.Count.EqualTo(27));
        Assert.That(WeaponAffixMigrationTool.ValidateAll(), Is.Empty);
    }

    [Test]
    public void EveryBehaviorRollsAtAuthoredEndpointsAndHasTooltipData()
    {
        WeaponAffixDatabase database = AssetDatabase.LoadAssetAtPath<WeaponAffixDatabase>(DatabasePath);
        var minimum = new EndpointRollSource(false);
        var maximum = new EndpointRollSource(true);

        foreach (WeaponAffixDefinition definition in database.Affixes)
        {
            Assert.That(definition.rootBehavior, Is.Not.Null, definition.affixId);
            WeaponAffixRollSpec spec = definition.rootBehavior.RollSpec;
            Assert.That(definition.rootBehavior.RollPrimaryValue(minimum), Is.EqualTo(spec.minimum), definition.affixId);
            float expectedMaximum = spec.valueKind == WeaponAffixRollValueKind.Integer
                ? Mathf.Round(spec.maximum)
                : spec.maximum;
            Assert.That(definition.rootBehavior.RollPrimaryValue(maximum), Is.EqualTo(expectedMaximum), definition.affixId);
            Assert.That(definition.rootBehavior.Tooltip.effect, Is.Not.Empty, definition.affixId);
        }
    }

    [Test]
    public void LastRoundRequiresBaseMagazineOfTwentyFive()
    {
        WeaponAffixDatabase database = AssetDatabase.LoadAssetAtPath<WeaponAffixDatabase>(DatabasePath);
        WeaponAffixDefinition lastRound = database.GetById("weapon.main.last_round.v1");
        var gun = ScriptableObject.CreateInstance<GunConfig>();
        gun.WeaponType = WeaponType.Rifle;
        gun.maxMagazine = 24;
        Assert.That(lastRound.SupportsWeapon(gun), Is.False);
        gun.maxMagazine = 25;
        Assert.That(lastRound.SupportsWeapon(gun), Is.True);
        Object.DestroyImmediate(gun);
    }

    [Test]
    public void PersistentStateDeepCloneDoesNotShareEntries()
    {
        var source = new WeaponInstanceData();
        WeaponAffixRuntimeStateData state = source.GetOrCreateAffixState("weapon.main.conservation_round.v1");
        state.entries.Add(new WeaponAffixRuntimeStateEntry { key = "progress", intValue = 3 });
        WeaponInstanceData clone = source.DeepClone();
        clone.affixRuntimeStates[0].entries[0].intValue = 5;
        Assert.That(source.affixRuntimeStates[0].entries[0].intValue, Is.EqualTo(3));
    }

    [Test]
    public void TypedMetadataCalculatesOverkillWithoutChangingEventValue()
    {
        var metadata = new CombatEventMetadata(
            requestedDamage: 80f,
            resolvedDamage: 80f,
            appliedDamage: 30f,
            healthBeforeHit: 30f,
            maxHealth: 100f);
        var context = new PassiveEventContext(
            PassiveEventType.Kill, null, null, null, null, "attack", 30f, 0d, 1, 0,
            PassiveEventOrigin.External, null, null, metadata);
        Assert.That(context.Value, Is.EqualTo(30f));
        Assert.That(context.Metadata.OverkillAmount, Is.EqualTo(50f));
    }

    [Test]
    public void AffixGeneratedMetadataIsMarkedForRecursionGuard()
    {
        var metadata = new CombatEventMetadata(
            weaponInstanceId: "weapon-instance",
            sourceKind: CombatSourceKind.WeaponAffix,
            weaponAffixId: "weapon.main.overkill.v1");
        Assert.That(metadata.IsWeaponAffixGenerated, Is.True);
    }
}
#endif
