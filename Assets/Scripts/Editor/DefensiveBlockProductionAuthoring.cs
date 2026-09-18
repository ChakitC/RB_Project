#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class DefensiveBlockProductionAuthoring
{
    public const string Folder = "Assets/Data/DefensiveBlock";
    public const string SkillPath = "Assets/Data/Skills/Enemies/Rector/Rector_Skill_1.asset";
    public const string AiresPath = "Assets/Scripts/CharacterStats/Asosiation/ChaDef.Aires.asset";
    public const string RectorPath = "Assets/Prefab/GameEnemy/Enemy_B_GR_01 Variant.prefab";

    [MenuItem("Tools/RB/Defensive Block/Configure Production Assets")]
    public static void Configure()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Data", "DefensiveBlock");
        var attack = GetOrCreate<DefensiveBlockAttackProfile>("RectorCharge.asset");
        var aires = AssetDatabase.LoadAssetAtPath<CharacterStats>(AiresPath);
        // Follow the character binding so renaming the settings asset does not create a replacement.
        var actor = aires.defensiveBlock != null ? aires.defensiveBlock : GetOrCreate<DefensiveBlockActorProfile>("GuardSetting.asset");
        var animation = GetOrCreate<BlockAnimationProfile>("AiresBlockAnimation.asset");
        string recoilPath = "Assets/Animation/Ch_Aires/Aires_Block_Recoil.fbx";
        if (AssetDatabase.LoadMainAssetAtPath(recoilPath) == null &&
            !AssetDatabase.CopyAsset("Assets/Animation/Ch_Aires/Aires_Block_Root.Test.fbx", recoilPath))
            throw new InvalidOperationException("Cannot copy Aires recoil clip.");
        animation.beginClip = Clip("Assets/Animation/Ch_Aires/Aires_Block.fbx");
        animation.impactClip = Clip(recoilPath);
        actor.animation = animation;
        actor.impactVfx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/VFX/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Electric/CFXR Lightning Impact.prefab");
        int world = LayerMask.GetMask("Default", "Ground", "Ground Y", "Terrain", "Barrier");
        attack.worldLayers = world; actor.worldLayers = world;
        var skill = AssetDatabase.LoadAssetAtPath<SkillGemDefinition>(SkillPath);
        skill.defensiveBlock = attack; aires.defensiveBlock = actor;
        foreach (var asset in new UnityEngine.Object[] { attack, actor, animation, skill, aires }) EditorUtility.SetDirty(asset);
        // Move the authored presentation assets, preserving their GUIDs and existing references.
        foreach (string file in new[] { "BlockReadyFlare.shader", "BlockReadyFlare.mat", "BlockReadyFlare.prefab" })
        {
            string oldPath = DefensiveBlockTestSceneBuilder.Folder + "/" + file;
            if (AssetDatabase.LoadMainAssetAtPath(oldPath) != null)
            {
                string error = AssetDatabase.MoveAsset(oldPath, Folder + "/" + file);
                if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error);
            }
        }
        var cue = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/BlockReadyFlare.prefab");
        EditPrefab("Assets/Prefab/Player/Player.prefab", root =>
        {
            var ctx = root.GetComponentInChildren<PlayerContext>(true); ctx.ResolveReferences();
            ctx.interruptionCommand.defensiveBlockEnabled = true;
            var guard = ctx.GetComponent<DefensiveBlockController>() ?? ctx.gameObject.AddComponent<DefensiveBlockController>();
            guard.defaultSettings = actor;
            ctx.ResolveReferences();
            var view = ctx.GetComponent<DefensiveBlockReadyCue>() ?? ctx.gameObject.AddComponent<DefensiveBlockReadyCue>();
            view.cuePrefab = cue;
        });
        foreach (string path in new[] { "Assets/Prefab/Player/Ally_Stryker.prefab", "Assets/Prefab/Player/Ally_Helper.prefab" })
            EditPrefab(path, root =>
            {
                var ctx = root.GetComponentInChildren<AllyContext>(true);
                if (ctx.GetComponent<DefensiveBlockController>() == null) ctx.gameObject.AddComponent<DefensiveBlockController>();
                ctx.ResolveReferences();
            });
        EditPrefab(RectorPath, root =>
        {
            var ctx = root.GetComponentInChildren<EnemyContext>(true);
            ctx.baseStats = AssetDatabase.LoadAssetAtPath<CharacterStats>("Assets/Scripts/CharacterStats/Enemy/Stats_NB_GR_02_Rector.asset");
            if (ctx.GetComponent<DefensiveBlockAttack>() == null) ctx.gameObject.AddComponent<DefensiveBlockAttack>();
            ctx.ResolveReferences();
            if (ctx.KnockbackMotor != null)
            {
                var motor = new SerializedObject(ctx.KnockbackMotor);
                motor.FindProperty("collisionMask").intValue |= world;
                motor.ApplyModifiedPropertiesWithoutUndo();
            }
        });
        AssetDatabase.SaveAssets();
    }

    static T GetOrCreate<T>(string name) where T : ScriptableObject
    {
        string path = Folder + "/" + name;
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) { asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path); }
        return asset;
    }
    static AnimationClip Clip(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().First(c => !c.name.StartsWith("__"));
    static void EditPrefab(string path, Action<GameObject> edit)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try { edit(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
#endif
