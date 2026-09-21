using UnityEditor;
using UnityEngine;

namespace SecretZauce.SecondBrain.Pro.Editor
{
    [CustomEditor(typeof(BatchRenamerAction))]
    [CanEditMultipleObjects]
    internal class BatchRenamerActionEditor : UnityEditor.Editor
    {
        SerializedProperty _operation;
        SerializedProperty _findText;
        SerializedProperty _replaceText;
        SerializedProperty _nameText;
        SerializedProperty _appendIndex;
        SerializedProperty _startIndex;
        SerializedProperty _indexPadding;
        SerializedProperty _indexSeparator;

        void OnEnable()
        {
            _operation      = serializedObject.FindProperty(nameof(BatchRenamerAction.operation));
            _findText       = serializedObject.FindProperty(nameof(BatchRenamerAction.findText));
            _replaceText    = serializedObject.FindProperty(nameof(BatchRenamerAction.replaceText));
            _nameText       = serializedObject.FindProperty(nameof(BatchRenamerAction.nameText));
            _appendIndex    = serializedObject.FindProperty(nameof(BatchRenamerAction.appendIndex));
            _startIndex     = serializedObject.FindProperty(nameof(BatchRenamerAction.startIndex));
            _indexPadding   = serializedObject.FindProperty(nameof(BatchRenamerAction.indexPadding));
            _indexSeparator = serializedObject.FindProperty(nameof(BatchRenamerAction.indexSeparator));
        }

        public override void OnInspectorGUI()
        {
            var action = (BatchRenamerAction)target;
            serializedObject.Update();

            DrawExecuteButton(targets);
            EditorGUILayout.Space(2);

            DrawOperationSection();
            EditorGUILayout.Space(4);

            DrawIndexingSection();
            EditorGUILayout.Space(4);

            serializedObject.ApplyModifiedProperties();

            DrawPreviewSection(action);
        }

        // ── Execute ────────────────────────────────────────────────────────────

        static void DrawExecuteButton(UnityEngine.Object[] actionTargets)
        {
            EditorGUILayout.Space(4);
            string label = actionTargets.Length > 1 ? $"▶  Execute ({actionTargets.Length})" : "▶  Execute";
            if (GUILayout.Button(label, GUILayout.Height(28)))
            {
                foreach (var t in actionTargets)
                    (t as BatchRenamerAction)?.Execute();
            }
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(string.Empty, GUI.skin.horizontalSlider);
        }

        // ── Operation ──────────────────────────────────────────────────────────

        void DrawOperationSection()
        {
            EditorGUILayout.LabelField("Rename", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.PropertyField(_operation, new GUIContent("Operation"));
                EditorGUILayout.Space(2);

                var op = (RenameOperation)_operation.intValue;
                DrawContextFields(op);
            }
        }

        void DrawContextFields(RenameOperation op)
        {
            switch (op)
            {
                case RenameOperation.Replace:
                    Field(_findText,    "Find");
                    Field(_replaceText, "Replace With");
                    break;

                case RenameOperation.RemoveText:
                    Field(_findText, "Text to Remove");
                    break;

                case RenameOperation.RegexReplace:
                    Field(_findText,    "Regex Pattern");
                    Field(_replaceText, "Replace With");
                    HelpBox("Supports capture groups: $1, $2 …", MessageType.None);
                    break;

                case RenameOperation.AddPrefix:
                    Field(_nameText, "Prefix");
                    break;

                case RenameOperation.AddSuffix:
                    Field(_nameText, "Suffix");
                    break;

                case RenameOperation.SetName:
                    Field(_nameText, "New Name");
                    HelpBox("Combine with Append Index to give each object a unique name.", MessageType.None);
                    break;
            }
        }

        // ── Indexing ───────────────────────────────────────────────────────────

        void DrawIndexingSection()
        {
            EditorGUILayout.LabelField("Indexing", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.PropertyField(_appendIndex, new GUIContent("Append Index"));

                if (_appendIndex.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(_startIndex,     new GUIContent("Start"));
                    EditorGUILayout.PropertyField(_indexPadding,   new GUIContent("Pad Digits"));
                    EditorGUILayout.PropertyField(_indexSeparator, new GUIContent("Separator"));

                    var action = (BatchRenamerAction)target;
                    string example = FormatIndex(action.startIndex, action.indexPadding, action.indexSeparator);
                    EditorGUILayout.LabelField("Example suffix", example, EditorStyles.miniLabel);
                    EditorGUI.indentLevel--;
                }
            }
        }

        static string FormatIndex(int start, int padding, string sep)
        {
            string idx = padding > 0 ? start.ToString($"D{padding}") : start.ToString();
            return $"{sep}{idx}";
        }

        // ── Preview ────────────────────────────────────────────────────────────

        static void DrawPreviewSection(BatchRenamerAction action)
        {
            var selected = Selection.objects;
            if (selected.Length == 0) return;

            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                int count = Mathf.Min(selected.Length, 6);
                for (int i = 0; i < count; i++)
                {
                    if (selected[i] == null) continue;

                    string from = selected[i].name;
                    string to   = action.PreviewName(from, action.startIndex + i);
                    DrawPreviewRow(from, to);
                }

                if (selected.Length > 6)
                    EditorGUILayout.LabelField($"… and {selected.Length - 6} more", EditorStyles.centeredGreyMiniLabel);
            }
        }

        static void DrawPreviewRow(string from, string to)
        {
            bool changed = to != from;

            using (new EditorGUILayout.HorizontalScope())
            {
                var fromStyle = changed ? EditorStyles.label : EditorStyles.miniLabel;
                var toStyle   = changed ? EditorStyles.boldLabel : EditorStyles.miniLabel;
                var arrowColor = changed ? new Color(0.4f, 0.8f, 0.4f) : new Color(0.5f, 0.5f, 0.5f);

                GUILayout.Label(from, fromStyle, GUILayout.ExpandWidth(true));

                var prev = GUI.color;
                GUI.color = arrowColor;
                GUILayout.Label("→", EditorStyles.centeredGreyMiniLabel, GUILayout.Width(18));
                GUI.color = prev;

                GUILayout.Label(to, toStyle, GUILayout.ExpandWidth(true));
            }
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        static void Field(SerializedProperty prop, string label) =>
            EditorGUILayout.PropertyField(prop, new GUIContent(label));

        static void HelpBox(string msg, MessageType type) =>
            EditorGUILayout.HelpBox(msg, type);
    }
}
