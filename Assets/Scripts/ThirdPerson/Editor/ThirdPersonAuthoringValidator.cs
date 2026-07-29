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

        Camera camera = cameraPrefab.GetComponentInChildren<Camera>(true);
        if (camera == null)
            return Error("CameraHolder prefab is missing a Camera.");
        if (!camera.CompareTag("MainCamera"))
            return Error("CameraHolder camera must use the MainCamera tag.");

        return 0;
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
