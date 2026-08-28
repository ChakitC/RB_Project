using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Keeps the additive DialoguePresentation scene loaded. Put one on a persistent object — the boot
/// System object is the natural home — and it re-loads the presentation scene after every
/// single-mode scene load, because loading a scene in Single mode unloads every additive scene with
/// it.
/// </summary>
[DisallowMultipleComponent]
public sealed class DialoguePresentationSceneLoader : MonoBehaviour
{
    [SerializeField, Tooltip("Preload the presentation scene as soon as this object wakes up, so the " +
                             "first conversation does not pay the load cost.")]
    private bool loadOnAwake = true;

    [SerializeField, Tooltip("Re-load the presentation scene after a single-mode scene load, which " +
                             "unloads every additive scene. Leave on for a persistent loader.")]
    private bool reloadAfterSceneChange = true;

    [SerializeField, Tooltip("Unload the presentation scene when this object is destroyed.")]
    private bool unloadOnDestroy;

    void Awake()
    {
        if (loadOnAwake)
            DialoguePresentationScene.EnsureLoaded();

        if (reloadAfterSceneChange)
            SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (unloadOnDestroy)
            DialoguePresentationScene.Unload();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Additive loads leave the presentation scene alone; only a Single load takes it away.
        if (mode == LoadSceneMode.Single)
            DialoguePresentationScene.EnsureLoaded();
    }

    /// <summary>Loads the presentation scene on demand, for callers that do not preload on Awake.</summary>
    public void Preload()
    {
        DialoguePresentationScene.EnsureLoaded();
    }
}
