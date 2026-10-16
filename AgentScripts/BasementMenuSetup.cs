using System;
using System.Linq;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class BasementMenuSetup
{
    const string Folder = "Assets/Data/Animation/Basement/Menus";
    static T Asset<T>(string name) where T : ScriptableObject
    {
        string path = Folder + "/" + name + ".asset";
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null) { asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path); }
        return asset;
    }
    static void Set(UnityEngine.Object obj, string field, UnityEngine.Object value)
    {
        var so = new SerializedObject(obj);
        so.FindProperty(field).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(obj);
    }
    public static string Main()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode before setup.");
        var current = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (current.isDirty) throw new InvalidOperationException("Active scene has unsaved changes; preserve them before setup.");
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/Data/Animation/Basement", "Menus");
        var gunCamera = Asset<BasementMenuCameraPresetSO>("Aires_GunMenuCamera");
        gunCamera.sourceFbx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Spain/UI/Gunmenu/Aires_camera_Gunmenumene.fbx");
        gunCamera.cameraPath = "Camera Gun Menu";
        BasementMenuCameraPresetEditor.Bake(gunCamera);
        var accessoryCamera = Asset<BasementMenuCameraPresetSO>("AccessoryCamera");
        accessoryCamera.sourceFbx = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CameraPosition_Publlick/camera_Assorymenu.fbx");
        accessoryCamera.cameraPath = "";
        BasementMenuCameraPresetEditor.Bake(accessoryCamera);
        var accessory = Asset<BasementMenuDefinitionSO>("Accessory");
        var gun = Asset<BasementMenuDefinitionSO>("GunMenu");
        accessory.defaultCamera = accessoryCamera;
        gun.defaultCamera = gunCamera;
        EditorUtility.SetDirty(accessory); EditorUtility.SetDirty(gun);
        foreach (var pair in new[] { ("BasementAccessoryScreen", accessory), ("BasementGunMenuScreen", gun) })
        {
            string path = "Assets/Prefab/User Interface/" + pair.Item1 + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var screen = root.GetComponents<MonoBehaviour>().Single(c => c is IBasementMenuView);
                Set(screen, "menuDefinition", pair.Item2);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        var profile = AssetDatabase.LoadAssetAtPath<BasementAnimationProfileSO>("Assets/Data/Animation/Basement/Aires_BasementAnimation.asset");
        var entry = profile.FindMenu(gun);
        if (entry == null)
        {
            var entries = profile.menus.ToList();
            entry = new BasementAnimationProfileSO.MenuEntry { menu = gun };
            entries.Add(entry); profile.menus = entries.ToArray();
        }
        entry.camera = gunCamera;
        EditorUtility.SetDirty(profile);
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Basement/Basement.unity");
        var manager = UnityEngine.Object.FindFirstObjectByType<CameraManager>();
        var serialized = new SerializedObject(manager);
        manager.RegisterMenu(accessory, (CinemachineCamera)serialized.FindProperty("accessoryCamera").objectReferenceValue);
        manager.RegisterMenu(gun, (CinemachineCamera)serialized.FindProperty("gunMenuCamera").objectReferenceValue);
        BasementMenuPresentationController.Resolve(manager);
        EditorUtility.SetDirty(manager);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        return "Created menu definitions, baked FBX presets, bound prefabs/scene and Aires GunMenu override.";
    }
}
