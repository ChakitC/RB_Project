#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public sealed class CharacterVoiceAudioCueGeneratorWindow : EditorWindow
{
    const string WindowTitle = "Character Voice AudioCue Generator";
    const string MenuPath = "Tools/RB Tools/Character Voice AudioCue Generator";
    const string DefaultSourceRoot = "Assets/AudioResult/Character Voice";
    const string FallbackSourceRoot = "Assets/Data/Audio/Character Voice";
    const string DefaultOutputRoot = "Assets/Data/Audio/Character Voice";

    string sourceRootPath = DefaultSourceRoot;
    string outputRootPath = DefaultOutputRoot;
    bool recursiveFolders = true;
    bool overwriteExistingCues = true;
    bool assignToCharacterStats = true;
    bool includeUnassignedEvents = true;

    AudioCueAudibilityMode voiceAudibilityMode = AudioCueAudibilityMode.Hybrid;
    float spatialBlend = 1f;
    float hybridSpatialBlend = 0.35f;
    float listenerVolumeBoost = 1.5f;
    float baseVolume = 1f;
    int priority = 96;
    float minDistance = 1f;
    float maxDistance = 25f;

    Vector2 scroll;
    string statusMessage;

    readonly List<PreviewItem> previewItems = new List<PreviewItem>();

    [MenuItem(MenuPath)]
    static void OpenWindow()
    {
        CharacterVoiceAudioCueGeneratorWindow window = GetWindow<CharacterVoiceAudioCueGeneratorWindow>(WindowTitle);
        window.minSize = new Vector2(620f, 460f);
        window.Show();
    }

    void OnEnable()
    {
        if (AssetDatabase.IsValidFolder(DefaultSourceRoot))
            sourceRootPath = DefaultSourceRoot;
        else if (AssetDatabase.IsValidFolder(FallbackSourceRoot))
            sourceRootPath = FallbackSourceRoot;

        if (!AssetDatabase.IsValidFolder(outputRootPath) && AssetDatabase.IsValidFolder(FallbackSourceRoot))
            outputRootPath = FallbackSourceRoot;

        RefreshPreview();
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("Scope", EditorStyles.boldLabel);
        DrawFolderField("Source Root", ref sourceRootPath);
        DrawFolderField("Output Root", ref outputRootPath);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Use Selection As Source", GUILayout.Height(22f)))
                UseSelectionAsFolder(ref sourceRootPath);

            if (GUILayout.Button("Use Selection As Output", GUILayout.Height(22f)))
                UseSelectionAsFolder(ref outputRootPath);
        }

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);
        recursiveFolders = EditorGUILayout.Toggle("Recursive Folders", recursiveFolders);
        overwriteExistingCues = EditorGUILayout.Toggle("Overwrite Existing Cues", overwriteExistingCues);
        assignToCharacterStats = EditorGUILayout.Toggle("Assign To CharacterStats", assignToCharacterStats);
        includeUnassignedEvents = EditorGUILayout.Toggle("Include Unassigned Events", includeUnassignedEvents);

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("AudioCue Defaults", EditorStyles.boldLabel);
        voiceAudibilityMode = (AudioCueAudibilityMode)EditorGUILayout.EnumPopup("Audibility Mode", voiceAudibilityMode);
        using (new EditorGUI.DisabledScope(voiceAudibilityMode == AudioCueAudibilityMode.Global2D))
            spatialBlend = EditorGUILayout.Slider("Spatial Blend", spatialBlend, 0f, 1f);
        using (new EditorGUI.DisabledScope(voiceAudibilityMode != AudioCueAudibilityMode.Hybrid))
            hybridSpatialBlend = EditorGUILayout.Slider("Hybrid Spatial Blend", hybridSpatialBlend, 0f, 1f);
        using (new EditorGUI.DisabledScope(voiceAudibilityMode == AudioCueAudibilityMode.Global2D))
            listenerVolumeBoost = Mathf.Max(0f, EditorGUILayout.FloatField("Listener Volume Boost", listenerVolumeBoost));
        baseVolume = Mathf.Max(0f, EditorGUILayout.FloatField("Base Volume", baseVolume));
        priority = EditorGUILayout.IntSlider("Priority", priority, 0, 256);
        minDistance = Mathf.Max(0f, EditorGUILayout.FloatField("Min Distance", minDistance));
        maxDistance = Mathf.Max(minDistance, EditorGUILayout.FloatField("Max Distance", maxDistance));

        EditorGUILayout.Space(8f);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Preview", GUILayout.Height(28f)))
                RefreshPreview();

            using (new EditorGUI.DisabledScope(previewItems.Count == 0 || HasBlockingItems()))
            {
                if (GUILayout.Button("Generate / Update AudioCue", GUILayout.Height(28f)))
                    GenerateAudioCues();
            }
        }

        if (!string.IsNullOrEmpty(statusMessage))
            EditorGUILayout.HelpBox(statusMessage, MessageType.Info);

        EditorGUILayout.Space(6f);
        DrawPreview();
    }

    void DrawFolderField(string label, ref string path)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            DefaultAsset current = AssetDatabase.LoadAssetAtPath<DefaultAsset>(path);
            DefaultAsset selected = (DefaultAsset)EditorGUILayout.ObjectField(label, current, typeof(DefaultAsset), false);
            if (selected != current)
            {
                string selectedPath = AssetDatabase.GetAssetPath(selected);
                if (AssetDatabase.IsValidFolder(selectedPath))
                    path = selectedPath;
            }

            if (GUILayout.Button("Browse", GUILayout.Width(72f)))
            {
                string absolute = EditorUtility.OpenFolderPanel(label, Application.dataPath, string.Empty);
                string assetPath = AbsoluteToAssetPath(absolute);
                if (AssetDatabase.IsValidFolder(assetPath))
                    path = assetPath;
            }
        }

        path = NormalizeAssetPath(EditorGUILayout.DelayedTextField(label + " Path", path));
    }

    void UseSelectionAsFolder(ref string path)
    {
        UnityEngine.Object selected = Selection.activeObject;
        if (selected == null)
            return;

        string selectedPath = AssetDatabase.GetAssetPath(selected);
        if (AssetDatabase.IsValidFolder(selectedPath))
        {
            path = selectedPath;
            RefreshPreview();
        }
    }

    void RefreshPreview()
    {
        previewItems.Clear();
        statusMessage = string.Empty;

        sourceRootPath = NormalizeAssetPath(sourceRootPath);
        outputRootPath = NormalizeAssetPath(outputRootPath);

        if (!AssetDatabase.IsValidFolder(sourceRootPath))
        {
            statusMessage = "Source root is not a valid Assets folder.";
            return;
        }

        if (string.IsNullOrEmpty(outputRootPath) || !outputRootPath.StartsWith("Assets", StringComparison.Ordinal))
        {
            statusMessage = "Output root must be inside Assets.";
            return;
        }

        CharacterStatsLookup statsLookup = new CharacterStatsLookup();
        SkillGemLookup skillLookup = new SkillGemLookup();
        Dictionary<string, PreviewItem> groups = new Dictionary<string, PreviewItem>(StringComparer.OrdinalIgnoreCase);

        string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { sourceRootPath });
        for (int i = 0; i < guids.Length; i++)
        {
            string clipPath = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (!ShouldIncludeClipPath(sourceRootPath, clipPath, recursiveFolders))
                continue;

            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
            if (clip == null)
                continue;

            VoiceClipInfo clipInfo = VoiceClipInfo.FromPath(sourceRootPath, clipPath);
            if (clipInfo == null)
                continue;

            if (!includeUnassignedEvents && clipInfo.Kind == VoiceCueKind.Unassigned)
                continue;

            string key = clipInfo.GetGroupKey();
            PreviewItem item;
            if (!groups.TryGetValue(key, out item))
            {
                item = new PreviewItem(clipInfo);
                groups.Add(key, item);
            }

            item.AddClip(clip, clipPath);
        }

        foreach (PreviewItem item in groups.Values)
        {
            item.SortClips();
            item.TargetPath = BuildTargetPath(outputRootPath, item);
            item.ExistingCue = AssetDatabase.LoadAssetAtPath<AudioCue>(item.TargetPath);
            item.Stats = statsLookup.FindBestMatch(item.CharacterName, out item.StatsWarning);

            if (item.Kind == VoiceCueKind.SkillLine)
                item.Skill = skillLookup.FindBestMatch(item.SkillName, out item.SkillWarning);

            if (item.Clips.Count == 0)
                item.BlockReason = "No AudioClip was found for this group.";
            else if (item.Kind == VoiceCueKind.SkillLine && item.Skill == null)
                item.AssignWarning = "Skill line cue will be created, but it cannot be assigned until a matching SkillGemDefinition exists.";
            else if (assignToCharacterStats && item.CanAssignToStats && item.Stats == null)
                item.AssignWarning = "Cue will be created, but no matching CharacterStats asset was found.";

            previewItems.Add(item);
        }

        previewItems.Sort(ComparePreviewItems);
        statusMessage = string.Format(
            "Found {0} AudioClip(s), grouped into {1} AudioCue asset(s).",
            CountClips(previewItems),
            previewItems.Count);
    }

    void GenerateAudioCues()
    {
        if (previewItems.Count == 0)
            RefreshPreview();

        int createdCount = 0;
        int updatedCount = 0;
        int reusedCount = 0;
        int assignedCount = 0;

        try
        {
            for (int i = 0; i < previewItems.Count; i++)
            {
                PreviewItem item = previewItems[i];
                if (!string.IsNullOrEmpty(item.BlockReason))
                    continue;

                EnsureAssetFolder(GetParentAssetPath(item.TargetPath));

                bool created;
                AudioCue cue = GetOrCreateCue(item.TargetPath, out created);
                if (cue == null)
                    continue;

                if (created)
                {
                    ApplyCueDefaults(cue, item);
                    EditorUtility.SetDirty(cue);
                    createdCount++;
                }
                else if (overwriteExistingCues)
                {
                    ApplyCueDefaults(cue, item);
                    EditorUtility.SetDirty(cue);
                    updatedCount++;
                }
                else
                {
                    reusedCount++;
                }

                if (assignToCharacterStats && AssignCueToStats(item, cue))
                    assignedCount++;
            }
        }
        finally
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            RefreshPreview();
        }

        statusMessage = string.Format(
            "Created {0}, updated {1}, reused {2}, assigned {3}.",
            createdCount,
            updatedCount,
            reusedCount,
            assignedCount);
    }

    AudioCue GetOrCreateCue(string targetPath, out bool created)
    {
        created = false;

        AudioCue cue = AssetDatabase.LoadAssetAtPath<AudioCue>(targetPath);
        if (cue != null)
            return cue;

        cue = CreateInstance<AudioCue>();
        AssetDatabase.CreateAsset(cue, targetPath);
        created = true;
        return cue;
    }

    void ApplyCueDefaults(AudioCue cue, PreviewItem item)
    {
        cue.cueId = BuildCueId(item);
        cue.category = AudioCategory.Voice;
        cue.audibilityMode = voiceAudibilityMode;
        cue.loop = false;
        cue.spatialBlend = voiceAudibilityMode == AudioCueAudibilityMode.Global2D ? 0f : spatialBlend;
        cue.hybridSpatialBlend = hybridSpatialBlend;
        cue.listenerVolumeBoost = voiceAudibilityMode == AudioCueAudibilityMode.Global2D ? 1f : listenerVolumeBoost;
        cue.followTarget = false;
        cue.stereoPan = 0f;
        cue.priority = priority;
        cue.minDistance = minDistance;
        cue.maxDistance = maxDistance;
        cue.dopplerLevel = 1f;
        cue.rolloffMode = AudioRolloffMode.Logarithmic;
        cue.baseVolume = baseVolume;
        cue.basePitch = 1f;
        cue.randomVolumeMultiplier = Vector2.one;
        cue.randomPitchMultiplier = Vector2.one;
        cue.avoidImmediateRepeats = true;
        cue.cooldown = 0f;
        cue.maxInstances = 0;
        cue.stopOldestInstanceWhenLimitReached = false;

        if (cue.variations == null)
            cue.variations = new List<AudioCue.Variation>();

        cue.variations.Clear();
        for (int i = 0; i < item.Clips.Count; i++)
        {
            cue.variations.Add(new AudioCue.Variation
            {
                clip = item.Clips[i],
                weight = 1f,
                volumeMultiplier = 1f,
                pitchMultiplier = 1f
            });
        }
    }

    static bool AssignCueToStats(PreviewItem item, AudioCue cue)
    {
        if (item == null || cue == null || item.Stats == null || !item.CanAssignToStats)
            return false;

        Undo.RecordObject(item.Stats, "Assign Character Voice AudioCue");

        if (item.Stats.voiceProfile == null)
            item.Stats.voiceProfile = new CharacterVoiceProfile();

        CharacterVoiceProfile profile = item.Stats.voiceProfile;

        switch (item.Kind)
        {
            case VoiceCueKind.DefaultSkill:
                profile.defaultSkillVoiceCue = cue;
                break;

            case VoiceCueKind.SkillLine:
                if (item.Skill == null)
                    return false;
                AssignSkillLine(profile, item.Skill, cue);
                break;

            case VoiceCueKind.Dash:
                if (profile.dashVoice == null)
                    profile.dashVoice = new CharacterEventVoiceLine();
                profile.dashVoice.cue = cue;
                break;

            case VoiceCueKind.Knockback:
                if (profile.knockbackVoice == null)
                    profile.knockbackVoice = new CharacterEventVoiceLine();
                profile.knockbackVoice.cue = cue;
                break;

            case VoiceCueKind.SelectCharacter:
                if (profile.selectCharacterVoice == null)
                    profile.selectCharacterVoice = new CharacterEventVoiceLine();
                profile.selectCharacterVoice.cue = cue;
                break;

            case VoiceCueKind.PickupCharacter:
                if (profile.pickupCharacterVoice == null)
                    profile.pickupCharacterVoice = new CharacterEventVoiceLine();
                profile.pickupCharacterVoice.cue = cue;
                break;

            case VoiceCueKind.LowHp:
                if (profile.lowHpVoice == null)
                    profile.lowHpVoice = new CharacterEventVoiceLine();
                profile.lowHpVoice.cue = cue;
                break;

            case VoiceCueKind.Damaged:
                item.Stats.damagedCue = cue;
                break;

            case VoiceCueKind.Down:
                item.Stats.downCue = cue;
                break;

            case VoiceCueKind.Death:
                item.Stats.deathCue = cue;
                break;

            case VoiceCueKind.Revive:
                item.Stats.reviveCue = cue;
                break;

            default:
                return false;
        }

        EditorUtility.SetDirty(item.Stats);
        return true;
    }

    static void AssignSkillLine(CharacterVoiceProfile profile, SkillGemDefinition skill, AudioCue cue)
    {
        if (profile.skillCastLines == null)
            profile.skillCastLines = new List<CharacterSkillVoiceLine>();

        for (int i = 0; i < profile.skillCastLines.Count; i++)
        {
            CharacterSkillVoiceLine existing = profile.skillCastLines[i];
            if (existing == null)
                continue;

            if (existing.skill == skill)
            {
                existing.cue = cue;
                return;
            }
        }

        profile.skillCastLines.Add(new CharacterSkillVoiceLine
        {
            skill = skill,
            cue = cue,
            chance = 1f,
            cooldown = 0f
        });
    }

    void DrawPreview()
    {
        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

        if (previewItems.Count == 0)
        {
            EditorGUILayout.HelpBox("No AudioClip groups found in the current source root.", MessageType.Info);
            return;
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);
        for (int i = 0; i < previewItems.Count; i++)
        {
            PreviewItem item = previewItems[i];
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(item.DisplayTitle, EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Clips", item.Clips.Count.ToString());
            EditorGUILayout.LabelField("Target", item.TargetPath);
            EditorGUILayout.LabelField("Stats", item.Stats != null ? AssetDatabase.GetAssetPath(item.Stats) : "None");
            if (item.Skill != null)
                EditorGUILayout.LabelField("Skill", AssetDatabase.GetAssetPath(item.Skill));
            if (item.ExistingCue != null)
                EditorGUILayout.LabelField("Existing", overwriteExistingCues ? "Will update" : "Will reuse");
            if (!item.CanAssignToStats)
                EditorGUILayout.HelpBox("AudioCue will be created, but this event has no CharacterStats field to assign to.", MessageType.None);
            if (!string.IsNullOrEmpty(item.StatsWarning))
                EditorGUILayout.HelpBox(item.StatsWarning, MessageType.Warning);
            if (!string.IsNullOrEmpty(item.SkillWarning))
                EditorGUILayout.HelpBox(item.SkillWarning, MessageType.Warning);
            if (!string.IsNullOrEmpty(item.AssignWarning))
                EditorGUILayout.HelpBox(item.AssignWarning, MessageType.Warning);
            if (!string.IsNullOrEmpty(item.BlockReason))
                EditorGUILayout.HelpBox(item.BlockReason, MessageType.Error);
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndScrollView();
    }

    bool HasBlockingItems()
    {
        for (int i = 0; i < previewItems.Count; i++)
        {
            if (!string.IsNullOrEmpty(previewItems[i].BlockReason))
                return true;
        }

        return false;
    }

    static bool ShouldIncludeClipPath(string sourceRoot, string clipPath, bool recursive)
    {
        if (recursive)
            return true;

        string relative = GetRelativePath(sourceRoot, clipPath);
        if (string.IsNullOrEmpty(relative))
            return false;

        string[] parts = relative.Split('/');
        return parts.Length <= 2;
    }

    static int ComparePreviewItems(PreviewItem left, PreviewItem right)
    {
        int characterCompare = string.Compare(left.CharacterName, right.CharacterName, StringComparison.OrdinalIgnoreCase);
        if (characterCompare != 0)
            return characterCompare;

        return string.Compare(left.CueName, right.CueName, StringComparison.OrdinalIgnoreCase);
    }

    static int CountClips(List<PreviewItem> items)
    {
        int count = 0;
        for (int i = 0; i < items.Count; i++)
            count += items[i].Clips.Count;
        return count;
    }

    static string BuildTargetPath(string outputRoot, PreviewItem item)
    {
        string characterFolder = SanitizeAssetName(item.CharacterName);
        string fileName = SanitizeAssetName(item.CharacterName + "_" + item.CueName + "_AudioCue") + ".asset";
        return CombineAssetPath(CombineAssetPath(outputRoot, characterFolder), fileName);
    }

    static string BuildCueId(PreviewItem item)
    {
        return string.Format(
            "voice.{0}.{1}",
            CanonicalToken(item.CharacterName),
            CanonicalToken(item.CueName));
    }

    static void EnsureAssetFolder(string folderPath)
    {
        folderPath = NormalizeAssetPath(folderPath);
        if (AssetDatabase.IsValidFolder(folderPath))
            return;

        string[] parts = folderPath.Split('/');
        if (parts.Length == 0 || parts[0] != "Assets")
            return;

        string current = "Assets";
        for (int i = 1; i < parts.Length; i++)
        {
            string next = CombineAssetPath(current, parts[i]);
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    static string AbsoluteToAssetPath(string absolutePath)
    {
        if (string.IsNullOrEmpty(absolutePath))
            return string.Empty;

        string normalized = NormalizeSlashes(absolutePath);
        string dataPath = NormalizeSlashes(Application.dataPath);

        if (normalized.Equals(dataPath, StringComparison.OrdinalIgnoreCase))
            return "Assets";

        if (normalized.StartsWith(dataPath + "/", StringComparison.OrdinalIgnoreCase))
            return "Assets" + normalized.Substring(dataPath.Length);

        int assetsIndex = normalized.IndexOf("/Assets/", StringComparison.OrdinalIgnoreCase);
        if (assetsIndex >= 0)
            return normalized.Substring(assetsIndex + 1);

        return string.Empty;
    }

    static string NormalizeAssetPath(string path)
    {
        path = NormalizeSlashes(path);
        while (path.EndsWith("/", StringComparison.Ordinal))
            path = path.Substring(0, path.Length - 1);
        return path;
    }

    static string NormalizeSlashes(string value)
    {
        return string.IsNullOrEmpty(value) ? string.Empty : value.Replace('\\', '/').Trim();
    }

    static string GetRelativePath(string root, string path)
    {
        root = NormalizeAssetPath(root);
        path = NormalizeAssetPath(path);

        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        string relative = path.Substring(root.Length);
        return relative.TrimStart('/');
    }

    static string GetParentAssetPath(string assetPath)
    {
        string parent = Path.GetDirectoryName(assetPath);
        return string.IsNullOrEmpty(parent) ? string.Empty : parent.Replace('\\', '/');
    }

    static string CombineAssetPath(string parent, string child)
    {
        if (string.IsNullOrEmpty(parent))
            return child;

        if (string.IsNullOrEmpty(child))
            return parent;

        return parent.TrimEnd('/') + "/" + child.TrimStart('/');
    }

    static string SanitizeAssetName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Unnamed";

        string sanitized = Regex.Replace(value.Trim(), @"[^\w\-. ]+", "_");
        sanitized = Regex.Replace(sanitized, @"\s+", "_");
        sanitized = sanitized.Trim('_', '.', '-');
        return string.IsNullOrEmpty(sanitized) ? "Unnamed" : sanitized;
    }

    static string CanonicalToken(string value)
    {
        string canonical = Canonical(value);
        return string.IsNullOrEmpty(canonical) ? "unknown" : canonical;
    }

    static string Canonical(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return Regex.Replace(value.ToLowerInvariant(), @"[^a-z0-9]+", string.Empty);
    }

    enum VoiceCueKind
    {
        DefaultSkill,
        SkillLine,
        Dash,
        Knockback,
        SelectCharacter,
        PickupCharacter,
        LowHp,
        Damaged,
        Down,
        Death,
        Revive,
        Unassigned
    }

    sealed class PreviewItem
    {
        public readonly string CharacterName;
        public readonly VoiceCueKind Kind;
        public readonly string CueName;
        public readonly string SkillName;
        public readonly List<AudioClip> Clips = new List<AudioClip>();
        public readonly List<string> ClipPaths = new List<string>();

        public string TargetPath;
        public string BlockReason;
        public string AssignWarning;
        public string StatsWarning;
        public string SkillWarning;
        public AudioCue ExistingCue;
        public CharacterStats Stats;
        public SkillGemDefinition Skill;

        public PreviewItem(VoiceClipInfo clipInfo)
        {
            CharacterName = clipInfo.CharacterName;
            Kind = clipInfo.Kind;
            CueName = clipInfo.CueName;
            SkillName = clipInfo.SkillName;
        }

        public bool CanAssignToStats
        {
            get { return Kind != VoiceCueKind.Unassigned; }
        }

        public string DisplayTitle
        {
            get { return string.Format("{0} / {1}", CharacterName, CueName); }
        }

        public void AddClip(AudioClip clip, string path)
        {
            Clips.Add(clip);
            ClipPaths.Add(path);
        }

        public void SortClips()
        {
            List<ClipEntry> entries = new List<ClipEntry>();
            for (int i = 0; i < Clips.Count; i++)
                entries.Add(new ClipEntry(Clips[i], ClipPaths[i]));

            entries.Sort((left, right) => string.Compare(left.Path, right.Path, StringComparison.OrdinalIgnoreCase));

            Clips.Clear();
            ClipPaths.Clear();
            for (int i = 0; i < entries.Count; i++)
            {
                Clips.Add(entries[i].Clip);
                ClipPaths.Add(entries[i].Path);
            }
        }

        struct ClipEntry
        {
            public readonly AudioClip Clip;
            public readonly string Path;

            public ClipEntry(AudioClip clip, string path)
            {
                Clip = clip;
                Path = path;
            }
        }
    }

    sealed class VoiceClipInfo
    {
        public string CharacterName;
        public VoiceCueKind Kind;
        public string CueName;
        public string SkillName;

        public string GetGroupKey()
        {
            return string.Format("{0}|{1}|{2}|{3}", CharacterName, Kind, CueName, SkillName);
        }

        public static VoiceClipInfo FromPath(string sourceRoot, string clipPath)
        {
            string relative = GetRelativePath(sourceRoot, clipPath);
            if (string.IsNullOrEmpty(relative))
                return null;

            string[] parts = relative.Split('/');
            string characterName = parts.Length > 1
                ? CleanDisplayName(parts[0])
                : ExtractCharacterName(Path.GetFileNameWithoutExtension(clipPath));

            if (string.IsNullOrEmpty(characterName))
                characterName = "Unknown";

            string rawLineName = GetRawLineName(parts, characterName, clipPath);
            if (string.IsNullOrEmpty(rawLineName))
                rawLineName = Path.GetFileNameWithoutExtension(clipPath);

            return Create(characterName, rawLineName);
        }

        static VoiceClipInfo Create(string characterName, string rawLineName)
        {
            rawLineName = TrimTrailingVariationToken(rawLineName);
            string canonical = Canonical(rawLineName);

            VoiceClipInfo info = new VoiceClipInfo
            {
                CharacterName = characterName,
                Kind = VoiceCueKind.Unassigned,
                CueName = ToCueName(rawLineName),
                SkillName = string.Empty
            };

            if (ContainsAny(canonical, "lowhp", "lowhealth", "lowlife"))
            {
                info.Kind = VoiceCueKind.LowHp;
                info.CueName = "LowHp";
            }
            else if (ContainsAny(canonical, "knockback", "knock"))
            {
                info.Kind = VoiceCueKind.Knockback;
                info.CueName = "Knockback";
            }
            else if (ContainsAny(canonical, "pickup", "pickcharacter", "pick"))
            {
                info.Kind = VoiceCueKind.PickupCharacter;
                info.CueName = "Pickup";
            }
            else if (ContainsAny(canonical, "select", "selectcharacter"))
            {
                info.Kind = VoiceCueKind.SelectCharacter;
                info.CueName = "Select";
            }
            else if (ContainsAny(canonical, "dash", "dodge"))
            {
                info.Kind = VoiceCueKind.Dash;
                info.CueName = "Dash";
            }
            else if (ContainsAny(canonical, "takedamage", "damaged", "damage", "hurt", "hit"))
            {
                info.Kind = VoiceCueKind.Damaged;
                info.CueName = "Damaged";
            }
            else if (ContainsAny(canonical, "revive", "revival"))
            {
                info.Kind = VoiceCueKind.Revive;
                info.CueName = "Revive";
            }
            else if (ContainsAny(canonical, "death", "dead", "die"))
            {
                info.Kind = VoiceCueKind.Death;
                info.CueName = "Death";
            }
            else if (ContainsAny(canonical, "down", "downed"))
            {
                info.Kind = VoiceCueKind.Down;
                info.CueName = "Down";
            }
            else if (ContainsAny(canonical, "defaultskill", "skilldefault") || canonical == "skill")
            {
                info.Kind = VoiceCueKind.DefaultSkill;
                info.CueName = "SkillDefault";
            }
            else if (canonical.StartsWith("skill", StringComparison.Ordinal))
            {
                string skillName = ExtractSkillName(rawLineName);
                if (string.IsNullOrEmpty(skillName))
                {
                    info.Kind = VoiceCueKind.DefaultSkill;
                    info.CueName = "SkillDefault";
                }
                else
                {
                    info.Kind = VoiceCueKind.SkillLine;
                    info.SkillName = skillName;
                    info.CueName = "Skill_" + ToCueName(skillName);
                }
            }

            return info;
        }

        static string GetRawLineName(string[] parts, string characterName, string clipPath)
        {
            string fileName = Path.GetFileNameWithoutExtension(clipPath);
            string fromFile = TrimTrailingVariationToken(StripCharacterPrefix(fileName, characterName));

            if (!IsVariationOnlyName(fromFile))
                return fromFile;

            if (parts.Length > 2)
                return parts[1];

            return fromFile;
        }

        static string ExtractCharacterName(string fileName)
        {
            int separatorIndex = fileName.IndexOfAny(new[] { '_', '-', ' ' });
            if (separatorIndex <= 0)
                return CleanDisplayName(fileName);

            return CleanDisplayName(fileName.Substring(0, separatorIndex));
        }

        static string StripCharacterPrefix(string fileName, string characterName)
        {
            if (string.IsNullOrEmpty(fileName) || string.IsNullOrEmpty(characterName))
                return fileName;

            if (fileName.Length <= characterName.Length)
                return fileName;

            if (!fileName.StartsWith(characterName, StringComparison.OrdinalIgnoreCase))
                return fileName;

            char next = fileName[characterName.Length];
            if (next != '_' && next != '-' && next != ' ' && next != '.')
                return fileName;

            return fileName.Substring(characterName.Length + 1).Trim();
        }

        static string TrimTrailingVariationToken(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            string current = value.Trim();
            while (true)
            {
                string next = Regex.Replace(current, @"(?:[\s_\-.]+(?:take|line|var|v)?\d+)$", string.Empty, RegexOptions.IgnoreCase);
                if (next == current || string.IsNullOrWhiteSpace(next))
                    return current.Trim();

                current = next.Trim();
            }
        }

        static bool IsVariationOnlyName(string value)
        {
            return string.IsNullOrWhiteSpace(value) || Regex.IsMatch(value.Trim(), @"^(?:take|line|var|v)?\d+$", RegexOptions.IgnoreCase);
        }

        static string ExtractSkillName(string rawLineName)
        {
            string value = Regex.Replace(rawLineName, @"^skill(?:[\s_\-.]+cast)?[\s_\-.]*", string.Empty, RegexOptions.IgnoreCase);
            value = TrimTrailingVariationToken(value);
            return ToCueName(value);
        }

        static string ToCueName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Unknown";

            string normalized = Regex.Replace(value.Trim(), @"[_\-.]+", " ");
            normalized = Regex.Replace(normalized, @"\s+", " ");
            TextInfoBuilder builder = new TextInfoBuilder(normalized);
            return builder.ToPascalCase();
        }

        static string CleanDisplayName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return Regex.Replace(value.Trim(), @"\s+", " ");
        }

        static bool ContainsAny(string value, params string[] candidates)
        {
            for (int i = 0; i < candidates.Length; i++)
            {
                if (value.Contains(candidates[i]))
                    return true;
            }

            return false;
        }
    }

    sealed class CharacterStatsLookup
    {
        readonly List<CharacterStatsEntry> entries = new List<CharacterStatsEntry>();

        public CharacterStatsLookup()
        {
            string[] guids = AssetDatabase.FindAssets("t:CharacterStats");
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                CharacterStats stats = AssetDatabase.LoadAssetAtPath<CharacterStats>(path);
                if (stats != null)
                    entries.Add(new CharacterStatsEntry(stats, path));
            }
        }

        public CharacterStats FindBestMatch(string characterName, out string warning)
        {
            warning = string.Empty;
            string target = Canonical(characterName);
            if (string.IsNullOrEmpty(target))
                return null;

            CharacterStatsEntry best = null;
            int bestScore = 0;
            int tieCount = 0;

            for (int i = 0; i < entries.Count; i++)
            {
                int score = entries[i].GetScore(target);
                if (score <= 0)
                    continue;

                if (score > bestScore)
                {
                    best = entries[i];
                    bestScore = score;
                    tieCount = 1;
                }
                else if (score == bestScore)
                {
                    tieCount++;
                }
            }

            if (best == null)
                return null;

            if (tieCount > 1)
                warning = "Multiple CharacterStats assets matched this character. The highest-priority path was selected.";

            return best.Stats;
        }

        sealed class CharacterStatsEntry
        {
            public readonly CharacterStats Stats;
            readonly string path;

            public CharacterStatsEntry(CharacterStats stats, string path)
            {
                Stats = stats;
                this.path = path;
            }

            public int GetScore(string target)
            {
                int score = 0;
                if (Canonical(Stats.characterName) == target)
                    score += 100;
                if (Canonical(StripIdPrefix(Stats.characterId)) == target)
                    score += 80;
                if (Canonical(Stats.name).Contains(target))
                    score += 20;
                if (Canonical(path).Contains(target))
                    score += 5;

                if (score == 0)
                    return 0;

                if (path.StartsWith("Assets/Character/", StringComparison.OrdinalIgnoreCase))
                    score += 30;
                if (path.StartsWith("Assets/Scripts/CharacterStats/Asosiation/", StringComparison.OrdinalIgnoreCase))
                    score += 20;
                return score;
            }

            static string StripIdPrefix(string value)
            {
                if (string.IsNullOrEmpty(value))
                    return string.Empty;

                return value.StartsWith("ID.", StringComparison.OrdinalIgnoreCase)
                    ? value.Substring(3)
                    : value;
            }
        }
    }

    sealed class SkillGemLookup
    {
        readonly List<SkillGemDefinition> skills = new List<SkillGemDefinition>();

        public SkillGemLookup()
        {
            string[] guids = AssetDatabase.FindAssets("t:SkillGemDefinition");
            for (int i = 0; i < guids.Length; i++)
            {
                SkillGemDefinition skill = AssetDatabase.LoadAssetAtPath<SkillGemDefinition>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (skill != null)
                    skills.Add(skill);
            }
        }

        public SkillGemDefinition FindBestMatch(string skillName, out string warning)
        {
            warning = string.Empty;
            string target = Canonical(skillName);
            if (string.IsNullOrEmpty(target))
                return null;

            SkillGemDefinition best = null;
            int bestScore = 0;
            int tieCount = 0;

            for (int i = 0; i < skills.Count; i++)
            {
                int score = GetScore(skills[i], target);
                if (score <= 0)
                    continue;

                if (score > bestScore)
                {
                    best = skills[i];
                    bestScore = score;
                    tieCount = 1;
                }
                else if (score == bestScore)
                {
                    tieCount++;
                }
            }

            if (tieCount > 1)
                warning = "Multiple SkillGemDefinition assets matched this skill name. The highest-priority asset was selected.";

            return best;
        }

        static int GetScore(SkillGemDefinition skill, string target)
        {
            int score = 0;
            if (Canonical(skill.displayName) == target)
                score += 100;
            if (Canonical(skill.skillId) == target)
                score += 90;
            if (Canonical(skill.name) == target)
                score += 80;
            if (Canonical(skill.name).Contains(target))
                score += 10;
            return score;
        }
    }

    struct TextInfoBuilder
    {
        readonly string value;

        public TextInfoBuilder(string value)
        {
            this.value = value;
        }

        public string ToPascalCase()
        {
            string[] parts = value.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return "Unknown";

            string result = string.Empty;
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i];
                if (part.Length == 0)
                    continue;

                result += char.ToUpperInvariant(part[0]);
                if (part.Length > 1)
                    result += part.Substring(1);
            }

            return string.IsNullOrEmpty(result) ? "Unknown" : result;
        }
    }
}
#endif
