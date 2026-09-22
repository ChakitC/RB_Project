using System;
using System.Collections.Generic;
using SecretZauce.SecondBrain.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SecretZauce.SecondBrain.Pro.Editor
{
    public class SceneLinkGUI : SceneLinkGUIBase
    {
        readonly SerializedObject serializedObject;
        readonly SerializedProperty sceneGuidProp;
        readonly List<SceneInfo> openScenes = new List<SceneInfo>();

        readonly Base targetBase;
        readonly Action onSceneLinkChanged;

        static Texture s_CreateIcon;
        static bool    s_CreateIconProSkin;
        
        struct SceneInfo { public string name; public string guid; }

        public SceneLinkGUI(UnityEditor.Editor editorWindow) : base(editorWindow)
        {
            serializedObject = editorWindow.serializedObject; 
            sceneGuidProp = serializedObject.FindProperty("sceneGuid");
            targetBase = editorWindow.target as Base;
            onSceneLinkChanged = editorWindow.Repaint;
            RefreshCaches();
        }

        void RefreshCaches()
        {
            openScenes.Clear();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (string.IsNullOrEmpty(scene.path)) continue;
                var guid = AssetDatabase.AssetPathToGUID(scene.path);
                if (string.IsNullOrEmpty(guid)) continue;
                openScenes.Add(new SceneInfo { name = scene.name, guid = guid });
            }
        }
        
        public override void Draw()
        {
            if (sceneGuidProp == null || targetBase == null) 
                return;

            var currentGuid = sceneGuidProp.stringValue;
            var currentAsset = ResolveSceneAsset(currentGuid, out var isMissing);
            if (isMissing)
            {
                var prevColor = GUI.color;
                GUI.color = new Color(1f, 0.7f, 0.3f);
                EditorGUILayout.LabelField(new GUIContent("⚠ Missing scene", $"GUID: {currentGuid}"));
                GUI.color = prevColor;
            }

            // One-line scene link row: label, object field, open-scenes menu, clear button
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    new GUIContent("Linked Scene",
                        "Drag a scene asset here. When this scene is opened in the Editor, the SecondBrainWindow will automatically open a window targeting this Base."),
                    GUILayout.Width(110));

                // Scene object field
                EditorGUI.BeginChangeCheck();
                var picked = (SceneAsset) EditorGUILayout.ObjectField(
                    currentAsset,
                    typeof(SceneAsset),
                    allowSceneObjects: false,
                    GUILayout.ExpandWidth(true));

                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(targetBase, "Change Linked Scene");
                    sceneGuidProp.stringValue = picked != null
                        ? AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(picked))
                        : string.Empty;
                    OnSceneLinkChanged();
                }

                // Open scenes menu (compact)
                bool linkPs = EditorGUIUtility.isProSkin;
                if (s_CreateIcon == null || s_CreateIconProSkin != linkPs)
                {
                    s_CreateIconProSkin = linkPs;
                    s_CreateIcon = IconUtils.Load("create");
                }
                GUIContent linkPlusContent = s_CreateIcon != null
                    ? new GUIContent(s_CreateIcon, "Link Scene")
                    : new GUIContent("+", "Link Scene");
                if (openScenes.Count > 0)
                {
                    if (GUILayout.Button(linkPlusContent, GUILayout.Width(28)))
                    {
                        var menu = new GenericMenu();
                        for (int i = 0; i < openScenes.Count; i++)
                        {
                            var s = openScenes[i];
                            bool isLinked = s.guid == currentGuid;
                            var label = isLinked ? $"✓ {s.name}" : s.name;
                            // capture
                            var guid = s.guid;
                            menu.AddItem(new GUIContent(label), isLinked, () =>
                            {
                                Undo.RecordObject(targetBase, isLinked ? "Unlink Scene" : "Link Scene");
                                sceneGuidProp.stringValue = isLinked ? string.Empty : guid;
                                OnSceneLinkChanged(); 
                            });
                        }
                        menu.ShowAsContext();
                    }
                }
                else
                {
                    using (new EditorGUI.DisabledScope(true))
                        GUILayout.Button(linkPlusContent, GUILayout.Width(28));
                }

                // Clear button on the right
                if (!string.IsNullOrEmpty(currentGuid))
                {
                    if (GUILayout.Button(new GUIContent("Clear", "Unlink the scene from this Base."),
                            GUILayout.Width(64)))
                    {
                        Undo.RecordObject(targetBase, "Clear Linked Scene");
                        sceneGuidProp.stringValue = string.Empty;
                        OnSceneLinkChanged(); 
                    }
                }
            }
        }

        static SceneAsset ResolveSceneAsset(string currentGuid, out bool isMissing)
        {
            // Resolve current scene asset from stored GUID
            SceneAsset currentAsset = null;
            isMissing = false;
            if (!string.IsNullOrEmpty(currentGuid))
            {
                var path = AssetDatabase.GUIDToAssetPath(currentGuid);
                if (!string.IsNullOrEmpty(path))
                    currentAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
                else
                    isMissing = true;
            }

            return currentAsset;
        }
        
        void OnSceneLinkChanged()
        {
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(targetBase);
            AssetDatabase.SaveAssets();
            EditorApplication.delayCall += () =>
            {
                RefreshCaches();
                onSceneLinkChanged?.Invoke();
            };
        }
    }
}
