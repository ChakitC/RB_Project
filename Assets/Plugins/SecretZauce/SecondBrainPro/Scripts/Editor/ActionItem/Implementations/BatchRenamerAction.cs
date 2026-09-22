using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace SecretZauce.SecondBrain.Pro.Editor
{
    public enum RenameOperation
    {
        /// <summary>Find a substring and replace it.</summary>
        Replace,
        /// <summary>Prepend text to every name.</summary>
        AddPrefix,
        /// <summary>Append text to every name.</summary>
        AddSuffix,
        /// <summary>Set a completely new name (use with AppendIndex for unique names).</summary>
        SetName,
        /// <summary>Remove a substring from every name.</summary>
        RemoveText,
        /// <summary>Replace using a regular expression pattern.</summary>
        RegexReplace,
    }

    /// <summary>
    /// Batch-renames all selected GameObjects (scene) or assets (project).
    /// Supports find-and-replace, prefix/suffix, full rename, regex, and sequential indexing.
    /// Sets dirty and triggers an AssetDatabase refresh where needed.
    /// </summary>
    public class BatchRenamerAction : ActionItem
    {
        [Tooltip("What kind of rename to perform.")]
        public RenameOperation operation = RenameOperation.Replace;

        [Header("Replace / Remove / Regex")]
        [Tooltip("Text (or regex pattern) to find. Used by Replace, RemoveText, and RegexReplace.")]
        public string findText = "";

        [Tooltip("Replacement text. Used by Replace and RegexReplace (supports $1 capture groups).")]
        public string replaceText = "";

        [Header("Prefix / Suffix / SetName")]
        [Tooltip("Text to prepend, append, or use as the full name depending on the operation.")]
        public string nameText = "";

        [Header("Indexing")]
        [Tooltip("Append a sequential index suffix to every renamed object (e.g. Cube_01).")]
        public bool appendIndex;

        [Tooltip("Starting value for the sequential index.")]
        public int startIndex = 1;

        [Tooltip("Zero-pad width for the index (e.g. 2 → 01, 02 … 10). 0 = no padding.")]
        public int indexPadding = 2;

        [Tooltip("Separator inserted between the name and the index.")]
        public string indexSeparator = "_";

        public override string ActionPath => "Examples";

        public override string GetDetailDisplay() => operation.ToString();

        /// <summary>Returns a preview of what <paramref name="currentName"/> would become at <paramref name="index"/>.</summary>
        public string PreviewName(string currentName, int index) => BuildName(currentName, index);

        public override void Execute()
        {
            var objects = Selection.objects;
            if (objects.Length == 0)
            {
                Debug.LogWarning("[BatchRenamerAction] Nothing selected.");
                return;
            }

            Undo.SetCurrentGroupName("Batch Rename");
            int group = Undo.GetCurrentGroup();

            bool anyAssetRenamed = false;
            int index = startIndex;

            foreach (var obj in objects)
            {
                if (obj == null) continue;

                string newName = BuildName(obj.name, index);
                index++;

                if (newName == obj.name) continue;

                string assetPath = AssetDatabase.GetAssetPath(obj);
                // GetAssetPath returns empty for scene objects; non-empty means a real project asset
                bool isProjectAsset = !string.IsNullOrEmpty(assetPath);

                if (isProjectAsset)
                {
                    string error = AssetDatabase.RenameAsset(assetPath, newName);
                    if (!string.IsNullOrEmpty(error))
                        Debug.LogWarning($"[BatchRenamerAction] Could not rename '{obj.name}': {error}");
                    else
                        anyAssetRenamed = true;
                }
                else if (obj is GameObject go)
                {
                    Undo.RecordObject(go, "Rename GameObject");
                    go.name = newName;
                    EditorUtility.SetDirty(go);
                }
                else
                {
                    Undo.RecordObject(obj, "Rename Object");
                    obj.name = newName;
                    EditorUtility.SetDirty(obj);
                }
            }

            Undo.CollapseUndoOperations(group);

            if (anyAssetRenamed)
                AssetDatabase.Refresh();
        }

        string BuildName(string current, int index)
        {
            string result = ApplyOperation(current);

            if (appendIndex)
            {
                string formatted = indexPadding > 0
                    ? index.ToString($"D{indexPadding}")
                    : index.ToString();
                result = result + indexSeparator + formatted;
            }

            return result;
        }

        string ApplyOperation(string current)
        {
            return operation switch
            {
                RenameOperation.Replace      => string.IsNullOrEmpty(findText) ? current : current.Replace(findText, replaceText),
                RenameOperation.AddPrefix    => nameText + current,
                RenameOperation.AddSuffix    => current + nameText,
                RenameOperation.SetName      => nameText,
                RenameOperation.RemoveText   => string.IsNullOrEmpty(findText) ? current : current.Replace(findText, string.Empty),
                RenameOperation.RegexReplace => SafeRegexReplace(current, findText, replaceText),
                _                            => current,
            };
        }

        static string SafeRegexReplace(string input, string pattern, string replacement)
        {
            if (string.IsNullOrEmpty(pattern)) return input;
            try
            {
                return Regex.Replace(input, pattern, replacement);
            }
            catch (System.ArgumentException ex)
            {
                Debug.LogWarning($"[BatchRenamerAction] Invalid regex pattern '{pattern}': {ex.Message}");
                return input;
            }
        }
    }
}
