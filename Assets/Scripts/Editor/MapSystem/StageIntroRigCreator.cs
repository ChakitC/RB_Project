using System.IO;
using Animancer;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Builds the shared <c>StageIntroRig.prefab</c> with the marker/camera contract already wired,
/// so authors only have to place the markers and assign the Camera Clip.
/// </summary>
public static class StageIntroRigCreator
{
    public const string RigPrefabPath = "Assets/Prefab/MAP/StageIntro/StageIntroRig.prefab";

    [MenuItem("Tools/RB/Map/Create Stage Intro Rig Prefab")]
    public static void CreateFromMenu()
    {
        GameObject prefab = CreateOrUpdatePrefab();
        if (prefab == null)
            return;

        EditorGUIUtility.PingObject(prefab);
        Selection.activeObject = prefab;
        EditorUtility.DisplayDialog(
            "Stage Intro Rig",
            $"Created '{RigPrefabPath}'.\n\n" +
            "Nest it under the Start room prefab, place the four markers, then assign the group-shot Camera Clip.",
            "OK");
    }

    public static GameObject CreateOrUpdatePrefab()
    {
        if (File.Exists(RigPrefabPath))
        {
            Debug.LogWarning($"[StageIntroRigCreator] '{RigPrefabPath}' already exists. Delete it first to regenerate.");
            return AssetDatabase.LoadAssetAtPath<GameObject>(RigPrefabPath);
        }

        string directory = Path.GetDirectoryName(RigPrefabPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        GameObject root = BuildRigHierarchy();
        try
        {
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, RigPrefabPath);
            AssetDatabase.SaveAssets();
            return prefab;
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    static GameObject BuildRigHierarchy()
    {
        var root = new GameObject("StageIntroRig");
        StageIntroRig rig = root.AddComponent<StageIntroRig>();

        var markerRoot = new GameObject("Markers");
        markerRoot.transform.SetParent(root.transform, false);

        CreateMarker(markerRoot.transform, ChainActorRole.Player, new Vector3(0f, 0f, 0f));
        CreateMarker(markerRoot.transform, ChainActorRole.PartySlot1, new Vector3(-1.2f, 0f, -0.45f));
        CreateMarker(markerRoot.transform, ChainActorRole.PartySlot2, new Vector3(1.2f, 0f, -0.45f));
        CreateMarker(markerRoot.transform, ChainActorRole.Helper, new Vector3(0f, 0f, -1.2f));

        var cameraRoot = new GameObject("CameraAnimationRoot", typeof(Animator));
        cameraRoot.transform.SetParent(root.transform, false);
        cameraRoot.AddComponent<AnimancerComponent>().Animator = cameraRoot.GetComponent<Animator>();
        cameraRoot.transform.localPosition = new Vector3(0f, 1.6f, 4.5f);
        cameraRoot.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

        var cameraObject = new GameObject("IntroCamera");
        cameraObject.transform.SetParent(cameraRoot.transform, false);
        CinemachineCamera introCamera = cameraObject.AddComponent<CinemachineCamera>();
        introCamera.Priority = 0;
        cameraObject.SetActive(false);

        var serialized = new SerializedObject(rig);
        serialized.FindProperty("introCamera").objectReferenceValue = introCamera;
        serialized.FindProperty("cameraAnimationRoot").objectReferenceValue = cameraRoot.transform;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        return root;
    }

    static void CreateMarker(Transform parent, ChainActorRole role, Vector3 localPosition)
    {
        var markerObject = new GameObject($"Marker_{role}");
        markerObject.transform.SetParent(parent, false);
        markerObject.transform.localPosition = localPosition;
        markerObject.transform.localRotation = Quaternion.identity;

        StageIntroActorMarker marker = markerObject.AddComponent<StageIntroActorMarker>();
        marker.SetRoleForAuthoring(role);
    }
}
