using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Owns the stage intro preview actors. Deliberately static rather than living on the inspector:
/// blocking out a shot means selecting and dragging markers, which would destroy an inspector-owned
/// preview the moment you clicked a marker.
///
/// Preview actors are clones of <see cref="CharacterStats.CharacterPrefab"/> — the actor prefabs in
/// the party config carry no model, because <c>CharacterVisualController</c> only builds one at
/// runtime. They are marked <see cref="HideFlags.DontSaveInEditor"/> and cleared on every exit path,
/// including entering Play Mode, so they can never leak into a real scene.
/// </summary>
[InitializeOnLoad]
public static class StageIntroPreviewSession
{
    sealed class PreviewActor
    {
        public ChainActorRole Role;
        public GameObject Root;
        public GameObject AnimatorRoot;
        public AnimationClip Clip;
        public StageIntroActorMarker Marker;
    }

    static readonly List<PreviewActor> Actors = new();

    static StageIntroRig activeRig;
    static bool animationModeOwned;
    static Scene previewScene;
    static bool previewSceneWasDirty;
    static float previewTime;
    static bool playing;
    static double lastUpdateTime;
    static int lastSyncHash;

    static StageIntroPreviewSession()
    {
        EditorApplication.update += OnEditorUpdate;
        AssemblyReloadEvents.beforeAssemblyReload += Clear;
        PrefabStage.prefabStageClosing += _ => Clear();
        EditorSceneManager.sceneClosing += (_, _) => Clear();
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    public static bool HasPreview => Actors.Count > 0;
    public static bool IsPlaying => playing;
    public static float PreviewTime => previewTime;
    public static StageIntroRig ActiveRig => activeRig;

    public static bool OwnsRig(StageIntroRig rig) => rig != null && activeRig == rig;

    // ---------------------------------------------------------------- spawn / clear

    public static void Spawn(StageIntroRig rig, StageIntroPreviewRoster roster)
    {
        Clear();

        if (rig == null || roster == null)
            return;

        activeRig = rig;
        previewScene = rig.gameObject.scene;
        previewSceneWasDirty = previewScene.IsValid() && previewScene.isDirty;

        IReadOnlyList<StageIntroPreviewRoster.Slot> slots = roster.Slots;
        for (int i = 0; i < slots.Count; i++)
        {
            StageIntroPreviewRoster.Slot slot = slots[i];
            CharacterStats stats = slot.Character;
            if (stats == null || stats.CharacterPrefab == null)
                continue;

            StageIntroActorMarker marker = rig.FindMarker(slot.Role);
            if (marker == null)
                continue;

            var clone = (GameObject)PrefabUtility.InstantiatePrefab(stats.CharacterPrefab, previewScene);
            if (clone == null)
                continue;

            clone.name = $"[StageIntroPreview] {slot.Role}";
            clone.hideFlags = HideFlags.DontSaveInEditor;

            var animator = clone.GetComponentInChildren<Animator>(true);
            if (animator != null && stats.characterAvatar != null)
            {
                // Runtime does the same in CharacterVisualController.ConfigureAnimatorRuntime; without
                // it a shared clip retargets onto the wrong rig and the preview pose is simply wrong.
                animator.avatar = stats.characterAvatar;
            }

            Actors.Add(new PreviewActor
            {
                Role = slot.Role,
                Root = clone,
                AnimatorRoot = animator != null ? animator.gameObject : clone,
                Clip = roster.ResolveClip(slot),
                Marker = marker,
            });
        }

        RestoreSceneDirtiness();
        lastSyncHash = 0;
        Sync(force: true);
    }

    public static void Clear()
    {
        playing = false;

        if (animationModeOwned && AnimationMode.InAnimationMode())
            AnimationMode.StopAnimationMode();
        animationModeOwned = false;

        for (int i = 0; i < Actors.Count; i++)
        {
            if (Actors[i].Root != null)
                Object.DestroyImmediate(Actors[i].Root);
        }

        Actors.Clear();
        activeRig = null;
        previewTime = 0f;
        lastSyncHash = 0;

        RestoreSceneDirtiness();
        SceneView.RepaintAll();
    }

    /// <summary>Re-reads clip choices without respawning, for a clip-override edit.</summary>
    public static void RefreshClips(StageIntroPreviewRoster roster)
    {
        if (roster == null)
            return;

        for (int i = 0; i < Actors.Count; i++)
            Actors[i].Clip = roster.ResolveClip(roster.GetSlot(Actors[i].Role));

        Sync(force: true);
    }

    // ---------------------------------------------------------------- playback

    public static void SetPlaying(bool value)
    {
        playing = value && activeRig != null && activeRig.IntroDuration > 0f;
        lastUpdateTime = EditorApplication.timeSinceStartup;
    }

    public static void SetTime(float value)
    {
        previewTime = Mathf.Max(0f, value);
        Sync(force: true);
    }

    static void OnEditorUpdate()
    {
        if (activeRig == null)
        {
            if (Actors.Count > 0)
                Clear();
            return;
        }

        if (playing)
        {
            double now = EditorApplication.timeSinceStartup;
            previewTime += (float)(now - lastUpdateTime);
            lastUpdateTime = now;

            float duration = activeRig.IntroDuration;
            if (previewTime >= duration)
            {
                previewTime = duration;
                playing = false;
            }
        }

        Sync(force: false);
    }

    static void OnPlayModeStateChanged(PlayModeStateChange change)
    {
        // DontSaveInEditor objects survive the edit-to-play scene reload, which would drop four ghost
        // actors on top of the real party in the Start room.
        if (change == PlayModeStateChange.ExitingEditMode)
            Clear();
    }

    // ---------------------------------------------------------------- sampling

    /// <summary>
    /// Samples every actor and then snaps it back onto its marker. The snap is not optional:
    /// <see cref="AnimationMode.SampleAnimationClip"/> applies a clip's root curves, while the runtime
    /// stage intro state forces root motion off, so without it an authored pickup clip would drift
    /// away from the marker in the preview but not in game.
    /// </summary>
    static void Sync(bool force)
    {
        if (activeRig == null || Actors.Count == 0)
            return;

        int hash = ComputeSyncHash();
        if (!force && hash == lastSyncHash)
            return;
        lastSyncHash = hash;

        if (!AnimationMode.InAnimationMode())
        {
            AnimationMode.StartAnimationMode();
            animationModeOwned = true;
        }

        AnimationClip cameraClip = activeRig.CameraClip;
        bool hasCameraSample = cameraClip != null && activeRig.CameraAnimationRoot != null;

        AnimationMode.BeginSampling();
        try
        {
            if (hasCameraSample)
            {
                AnimationMode.SampleAnimationClip(
                    activeRig.CameraAnimationRoot.gameObject, cameraClip, previewTime);
            }

            for (int i = 0; i < Actors.Count; i++)
            {
                PreviewActor actor = Actors[i];
                if (actor.Root == null || actor.Clip == null)
                    continue;

                float clipTime = actor.Clip.length > 0f
                    ? Mathf.Min(previewTime, actor.Clip.length)
                    : 0f;

                AnimationMode.SampleAnimationClip(actor.AnimatorRoot, actor.Clip, clipTime);
            }
        }
        finally
        {
            AnimationMode.EndSampling();
        }

        SnapActorsToMarkers();
        RestoreSceneDirtiness();
        SceneView.RepaintAll();
    }

    static void SnapActorsToMarkers()
    {
        for (int i = 0; i < Actors.Count; i++)
        {
            PreviewActor actor = Actors[i];
            if (actor.Root == null || actor.Marker == null)
                continue;

            actor.Root.transform.SetPositionAndRotation(actor.Marker.Position, actor.Marker.Rotation);
        }
    }

    /// <summary>Cheap change detector so dragging a marker resyncs but an idle editor does not.</summary>
    static int ComputeSyncHash()
    {
        var hash = new System.HashCode();
        hash.Add(previewTime);

        for (int i = 0; i < Actors.Count; i++)
        {
            StageIntroActorMarker marker = Actors[i].Marker;
            if (marker == null)
                continue;

            hash.Add(marker.Position);
            hash.Add(marker.Rotation);
        }

        return hash.ToHashCode();
    }

    // ---------------------------------------------------------------- scene dirtiness

    static void RestoreSceneDirtiness()
    {
        if (previewSceneWasDirty || !previewScene.IsValid() || !previewScene.isDirty)
            return;

        // Clearing a scene's dirty flag has no public API in Unity 6, so resolve it reflectively and
        // simply leave the flag alone if the internal entry point ever disappears.
        StageIntroEditorReflection.ClearSceneDirtiness(previewScene);
    }
}
