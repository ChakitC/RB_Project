#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public sealed class BulkAssetRenamerWindow : EditorWindow
{
    const string WindowTitle = "Bulk Asset Renamer";
    const string MenuPath = "Tools/RB Tools/Bulk Asset Renamer";
    const string ContextMenuPath = "Assets/RB Tools/Rename HMG to Feno";

    RenameRuleMode ruleMode = RenameRuleMode.ReplaceText;
    string findText = "HMG";
    string replaceText = "Feno";
    string cutText = "_remap";
    int cutStartCharacters;
    int cutEndCharacters;
    bool recursiveFolders = true;
    bool includeFolders;
    bool caseSensitive = true;
    Vector2 scroll;

    readonly List<string> scopePaths = new List<string>();
    readonly List<RenamePreviewItem> previewItems = new List<RenamePreviewItem>();

    [MenuItem(MenuPath)]
    static void OpenWindow()
    {
        BulkAssetRenamerWindow window = GetWindow<BulkAssetRenamerWindow>(WindowTitle);
        window.minSize = new Vector2(520f, 360f);
        window.LoadSelection();
        window.Show();
    }

    [MenuItem(ContextMenuPath, false, 2200)]
    static void RenameHmgToFenoFromContextMenu()
    {
        List<string> paths = GetSelectedAssetPaths();
        List<RenamePreviewItem> items = BuildPreview(paths, RenameRuleMode.ReplaceText, "HMG", "Feno", 0, 0, true, false, true);

        if (items.Count == 0)
        {
            EditorUtility.DisplayDialog("Rename HMG to Feno", "No selected assets contain 'HMG'.", "OK");
            return;
        }

        int blockedCount = CountBlockedItems(items);
        string message = blockedCount > 0
            ? string.Format("Found {0} rename candidates, but {1} have conflicts or errors. Open the tool window to review them.", items.Count, blockedCount)
            : string.Format("Rename {0} selected assets from HMG to Feno?", items.Count);

        if (blockedCount > 0)
        {
            EditorUtility.DisplayDialog("Rename HMG to Feno", message, "OK");
            OpenWindow();
            return;
        }

        if (!EditorUtility.DisplayDialog("Rename HMG to Feno", message, "Rename", "Cancel"))
            return;

        ApplyRenames(items);
    }

    [MenuItem(ContextMenuPath, true)]
    static bool ValidateRenameHmgToFenoFromContextMenu()
    {
        return Selection.GetFiltered<UnityEngine.Object>(SelectionMode.Assets).Length > 0;
    }

    void OnEnable()
    {
        if (scopePaths.Count == 0)
            LoadSelection();
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("Scope", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Use Current Project Selection", GUILayout.Height(24f)))
                LoadSelection();

            if (GUILayout.Button("Clear", GUILayout.Width(80f), GUILayout.Height(24f)))
            {
                scopePaths.Clear();
                previewItems.Clear();
            }
        }

        if (scopePaths.Count == 0)
        {
            EditorGUILayout.HelpBox("Select one or more assets or folders in the Project window, then click Use Current Project Selection.", MessageType.Info);
        }
        else
        {
            EditorGUILayout.HelpBox(string.Format("{0} selected path(s). Folder contents are included when Recursive Folders is enabled.", scopePaths.Count), MessageType.None);
        }

        EditorGUILayout.Space(6f);
        EditorGUILayout.LabelField("Rename Rule", EditorStyles.boldLabel);
        ruleMode = (RenameRuleMode)EditorGUILayout.EnumPopup("Mode", ruleMode);
        DrawRuleFields();

        recursiveFolders = EditorGUILayout.Toggle("Recursive Folders", recursiveFolders);
        includeFolders = EditorGUILayout.Toggle("Include Folder Names", includeFolders);

        using (new EditorGUI.DisabledScope(!CanPreview()))
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Preview", GUILayout.Height(28f)))
                RefreshPreview();

            using (new EditorGUI.DisabledScope(previewItems.Count == 0 || CountBlockedItems(previewItems) > 0))
            {
                if (GUILayout.Button("Rename", GUILayout.Height(28f)))
                {
                    if (EditorUtility.DisplayDialog("Bulk Rename", string.Format("Rename {0} asset(s)?", previewItems.Count), "Rename", "Cancel"))
                    {
                        ApplyRenames(previewItems);
                        RefreshPreview();
                    }
                }
            }
        }

        EditorGUILayout.Space(6f);
        DrawPreview();
    }

    void LoadSelection()
    {
        scopePaths.Clear();
        scopePaths.AddRange(GetSelectedAssetPaths());
        RefreshPreview();
    }

    void RefreshPreview()
    {
        previewItems.Clear();

        if (!CanPreview())
            return;

        previewItems.AddRange(BuildPreview(scopePaths, ruleMode, GetFindText(), GetReplaceText(), cutStartCharacters, cutEndCharacters, caseSensitive, includeFolders, recursiveFolders));
    }

    void DrawRuleFields()
    {
        switch (ruleMode)
        {
            case RenameRuleMode.ReplaceText:
                findText = EditorGUILayout.TextField("Find", findText);
                replaceText = EditorGUILayout.TextField("Replace With", replaceText);
                caseSensitive = EditorGUILayout.Toggle("Case Sensitive", caseSensitive);
                break;

            case RenameRuleMode.CutText:
                cutText = EditorGUILayout.TextField("Cut Text", cutText);
                caseSensitive = EditorGUILayout.Toggle("Case Sensitive", caseSensitive);
                break;

            case RenameRuleMode.TrimCharacters:
                cutStartCharacters = Mathf.Max(0, EditorGUILayout.IntField("Cut From Start", cutStartCharacters));
                cutEndCharacters = Mathf.Max(0, EditorGUILayout.IntField("Cut From End", cutEndCharacters));
                break;
        }
    }

    bool CanPreview()
    {
        if (scopePaths.Count == 0)
            return false;

        switch (ruleMode)
        {
            case RenameRuleMode.ReplaceText:
                return !string.IsNullOrEmpty(findText);

            case RenameRuleMode.CutText:
                return !string.IsNullOrEmpty(cutText);

            case RenameRuleMode.TrimCharacters:
                return cutStartCharacters > 0 || cutEndCharacters > 0;

            default:
                return false;
        }
    }

    string GetFindText()
    {
        return ruleMode == RenameRuleMode.CutText ? cutText : findText;
    }

    string GetReplaceText()
    {
        return ruleMode == RenameRuleMode.CutText ? string.Empty : replaceText;
    }

    void DrawPreview()
    {
        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

        if (previewItems.Count == 0)
        {
            EditorGUILayout.HelpBox("No matching assets in the current scope.", MessageType.Info);
            return;
        }

        int blockedCount = CountBlockedItems(previewItems);
        if (blockedCount > 0)
            EditorGUILayout.HelpBox(string.Format("{0} item(s) cannot be renamed. Fix conflicts before applying.", blockedCount), MessageType.Warning);

        scroll = EditorGUILayout.BeginScrollView(scroll);
        for (int i = 0; i < previewItems.Count; i++)
        {
            RenamePreviewItem item = previewItems[i];
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(item.OldName, EditorStyles.boldLabel);
            EditorGUILayout.LabelField("To", item.NewName);
            EditorGUILayout.LabelField("Path", item.Path);
            if (!string.IsNullOrEmpty(item.BlockReason))
                EditorGUILayout.HelpBox(item.BlockReason, MessageType.Warning);
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndScrollView();
    }

    static List<string> GetSelectedAssetPaths()
    {
        UnityEngine.Object[] selection = Selection.GetFiltered<UnityEngine.Object>(SelectionMode.Assets);
        List<string> paths = new List<string>();

        for (int i = 0; i < selection.Length; i++)
        {
            string path = AssetDatabase.GetAssetPath(selection[i]);
            if (!string.IsNullOrEmpty(path) && !paths.Contains(path))
                paths.Add(path);
        }

        return paths;
    }

    static List<RenamePreviewItem> BuildPreview(
        List<string> rootPaths,
        RenameRuleMode mode,
        string find,
        string replace,
        int cutStartCount,
        int cutEndCount,
        bool caseSensitiveSearch,
        bool includeFolderNames,
        bool recursive)
    {
        List<RenamePreviewItem> items = new List<RenamePreviewItem>();
        HashSet<string> visitedPaths = new HashSet<string>();
        HashSet<string> plannedTargets = new HashSet<string>();

        for (int i = 0; i < rootPaths.Count; i++)
            AddPathCandidates(rootPaths[i], recursive, includeFolderNames, visitedPaths, items);

        for (int i = items.Count - 1; i >= 0; i--)
        {
            RenamePreviewItem item = items[i];
            string newName = CreateNewName(item.OldName, mode, find, replace, cutStartCount, cutEndCount, caseSensitiveSearch);
            if (newName == item.OldName)
            {
                items.RemoveAt(i);
                continue;
            }

            item.NewName = newName;
            item.TargetPath = CombineAssetPath(GetParentAssetPath(item.Path), newName + GetTargetExtension(item));

            if (string.IsNullOrEmpty(newName))
                item.BlockReason = "New name is empty.";
            else if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(item.TargetPath) != null)
                item.BlockReason = "An asset already exists at the target path.";
            else if (!plannedTargets.Add(item.TargetPath))
                item.BlockReason = "Another selected asset would be renamed to the same target path.";

            items[i] = item;
        }

        items.Sort((left, right) => string.Compare(left.Path, right.Path, StringComparison.Ordinal));
        return items;
    }

    static void AddPathCandidates(
        string path,
        bool recursive,
        bool includeFolderNames,
        HashSet<string> visitedPaths,
        List<RenamePreviewItem> items)
    {
        if (string.IsNullOrEmpty(path) || !visitedPaths.Add(path))
            return;

        bool isFolder = AssetDatabase.IsValidFolder(path);
        if (!isFolder || includeFolderNames)
            items.Add(new RenamePreviewItem(path));

        if (!isFolder)
            return;

        if (recursive)
        {
            string[] guids = AssetDatabase.FindAssets(string.Empty, new[] { path });
            for (int i = 0; i < guids.Length; i++)
                AddPathCandidates(AssetDatabase.GUIDToAssetPath(guids[i]), false, includeFolderNames, visitedPaths, items);
        }
        else
        {
            string[] childGuids = AssetDatabase.FindAssets(string.Empty, new[] { path });
            for (int i = 0; i < childGuids.Length; i++)
            {
                string childPath = AssetDatabase.GUIDToAssetPath(childGuids[i]);
                if (GetParentAssetPath(childPath) == path)
                    AddPathCandidates(childPath, false, includeFolderNames, visitedPaths, items);
            }
        }
    }

    static string ReplaceText(string value, string find, string replace, bool caseSensitiveSearch)
    {
        if (caseSensitiveSearch)
            return value.Replace(find, replace);

        return ReplaceTextIgnoreCase(value, find, replace);
    }

    static string CreateNewName(
        string oldName,
        RenameRuleMode mode,
        string find,
        string replace,
        int cutStartCount,
        int cutEndCount,
        bool caseSensitiveSearch)
    {
        switch (mode)
        {
            case RenameRuleMode.ReplaceText:
            case RenameRuleMode.CutText:
                return ReplaceText(oldName, find, replace, caseSensitiveSearch);

            case RenameRuleMode.TrimCharacters:
                return TrimCharacters(oldName, cutStartCount, cutEndCount);

            default:
                return oldName;
        }
    }

    static string TrimCharacters(string value, int cutStartCount, int cutEndCount)
    {
        int start = Math.Min(Math.Max(0, cutStartCount), value.Length);
        int end = Math.Max(start, value.Length - Math.Max(0, cutEndCount));
        return value.Substring(start, end - start);
    }

    static string ReplaceTextIgnoreCase(string value, string find, string replace)
    {
        int index = value.IndexOf(find, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
            return value;

        string result = string.Empty;
        int currentIndex = 0;
        while (index >= 0)
        {
            result += value.Substring(currentIndex, index - currentIndex);
            result += replace;
            currentIndex = index + find.Length;
            index = value.IndexOf(find, currentIndex, StringComparison.OrdinalIgnoreCase);
        }

        result += value.Substring(currentIndex);
        return result;
    }

    static int CountBlockedItems(List<RenamePreviewItem> items)
    {
        int count = 0;
        for (int i = 0; i < items.Count; i++)
        {
            if (!string.IsNullOrEmpty(items[i].BlockReason))
                count++;
        }

        return count;
    }

    static void ApplyRenames(List<RenamePreviewItem> items)
    {
        List<RenamePreviewItem> orderedItems = new List<RenamePreviewItem>(items);
        orderedItems.Sort((left, right) => string.Compare(right.Path, left.Path, StringComparison.Ordinal));

        AssetDatabase.StartAssetEditing();
        try
        {
            for (int i = 0; i < orderedItems.Count; i++)
            {
                RenamePreviewItem item = orderedItems[i];
                if (!string.IsNullOrEmpty(item.BlockReason))
                    continue;

                string error = AssetDatabase.RenameAsset(item.Path, item.NewName);
                if (!string.IsNullOrEmpty(error))
                    Debug.LogError(string.Format("Failed to rename asset '{0}' to '{1}': {2}", item.Path, item.NewName, error));
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }

    static string GetParentAssetPath(string assetPath)
    {
        string parent = Path.GetDirectoryName(assetPath);
        return string.IsNullOrEmpty(parent) ? string.Empty : parent.Replace('\\', '/');
    }

    static string CombineAssetPath(string parent, string child)
    {
        return string.IsNullOrEmpty(parent) ? child : parent + "/" + child;
    }

    static string GetTargetExtension(RenamePreviewItem item)
    {
        return item.IsFolder ? string.Empty : Path.GetExtension(item.Path);
    }

    enum RenameRuleMode
    {
        ReplaceText,
        CutText,
        TrimCharacters
    }

    struct RenamePreviewItem
    {
        public readonly string Path;
        public readonly bool IsFolder;
        public readonly string OldName;
        public string NewName;
        public string TargetPath;
        public string BlockReason;

        public RenamePreviewItem(string path)
        {
            Path = path;
            IsFolder = AssetDatabase.IsValidFolder(path);
            OldName = IsFolder ? System.IO.Path.GetFileName(path) : System.IO.Path.GetFileNameWithoutExtension(path);
            NewName = string.Empty;
            TargetPath = string.Empty;
            BlockReason = string.Empty;
        }
    }
}
#endif
