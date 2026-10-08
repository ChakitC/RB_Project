using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>One-time native asset migration; never changes saved inventory or numeric tuning.</summary>
public static class StatScopeMigration
{
    [Serializable] public sealed class Report
    {
        public bool dryRun;
        public int changedAssets;
        public List<string> changes = new();
        public List<string> errors = new();
        public List<Sample> samples = new();
        public string sampleBasis = "Synthetic character Damage=20, gun Damage=80; idle affixes at maximum roll, upgrades at level 10. Combat-triggered bonuses are not active in these samples.";
        public string stackChanges = "At 3 stacks of x1.2: Passive 3.6 -> 1.6; Status 1.728 -> 1.6. At 3 stacks of x0.8: Status 0.512 -> 0.4. Numeric authoring values are unchanged.";
    }
    [Serializable] public sealed class Sample
    {
        public string asset;
        public float oldDamage, newDamage, oldSkillDamage, newSkillDamage;
        public CharacterStatTotals before, after;
    }

    [MenuItem("Tools/RB/Stats/Preview Scope Migration")]
    static void Preview() => Debug.Log(JsonUtility.ToJson(Run(true), true));
    [MenuItem("Tools/RB/Stats/Apply Scope Migration")]
    static void Apply() => Debug.Log(JsonUtility.ToJson(Run(false), true));

    [MenuItem("Tools/RB/Stats/Validate Modifier Scopes")]
    static void ValidateMenu()
    {
        var errors = ValidateProjectScopes();
        if (errors.Count == 0) Debug.Log("Stat modifier scope validation passed.");
        else Debug.LogError(string.Join("\n", errors));
    }

    public static List<string> ValidateProjectScopes()
    {
        var errors = new List<string>();
        var seen = new HashSet<Object>();
        foreach (string type in new[] { "AccessoryDefinition", "AccessoryReforgeSettings", "StatusEffectDef", "WeaponUpgradeCurve", "PassiveRecipe", "PassiveFragment", "PassiveDefinition" })
            foreach (string guid in AssetDatabase.FindAssets("t:" + type))
                foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)))
                    if (asset != null && seen.Add(asset)) errors.AddRange(ValidateScopes(asset));
        return errors;
    }

    public static List<string> ValidateScopes(Object asset)
    {
        var errors = new List<string>();
        using var serialized = new SerializedObject(asset);
        var property = serialized.GetIterator();
        while (property.Next(true))
        {
            if (property.name != "scope" || property.propertyType != SerializedPropertyType.Enum) continue;
            string prefix = property.propertyPath.Substring(0, property.propertyPath.Length - "scope".Length);
            var stat = serialized.FindProperty(prefix + "statType") ?? serialized.FindProperty(prefix + "stat");
            if (stat != null && !StatModifierRules.IsValidScope((StatType)stat.intValue, (StatModifierScope)property.intValue))
                errors.Add(AssetDatabase.GetAssetPath(asset) + " / " + asset.name + " / " + property.propertyPath + ": unsupported scope/stat");
        }
        return errors;
    }

    public static Report Run(bool dryRun)
    {
        var report = new Report { dryRun = dryRun };
        foreach (string guid in AssetDatabase.FindAssets("t:WeaponUpgradeCurve"))
        {
            var curve = AssetDatabase.LoadAssetAtPath<WeaponUpgradeCurve>(AssetDatabase.GUIDToAssetPath(guid));
            if (curve == null || curve.statScopeSchemaVersion >= 1) continue;
            int count = ScopeProperties(curve, false);
            report.changes.Add(AssetDatabase.GetAssetPath(curve) + ": " + count + " weapon modifiers");
            report.changedAssets++;
            var oldTotals = PreviewUpgrade(curve);
            if (!dryRun)
            {
                Undo.RecordObject(curve, "Migrate weapon stat scopes");
                ScopeProperties(curve, true);
                curve.statScopeSchemaVersion = 1;
                EditorUtility.SetDirty(curve);
                AssetDatabase.SaveAssetIfDirty(curve);
            }
            var newTotals = dryRun ? oldTotals : PreviewUpgrade(curve);
            report.samples.Add(new Sample { asset = AssetDatabase.GetAssetPath(curve), before = oldTotals, after = newTotals,
                oldDamage = oldTotals.Damage, newDamage = newTotals.Damage, oldSkillDamage = oldTotals.SkillBaseDamage, newSkillDamage = newTotals.SkillBaseDamage });
        }
        foreach (string guid in AssetDatabase.FindAssets("t:WeaponAffixDefinition"))
        {
            var affix = AssetDatabase.LoadAssetAtPath<WeaponAffixDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (affix == null || affix.statScopeSchemaVersion >= 1) continue;
            MigrateAffix(affix, dryRun, report);
        }
        return report;
    }

    public static void MigrateAffix(WeaponAffixDefinition affix, bool dryRun, Report report)
    {
        if (affix.statScopeSchemaVersion >= 1) return;
        var definition = affix.passiveDefinition;
        if (definition == null || definition.recipe == null)
        { report.errors.Add(affix.name + ": missing passive recipe"); return; }
        int count = CountGraph(definition.recipe, new HashSet<Object>());
        string path = AssetDatabase.GetAssetPath(affix);
        report.changedAssets++;
        report.changes.Add(path + ": " + count + " weapon modifiers; isolate changed passive graph, preserve original references");
        var before = PreviewTotals(definition, affix.rollSpec.maximum);
        var sample = new Sample { asset = path, oldDamage = before.Damage, oldSkillDamage = before.SkillBaseDamage,
            newDamage = before.Damage, newSkillDamage = before.SkillBaseDamage, before = before, after = before };
        if (!dryRun)
        {
            Undo.RecordObject(affix, "Migrate weapon stat scopes");
            if (count > 0)
            {
                // Copy-on-write, including subassets: external character/item references stay untouched.
                var copy = Object.Instantiate(definition);
                copy.name = definition.name + " (Weapon scopes)";
                AssetDatabase.AddObjectToAsset(copy, affix);
                copy.SetDefinitionIdForMigration(definition.DefinitionId);
                copy.recipe = (PassiveRecipe)CloneGraph(definition.recipe, affix, new Dictionary<Object, Object>());
                EditorUtility.SetDirty(copy);
                affix.passiveDefinition = copy;
            }
            affix.statScopeSchemaVersion = 1;
            EditorUtility.SetDirty(affix);
            AssetDatabase.SaveAssetIfDirty(affix);
            var after = PreviewTotals(affix.passiveDefinition, affix.rollSpec.maximum);
            sample.newDamage = after.Damage; sample.newSkillDamage = after.SkillBaseDamage;
            sample.after = after;
        }
        report.samples.Add(sample);
    }

    // Controlled graph: do not rewrite skill trees, projectile effects or other combat behavior.
    static bool IsStatGraph(Object value) => value is PassiveRecipe or PassiveFragment or StatusEffectDef;
    static int CountGraph(Object asset, HashSet<Object> visited)
    {
        if (asset == null || !visited.Add(asset)) return 0;
        int count = ScopeProperties(asset, false);
        using var serialized = new SerializedObject(asset);
        var property = serialized.GetIterator();
        while (property.Next(true))
            if (property.propertyType == SerializedPropertyType.ObjectReference && IsStatGraph(property.objectReferenceValue))
                count += CountGraph(property.objectReferenceValue, visited);
        return count;
    }
    static Object CloneGraph(Object original, Object owner, Dictionary<Object, Object> copies)
    {
        if (copies.TryGetValue(original, out var existing)) return existing;
        var copy = Object.Instantiate(original);
        copy.name = original.name + " (Weapon scopes)";
        copies.Add(original, copy);
        AssetDatabase.AddObjectToAsset(copy, owner);
        using (var serialized = new SerializedObject(copy))
        {
            var property = serialized.GetIterator();
            while (property.Next(true))
                if (property.propertyType == SerializedPropertyType.ObjectReference && IsStatGraph(property.objectReferenceValue))
                    property.objectReferenceValue = CloneGraph(property.objectReferenceValue, owner, copies);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        ScopeProperties(copy, true);
        EditorUtility.SetDirty(copy);
        return copy;
    }

    static int ScopeProperties(Object asset, bool apply)
    {
        int count = 0;
        using var serialized = new SerializedObject(asset);
        var property = serialized.GetIterator();
        while (property.Next(true))
        {
            if (property.name != "scope" || property.propertyType != SerializedPropertyType.Enum) continue;
            string prefix = property.propertyPath.Substring(0, property.propertyPath.Length - "scope".Length);
            var stat = serialized.FindProperty(prefix + "statType") ?? serialized.FindProperty(prefix + "stat");
            if (stat == null || !StatModifierRules.SupportsWeaponScope((StatType)stat.intValue) ||
                property.intValue == (int)StatModifierScope.WeaponOnly) continue;
            count++;
            if (apply) property.intValue = (int)StatModifierScope.WeaponOnly;
        }
        if (apply) serialized.ApplyModifiedPropertiesWithoutUndo();
        return count;
    }

    static CharacterStatTotals PreviewTotals(PassiveDefinition definition, float roll)
    {
        var modifiers = new List<RuntimeStatModifier>();
        PassiveStatPreview.Append(new PassiveGrant(definition, "scope-audit", "affix",
            options: new PassiveGrantOptions("audit-weapon", parameters: new Dictionary<string, float> { ["roll"] = roll })), modifiers);
        var gun = ScriptableObject.CreateInstance<GunConfig>();
        try
        {
            gun.damage = 80;
            return CharacterStatFormula.Compute(new CharacterStatInputs
            { Weapon = gun, WeaponInstanceId = "audit-weapon", CharacterDamage = 20, CharacterCritMultiplier = 1 }, modifiers);
        }
        finally { Object.DestroyImmediate(gun); }
    }

    static CharacterStatTotals PreviewUpgrade(WeaponUpgradeCurve curve)
    {
        var gun = ScriptableObject.CreateInstance<GunConfig>();
        try
        {
            gun.damage = 80; gun.maxMagazine = 30; gun.critMultiplier = 1; gun.fireRate = .1f; gun.reloadTime = 2;
            var modifiers = new List<RuntimeStatModifier>();
            curve.AppendStatModifiers(modifiers, gun, new WeaponInstanceData { instanceId = "audit-weapon", upgradeLevel = 10 }, "audit");
            return CharacterStatFormula.Compute(new CharacterStatInputs
            { Weapon = gun, WeaponInstanceId = "audit-weapon", CharacterDamage = 20, CharacterCritMultiplier = 1 }, modifiers);
        }
        finally { Object.DestroyImmediate(gun); }
    }

    public static string WriteReport(bool dryRun)
    {
        Report report = Run(dryRun);
        string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../BuildArtifacts/StatScopes"));
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, dryRun ? "migration-preview.json" : "migration-applied.json");
        File.WriteAllText(path, JsonUtility.ToJson(report, true));
        return path;
    }
}
