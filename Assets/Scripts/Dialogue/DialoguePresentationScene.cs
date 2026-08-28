using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Loads and holds the additive DialoguePresentation scene. The scene stays loaded for the whole
/// gameplay/map lifetime — building the stage on demand would cost a hitch at the exact moment the
/// player triggers a conversation — while its camera and light rig stay disabled between
/// conversations, so an idle stage costs nothing to render.
/// </summary>
public static class DialoguePresentationScene
{
    public const string SceneName = "DialoguePresentation";

    static AsyncOperation loadOperation;

    public static bool IsLoaded
    {
        get
        {
            Scene scene = SceneManager.GetSceneByName(SceneName);
            return scene.IsValid() && scene.isLoaded;
        }
    }

    public static bool IsLoading => loadOperation != null && !loadOperation.isDone;

    /// <summary>
    /// Starts the additive load if the scene is not already present. Safe to call repeatedly;
    /// <paramref name="onReady"/> runs as soon as the stage is available.
    /// </summary>
    public static void EnsureLoaded(Action onReady = null)
    {
        if (IsLoaded)
        {
            StripActorLayerFromGameplayCameras();
            onReady?.Invoke();
            return;
        }

        if (IsLoading)
        {
            if (onReady != null)
                loadOperation.completed += _ => OnLoadCompleted(onReady);
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(SceneName))
        {
            Debug.LogWarning(
                $"[Dialogue] Scene '{SceneName}' is not in Build Settings; dialogue cannot be presented.");
            return;
        }

        loadOperation = SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Additive);
        if (loadOperation == null)
            return;

        loadOperation.completed += _ => OnLoadCompleted(onReady);
    }

    public static void Unload()
    {
        loadOperation = null;

        if (!IsLoaded)
            return;

        // A conversation in flight owns a world pause token; ending it first hands the world back.
        if (DialogueDirector.HasInstance && DialogueDirector.Instance.IsPlaying)
            DialogueDirector.Instance.Abort("Presentation scene unloaded");

        SceneManager.UnloadSceneAsync(SceneName);
    }

    static void OnLoadCompleted(Action onReady)
    {
        loadOperation = null;
        StripActorLayerFromGameplayCameras();
        onReady?.Invoke();
    }

    /// <summary>
    /// Takes the DialogueActor layer out of every camera except the stage's own portrait camera, so
    /// actor clones are never drawn into the gameplay view. Runs once per load rather than per frame.
    /// </summary>
    static void StripActorLayerFromGameplayCameras()
    {
        int actorMask = DialogueLayers.ActorLayerMask;
        if (actorMask == 0)
            return;

        DialogueStage stage = UnityEngine.Object.FindFirstObjectByType<DialogueStage>(
            FindObjectsInactive.Include);

        Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < cameras.Length; i++)
        {
            Camera camera = cameras[i];
            if (camera == null || (camera.cullingMask & actorMask) == 0)
                continue;

            // The stage's own portrait camera is the one camera that must keep the actor layer.
            if (stage != null && camera.transform.IsChildOf(stage.transform))
                continue;

            camera.cullingMask &= ~actorMask;
        }
    }
}
