#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class WeaponAffixMigrationTool
{
    const string DatabasePath = "Assets/Scripts/Weapons/WeaponAffixDatabase.asset";
    const string AssetFolder = "Assets/Scripts/Weapons/WeaponAffix";

    sealed class Spec
    {
        public string id, name;
        public WeaponAffixRuntimeKind kind;
        public WeaponAffixSlot slot;
        public float min, max;
        public WeaponType[] weapons;
        public StatType stat;
        public ModifierOp op = ModifierOp.AddPercent;
        public float duration, fixedValue, secondaryValue;
        public int cap, threshold, minMagazine;
        public float weight = 1f;
        public float procChance = 1f;
        public ProjectileConfig projectileConfig;
        public GameObject projectilePrefab;
    }

    static readonly Spec[] NewSpecs =
    {
        S("weapon.sub.stagger_power.v1", "Impact Core", WeaponAffixRuntimeKind.ImpactCore, WeaponAffixSlot.Sub, 10, 20, All, StatType.StaggerPower, weight:.85f),
        S("weapon.main.kill_clip.v1", "Kill Clip", WeaponAffixRuntimeKind.KillClip, WeaponAffixSlot.Main, 12, 18, All, StatType.Damage, duration:4),
        S("weapon.main.execution_feed.v1", "Execution Feed", WeaponAffixRuntimeKind.ExecutionFeed, WeaponAffixSlot.Main, 8, 12, Standard),
        S("weapon.main.last_round.v1", "Last Round", WeaponAffixRuntimeKind.LastRound, WeaponAffixSlot.Main, 45, 75, Auto, minMagazine:25),
        S("weapon.main.head_hunter.v1", "Head Hunter", WeaponAffixRuntimeKind.HeadHunter, WeaponAffixSlot.Main, .08f, .16f, Precision, duration:4, cap:5),
        S("weapon.main.fresh_chamber.v1", "Fresh Chamber", WeaponAffixRuntimeKind.FreshChamber, WeaponAffixSlot.Main, 15, 25, All, cap:3),
        S("weapon.main.hot_streak.v1", "Hot Streak", WeaponAffixRuntimeKind.HotStreak, WeaponAffixSlot.Main, 4, 6, Auto, duration:4, cap:3),
        S("weapon.main.pressure_point.v1", "Pressure Point", WeaponAffixRuntimeKind.PressurePoint, WeaponAffixSlot.Main, 6, 10, All, duration:3, cap:5),
        S("weapon.main.conservation_round.v1", "Conservation Round", WeaponAffixRuntimeKind.ConservationRound, WeaponAffixSlot.Main, 4, 6, All),
        S("weapon.main.overkill.v1", "Overkill", WeaponAffixRuntimeKind.Overkill, WeaponAffixSlot.Main, 60, 90, new[]{WeaponType.Sniper,WeaponType.Shotgun,WeaponType.Pistol}),
        S("weapon.main.broken_guard.v1", "Broken Guard", WeaponAffixRuntimeKind.BrokenGuard, WeaponAffixSlot.Main, .35f, .65f, All, duration:5),
        S("weapon.main.marked_quarry.v1", "Marked Quarry", WeaponAffixRuntimeKind.MarkedQuarry, WeaponAffixSlot.Main, 20, 30, Auto, duration:5, cap:5),
        S("weapon.main.blood_magazine.v1", "Blood Magazine", WeaponAffixRuntimeKind.BloodMagazine, WeaponAffixSlot.Main, 15, 25, Auto, duration:4),
    };

    static WeaponType[] All => new[]{WeaponType.Sniper,WeaponType.Shotgun,WeaponType.Pistol,WeaponType.Rifle,WeaponType.Smg,WeaponType.Hmg};
    static WeaponType[] Standard => new[]{WeaponType.Pistol,WeaponType.Rifle,WeaponType.Smg,WeaponType.Hmg};
    static WeaponType[] Auto => new[]{WeaponType.Rifle,WeaponType.Smg,WeaponType.Hmg};
    static WeaponType[] Precision => new[]{WeaponType.Sniper,WeaponType.Shotgun,WeaponType.Pistol,WeaponType.Rifle};

    static Spec S(string id,string name,WeaponAffixRuntimeKind kind,WeaponAffixSlot slot,float min,float max,WeaponType[] weapons,
        StatType stat=StatType.Damage,float duration=0,int cap=0,int threshold=0,int minMagazine=0,float weight=1)
        => new Spec{id=id,name=name,kind=kind,slot=slot,min=min,max=max,weapons=weapons,stat=stat,duration=duration,cap=cap,threshold=threshold,minMagazine=minMagazine,weight=weight};

    [MenuItem("Tools/Weapons/Affixes/Migrate And Generate")]
    public static void MigrateAndGenerate()
    {
        var database = AssetDatabase.LoadAssetAtPath<WeaponAffixDatabase>(DatabasePath);
        if (database == null) throw new InvalidOperationException($"Missing database at {DatabasePath}");
        Undo.RecordObject(database, "Migrate weapon affixes");
        var databaseObject = new SerializedObject(database);
        var affixes = databaseObject.FindProperty("affixes");
        var definitions = new Dictionary<string, WeaponAffixDefinition>(StringComparer.Ordinal);
        for (int i=0;i<affixes.arraySize;i++) { var d=affixes.GetArrayElementAtIndex(i).objectReferenceValue as WeaponAffixDefinition; if(d!=null&&!string.IsNullOrEmpty(d.affixId)) definitions[d.affixId]=d; }

        foreach (var pair in definitions) BindExisting(pair.Value);
        foreach (var spec in NewSpecs)
        {
            if (!definitions.TryGetValue(spec.id,out var definition))
            {
                definition=ScriptableObject.CreateInstance<WeaponAffixDefinition>();
                definition.affixId=spec.id; definition.displayName=spec.name; definition.description=spec.name;
                string path=$"{AssetFolder}/WeaponAffix.{spec.name.Replace(" ",string.Empty)}.{spec.slot}.asset";
                AssetDatabase.CreateAsset(definition,AssetDatabase.GenerateUniqueAssetPath(path));
                affixes.InsertArrayElementAtIndex(affixes.arraySize);
                affixes.GetArrayElementAtIndex(affixes.arraySize-1).objectReferenceValue=definition;
            }
            ApplySpec(definition,spec);
        }
        databaseObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(database); AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        Debug.Log($"Weapon affix migration complete. {affixes.arraySize} definitions registered.");
    }

    static void BindExisting(WeaponAffixDefinition definition)
    {
        var spec=new Spec{id=definition.affixId,name=definition.displayName,slot=definition.slot,min=definition.minRollValue,max=definition.maxRollValue,
            weapons=definition.allowedWeaponTypes?.ToArray(),stat=definition.statType,op=definition.modifierOp,duration=definition.buffDurationSeconds,
            threshold=definition.requiredShots,fixedValue=definition.specialProjectileDamageMultiplier,secondaryValue=definition.specialProjectileSpeedMultiplier,weight=definition.weight,
            procChance=definition.procChance,projectileConfig=definition.specialProjectileConfig,projectilePrefab=definition.specialProjectilePrefab,
            kind=definition.behaviorType==WeaponAffixBehaviorType.StatModifier?WeaponAffixRuntimeKind.StaticStat:
                definition.behaviorType==WeaponAffixBehaviorType.TimedBuffOnReload?WeaponAffixRuntimeKind.ReloadBuff:
                definition.affixId.Contains("breach")?WeaponAffixRuntimeKind.BreachChamber:WeaponAffixRuntimeKind.EchoChamber};
        ApplySpec(definition,spec);
    }

    static void ApplySpec(WeaponAffixDefinition definition,Spec spec)
    {
        Undo.RecordObject(definition,"Configure weapon affix");
        definition.slot=spec.slot; definition.weight=spec.weight; definition.allowedWeaponTypes=new List<WeaponType>(spec.weapons??All);
        if (definition.rootBehavior==null)
        {
            var behavior=ScriptableObject.CreateInstance<ConfiguredWeaponAffixBehavior>();
            behavior.name=$"{definition.name}.Behavior";
            AssetDatabase.AddObjectToAsset(behavior,definition); definition.rootBehavior=behavior;
        }
        var configured=definition.rootBehavior as ConfiguredWeaponAffixBehavior;
        configured.kind=spec.kind; configured.statType=spec.stat; configured.modifierOp=spec.op; configured.duration=spec.duration;
        configured.cap=spec.cap; configured.threshold=spec.threshold; configured.fixedValue=spec.fixedValue; configured.secondaryValue=spec.secondaryValue; configured.minimumBaseMagazine=spec.minMagazine;
        configured.procChance=spec.procChance; configured.projectileConfig=spec.projectileConfig; configured.projectilePrefab=spec.projectilePrefab;
        var serialized=new SerializedObject(configured); var roll=serialized.FindProperty("roll");
        roll.FindPropertyRelative("minimum").floatValue=spec.min; roll.FindPropertyRelative("maximum").floatValue=spec.max;
        roll.FindPropertyRelative("valueKind").enumValueIndex=spec.kind==WeaponAffixRuntimeKind.ConservationRound?1:0;
        var tooltip=serialized.FindProperty("tooltip");
        BuildTooltip(spec,out string trigger,out string effect,out string restriction);
        tooltip.FindPropertyRelative("trigger").stringValue=trigger;
        tooltip.FindPropertyRelative("effect").stringValue=effect;
        tooltip.FindPropertyRelative("restriction").stringValue=restriction;
        tooltip.FindPropertyRelative("duration").floatValue=spec.duration;
        tooltip.FindPropertyRelative("cap").intValue=spec.cap;
        serialized.ApplyModifiedProperties(); EditorUtility.SetDirty(configured); EditorUtility.SetDirty(definition);
    }

    static void BuildTooltip(Spec spec,out string trigger,out string effect,out string restriction)
    {
        trigger="Always"; restriction=string.Empty;
        effect=$"{spec.stat} {{value}}";
        switch(spec.kind)
        {
            case WeaponAffixRuntimeKind.ReloadBuff: trigger="After reloading ammo"; break;
            case WeaponAffixRuntimeKind.EchoChamber:
            case WeaponAffixRuntimeKind.BreachChamber: trigger=$"Every {Mathf.Max(1,spec.threshold)} consumed-ammo shots"; effect="Fires the configured bonus projectile"; break;
            case WeaponAffixRuntimeKind.KillClip: trigger="Direct kill"; effect="Damage +{value}%"; break;
            case WeaponAffixRuntimeKind.ExecutionFeed: trigger="Direct kill"; effect="Restore {value}% of max magazine"; break;
            case WeaponAffixRuntimeKind.LastRound: trigger="Consumed final round"; effect="Direct damage +25%; explosion deals {value}% pre-crit shot damage"; restriction="Base magazine 25+; once per attack"; break;
            case WeaponAffixRuntimeKind.HeadHunter: trigger="Damaging head hit"; effect="Crit Multiplier +{value} per stack"; restriction="Non-head hit resets stacks"; break;
            case WeaponAffixRuntimeKind.FreshChamber: trigger="Reload inserted ammo"; effect="Next 3 shots deal +{value}% damage"; restriction="Consumed-ammo shots only"; break;
            case WeaponAffixRuntimeKind.HotStreak: trigger="Direct kill"; effect="Fire Interval -{value}% per stack"; break;
            case WeaponAffixRuntimeKind.PressurePoint: trigger="Repeated hit on same target"; effect="Stagger Power +{value}% per stack"; break;
            case WeaponAffixRuntimeKind.ConservationRound: trigger="{value} confirmed hits"; effect="Restore 1 magazine round"; restriction="Progress persists while magazine is full"; break;
            case WeaponAffixRuntimeKind.Overkill: trigger="Kill with 20% max-health overkill"; effect="3m explosion for {value}% overkill"; restriction="Cannot recursively trigger affixes"; break;
            case WeaponAffixRuntimeKind.BrokenGuard: trigger="Cause ChainReady"; effect="Next shot has 100% Crit Rate and +{value} Crit Multiplier"; restriction="Consumed on shot; consumed-ammo only"; break;
            case WeaponAffixRuntimeKind.MarkedQuarry: trigger="Five hits on the same target"; effect="Deal +{value}% damage to that target"; break;
            case WeaponAffixRuntimeKind.BloodMagazine: trigger="Direct kill at 25% magazine or less"; effect="Restore {value}% max magazine and gain +15 Stability"; restriction="Once per attack"; break;
        }
    }

    [MenuItem("Tools/Weapons/Affixes/Validate (Dry Run)")]
    public static void ValidateMenu() { var errors=ValidateAll(); Debug.Log(errors.Count==0?"Weapon affix validation passed.":string.Join("\n",errors)); }
    public static List<string> ValidateAll()
    {
        var errors=new List<string>(); var db=AssetDatabase.LoadAssetAtPath<WeaponAffixDatabase>(DatabasePath); var ids=new HashSet<string>();
        if(db==null){errors.Add("Missing weapon affix database.");return errors;}
        foreach(var d in db.Affixes){if(d==null){errors.Add("Null definition.");continue;} if(!ids.Add(d.affixId))errors.Add($"Duplicate id: {d.affixId}"); if(d.rootBehavior==null)errors.Add($"Missing behavior: {d.affixId}"); else if(!d.rootBehavior.RollSpec.IsValid)errors.Add($"Invalid roll: {d.affixId}");}
        return errors;
    }
}

public sealed class WeaponAffixBuildValidator : IPreprocessBuildWithReport
{
    public int callbackOrder=>0;
    public void OnPreprocessBuild(BuildReport report){var errors=WeaponAffixMigrationTool.ValidateAll();if(errors.Count>0)throw new BuildFailedException(string.Join("\n",errors));}
}
#endif
