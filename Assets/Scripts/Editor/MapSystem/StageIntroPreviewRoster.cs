using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Editor-only roster driving the stage intro preview: which character stands on each marker and an
/// optional clip override for trying a pose before committing it to <see cref="CharacterStats"/>.
///
/// This never touches the rig prefab. Character choices persist per rig asset in
/// <see cref="EditorPrefs"/>; clip overrides are deliberately volatile because they are a scratch
/// value, not authored data — the shipping clip always lives on <see cref="CharacterStats"/>.
/// </summary>
public sealed class StageIntroPreviewRoster
{
    public static readonly ChainActorRole[] Roles =
    {
        ChainActorRole.Player,
        ChainActorRole.PartySlot1,
        ChainActorRole.PartySlot2,
        ChainActorRole.Helper,
    };

    const string PrefKeyPrefix = "RB.StageIntroPreview.";

    public sealed class Slot
    {
        public ChainActorRole Role;
        public CharacterStats Character;
        public AnimationClip ClipOverride;
    }

    readonly List<Slot> slots = new();
    string rigKey;

    public IReadOnlyList<Slot> Slots => slots;

    public Slot GetSlot(ChainActorRole role)
    {
        for (int i = 0; i < slots.Count; i++)
            if (slots[i].Role == role)
                return slots[i];
        return null;
    }

    public static StageIntroPreviewRoster LoadFor(StageIntroRig rig)
    {
        var roster = new StageIntroPreviewRoster { rigKey = ResolveRigKey(rig) };

        for (int i = 0; i < Roles.Length; i++)
        {
            ChainActorRole role = Roles[i];
            var slot = new Slot { Role = role };

            string saved = EditorPrefs.GetString(roster.BuildKey(role), string.Empty);
            if (!string.IsNullOrEmpty(saved))
                slot.Character = LoadCharacterByGuid(saved);

            slot.Character ??= ResolveDefaultCharacter(role);
            roster.slots.Add(slot);
        }

        return roster;
    }

    public void Save()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            Slot slot = slots[i];
            string guid = slot.Character != null
                ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(slot.Character))
                : string.Empty;

            EditorPrefs.SetString(BuildKey(slot.Role), guid);
        }
    }

    /// <summary>
    /// Clip actually shown for a slot: the scratch override first, then the shipping
    /// <see cref="CharacterAnimProfileSO.stageIntroClip"/>, then the locomotion idle — the same
    /// fallback chain the runtime stage intro state uses.
    /// </summary>
    public AnimationClip ResolveClip(Slot slot)
    {
        if (slot == null)
            return null;

        if (slot.ClipOverride != null)
            return slot.ClipOverride;

        CharacterStats stats = slot.Character;
        CharacterAnimProfileSO profile = stats != null ? stats.animProfile : null;
        if (profile == null)
            return null;

        if (profile.HasStageIntroClip)
            return profile.stageIntroClip.Clip;

        return profile.locomotionDirectionalClips.idle;
    }

    string BuildKey(ChainActorRole role) => $"{PrefKeyPrefix}{rigKey}.{role}";

    static CharacterStats LoadCharacterByGuid(string guid)
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        return string.IsNullOrEmpty(path)
            ? null
            : AssetDatabase.LoadAssetAtPath<CharacterStats>(path);
    }

    /// <summary>Rig prefab GUID, so a roster survives closing and reopening the prefab.</summary>
    static string ResolveRigKey(StageIntroRig rig)
    {
        if (rig == null)
            return "unknown";

        PrefabStage stage = PrefabStageUtility.GetPrefabStage(rig.gameObject);
        if (stage != null && !string.IsNullOrEmpty(stage.assetPath))
            return AssetDatabase.AssetPathToGUID(stage.assetPath);

        Object source = PrefabUtility.GetCorrespondingObjectFromSource(rig);
        string sourcePath = source != null ? AssetDatabase.GetAssetPath(source) : string.Empty;
        if (!string.IsNullOrEmpty(sourcePath))
            return AssetDatabase.AssetPathToGUID(sourcePath);

        return rig.gameObject.name;
    }

    /// <summary>
    /// Default lineup comes from the shipping party: each actor prefab's
    /// <see cref="CharacterContextPartyLoader"/> fallback id resolved through its character database.
    /// This keeps the preview showing the real team without any per-user setup.
    /// </summary>
    static CharacterStats ResolveDefaultCharacter(ChainActorRole role)
    {
        var config = AssetDatabase.LoadAssetAtPath<PartySpawnConfigSO>(PartySpawnMigrationTool.ConfigPath);
        PartySpawnEntry entry = config != null ? config.GetMember(role) : null;
        if (entry == null || entry.Prefab == null)
            return null;

        var loader = entry.Prefab.GetComponentInChildren<CharacterContextPartyLoader>(true);
        if (loader == null)
            return null;

        if (loader.CurrentContext != null)
            return loader.CurrentContext;

        var serialized = new SerializedObject(loader);
        string fallbackId = serialized.FindProperty("fallbackId")?.stringValue;
        var database = serialized.FindProperty("db")?.objectReferenceValue as CharacterDatabase;

        if (database == null || string.IsNullOrWhiteSpace(fallbackId))
            return null;

        return database.GetById(fallbackId);
    }
}
