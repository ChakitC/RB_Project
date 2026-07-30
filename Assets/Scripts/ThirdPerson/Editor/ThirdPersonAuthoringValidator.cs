using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.InputSystem;

public static class ThirdPersonAuthoringValidator
{
    const string PlayerPrefabPath = "Assets/Prefab/Player/Player.prefab";
    const string CameraPrefabPath = "Assets/Prefab/System/CameraHolder.prefab";
    const string CameraScriptPath = "Assets/Scripts/GameplayCameraController.cs";
    const string InputActionsPath = "Assets/Input/Inputmaneger.inputactions";
    const string CharacterDatabasePath =
        "Assets/Scripts/System/SaveSystem/CharacterDatabase.asset";
    static readonly string[] AimRigActorPrefabPaths =
    {
        PlayerPrefabPath,
        "Assets/Prefab/Player/Ally_Stryker.prefab",
        "Assets/Prefab/Player/Ally_Helper.prefab",
    };

    [MenuItem("Tools/RB Project/Validate Third Person Authoring")]
    public static void ValidateFromMenu()
    {
        int errorCount = Validate();
        if (errorCount == 0)
        {
            Debug.Log("[Third Person] Authoring validation passed.");
            return;
        }

        Debug.LogError(
            $"[Third Person] Authoring validation found {errorCount} error(s).");
    }

    public static void ValidateBatchMode()
    {
        int errorCount = Validate();
        if (errorCount > 0)
        {
            throw new BuildFailedException(
                $"Third-person authoring validation failed with {errorCount} error(s).");
        }

        Debug.Log("[Third Person] Authoring validation passed.");
    }

    static int Validate()
    {
        int errorCount = 0;
        errorCount += ValidatePlayerPrefab();
        errorCount += ValidateAimRigActorPrefabs();
        errorCount += ValidateCharacterAimBoneMaps();
        errorCount += ValidateCameraPrefab();
        errorCount += ValidateInputActions();
        errorCount += ValidateGameplayScenes();
        return errorCount;
    }

    static int ValidatePlayerPrefab()
    {
        GameObject playerPrefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (playerPrefab == null)
            return Error($"Missing player prefab at {PlayerPrefabPath}.");

        int errorCount = 0;
        if (playerPrefab.GetComponentInChildren<PlayerContext>(true) == null)
            errorCount += Error("Player prefab is missing PlayerContext.");
        if (playerPrefab.GetComponentInChildren<PlayerInput>(true) == null)
            errorCount += Error("Player prefab is missing PlayerInput.");

        return errorCount;
    }

    static int ValidateAimRigActorPrefabs()
    {
        int errorCount = 0;
        foreach (string prefabPath in AimRigActorPrefabPaths)
        {
            GameObject prefab =
                AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                errorCount += Error($"Missing actor prefab at {prefabPath}.");
                continue;
            }

            if (prefab.GetComponentInChildren<ThirdPersonAimRigController>(true) ==
                null)
            {
                errorCount += Error(
                    $"Actor prefab is missing ThirdPersonAimRigController: " +
                    prefabPath);
            }
        }

        return errorCount;
    }

    static int ValidateCharacterAimBoneMaps()
    {
        CharacterDatabase database =
            AssetDatabase.LoadAssetAtPath<CharacterDatabase>(
                CharacterDatabasePath);
        if (database == null)
        {
            return Error(
                $"Missing character database at {CharacterDatabasePath}.");
        }

        int errorCount = 0;
        foreach (CharacterStats character in database.characters)
        {
            if (character == null)
            {
                errorCount += Error(
                    "Character database contains an empty character entry.");
                continue;
            }

            if (character.CharacterPrefab == null)
            {
                errorCount += Error(
                    $"Character '{character.name}' has no visual prefab.");
                continue;
            }

            Animator animator =
                character.CharacterPrefab.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                errorCount += Error(
                    $"Character '{character.name}' visual prefab has no Animator.");
                continue;
            }

            if (animator.isHuman)
                continue;

            ThirdPersonAimBoneMap boneMap =
                animator.GetComponent<ThirdPersonAimBoneMap>();
            if (boneMap == null)
            {
                errorCount += Error(
                    $"Generic character '{character.name}' is missing " +
                    "ThirdPersonAimBoneMap on its Animator GameObject.");
                continue;
            }

            errorCount += ValidateAimBoneMap(character, animator, boneMap);
        }

        return errorCount;
    }

    static int ValidateAimBoneMap(
        CharacterStats character,
        Animator animator,
        ThirdPersonAimBoneMap boneMap)
    {
        int errorCount = 0;
        if (boneMap.Spine == null)
        {
            errorCount += Error(
                $"Generic character '{character.name}' has no mapped Spine.");
        }

        if (boneMap.Chest == null)
        {
            errorCount += Error(
                $"Generic character '{character.name}' has no mapped Chest.");
        }

        Transform[] bones =
        {
            boneMap.Spine,
            boneMap.Chest,
            boneMap.UpperChest,
        };
        for (int i = 0; i < bones.Length; i++)
        {
            Transform bone = bones[i];
            if (bone == null)
                continue;

            if (!bone.IsChildOf(animator.transform))
            {
                errorCount += Error(
                    $"Character '{character.name}' aim bone '{bone.name}' is " +
                    "outside its Animator hierarchy.");
            }

            for (int j = i + 1; j < bones.Length; j++)
            {
                if (bone == bones[j])
                {
                    errorCount += Error(
                        $"Character '{character.name}' reuses aim bone " +
                        $"'{bone.name}' in more than one slot.");
                }
            }
        }

        return errorCount;
    }

    static int ValidateCameraPrefab()
    {
        GameObject cameraPrefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(CameraPrefabPath);
        if (cameraPrefab == null)
            return Error($"Missing camera prefab at {CameraPrefabPath}.");

        GameplayCameraController controller =
            cameraPrefab.GetComponent<GameplayCameraController>();
        if (controller == null)
            return Error("CameraHolder prefab is missing GameplayCameraController.");

        Camera[] cameras = cameraPrefab.GetComponentsInChildren<Camera>(true);
        Camera camera = Array.Find(cameras, candidate => candidate.CompareTag("MainCamera"));
        if (camera == null)
            return Error("CameraHolder prefab is missing a Camera.");

        int errorCount = 0;
        WorldUICameraSync worldUISync =
            cameraPrefab.GetComponentInChildren<WorldUICameraSync>(true);
        if (worldUISync == null)
        {
            errorCount += Error("CameraHolder prefab is missing WorldUICameraSync.");
            return errorCount;
        }

        SerializedObject syncObject = new(worldUISync);
        Camera sourceCamera =
            syncObject.FindProperty("sourceCamera").objectReferenceValue as Camera;
        if (sourceCamera != camera)
        {
            errorCount += Error(
                "WorldUICameraSync must reference the MainCamera Camera component.");
        }

        int worldUILayer = LayerMask.NameToLayer("WorldUI");
        if (worldUILayer < 0)
        {
            errorCount += Error("Project is missing the WorldUI layer.");
            return errorCount;
        }

        int worldUIMask = 1 << worldUILayer;
        Camera worldUICamera = worldUISync.GetComponent<Camera>();
        if ((camera.cullingMask & worldUIMask) != 0)
            errorCount += Error("MainCamera must exclude the WorldUI layer.");
        if ((worldUICamera.cullingMask & worldUIMask) == 0)
            errorCount += Error("WorldUICamera must render the WorldUI layer.");

        return errorCount;
    }

    static int ValidateInputActions()
    {
        InputActionAsset inputActions =
            AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
        if (inputActions == null)
            return Error($"Missing input actions asset at {InputActionsPath}.");

        int errorCount = 0;
        InputActionMap playerMap = inputActions.FindActionMap("Player", false);
        if (playerMap == null)
            return Error("Input actions asset is missing the Player map.");

        InputAction look = playerMap.FindAction("MouseLook", false);
        if (look == null)
        {
            errorCount += Error("Player input map is missing MouseLook.");
        }
        else
        {
            if (look.type != InputActionType.PassThrough)
            {
                errorCount += Error(
                    "MouseLook must be PassThrough so pointer delta is not gated.");
            }

            bool hasMouseDelta = look.bindings.Any(
                binding => string.Equals(
                    binding.effectivePath,
                    "<Mouse>/delta",
                    StringComparison.OrdinalIgnoreCase));
            if (!hasMouseDelta)
                errorCount += Error("MouseLook must bind to <Mouse>/delta.");
        }

        InputAction aim = playerMap.FindAction("AimOn", false);
        if (aim == null)
        {
            errorCount += Error("Player input map is missing AimOn.");
        }
        else
        {
            bool hasRightMouse = aim.bindings.Any(
                binding => string.Equals(
                    binding.effectivePath,
                    "<Mouse>/rightButton",
                    StringComparison.OrdinalIgnoreCase));
            if (!hasRightMouse)
                errorCount += Error("AimOn must bind to the right mouse button.");
        }

        return errorCount;
    }

    static int ValidateGameplayScenes()
    {
        int errorCount = 0;
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (!scene.enabled ||
                scene.path.IndexOf(
                    "/Basement/",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                continue;
            }

            string[] dependencies =
                AssetDatabase.GetDependencies(scene.path, true);
            bool hasCamera = dependencies.Any(
                path => string.Equals(
                    path,
                    CameraPrefabPath,
                    StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(
                            path,
                            CameraScriptPath,
                            StringComparison.OrdinalIgnoreCase));
            if (!hasCamera)
            {
                errorCount += Error(
                    $"Gameplay scene has no TPS camera dependency: {scene.path}");
            }
        }

        return errorCount;
    }

    static int Error(string message)
    {
        Debug.LogError($"[Third Person] {message}");
        return 1;
    }
}
