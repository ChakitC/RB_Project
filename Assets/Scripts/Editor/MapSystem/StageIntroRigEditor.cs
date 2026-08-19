using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Inspector for <see cref="StageIntroRig"/> with an in-editor preview so authors can block the
/// group shot without entering Play Mode.
///
/// The preview actors themselves live in <see cref="StageIntroPreviewSession"/>, not here, so they
/// survive selecting a marker to drag it. This inspector only owns transient view state: the Scene
/// view lock and the Game view Solo, both of which are meaningless once the rig is deselected.
/// </summary>
[CustomEditor(typeof(StageIntroRig))]
public sealed class StageIntroRigEditor : Editor
{
    readonly List<string> validationIssues = new();

    StageIntroPreviewRoster roster;
    CharacterDatabase characterDatabase;
    string[] characterLabels = System.Array.Empty<string>();
    CharacterStats[] characterOptions = System.Array.Empty<CharacterStats>();

    bool lockSceneViewToIntroCamera;

    bool gameViewSolo;
    bool introCameraStateCaptured;
    bool introCameraWasActive;
    Camera previewBrainCamera;
    CinemachineBrain previewBrain;
    bool previewBrainAdded;
    Vector3 previewBrainCameraPosition;
    Quaternion previewBrainCameraRotation;

    void OnEnable()
    {
        roster = StageIntroPreviewRoster.LoadFor((StageIntroRig)target);
        BuildCharacterOptions();
        EditorApplication.update += OnEditorUpdate;
    }

    void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;

        // The preview itself intentionally outlives this inspector; only the view overrides are ours.
        ReleaseGameViewSolo(target as StageIntroRig);
        roster?.Save();
    }

    void OnEditorUpdate()
    {
        if (target == null)
            return;

        // The session samples on its own clock, so the Scene view lock has to follow from here.
        if (lockSceneViewToIntroCamera)
            AlignSceneViewToIntroCamera((StageIntroRig)target);

        if (StageIntroPreviewSession.IsPlaying)
        {
            if (gameViewSolo)
                EditorApplication.QueuePlayerLoopUpdate();
            Repaint();
        }
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var rig = (StageIntroRig)target;

        EditorGUILayout.Space();
        DrawValidation(rig);

        EditorGUILayout.Space();
        DrawRoster(rig);

        EditorGUILayout.Space();
        DrawPreviewControls(rig);
    }

    void DrawValidation(StageIntroRig rig)
    {
        validationIssues.Clear();
        rig.CollectValidationIssues(validationIssues);

        if (validationIssues.Count == 0)
        {
            EditorGUILayout.HelpBox("Stage intro rig is valid.", MessageType.Info);
            return;
        }

        EditorGUILayout.HelpBox(
            "Stage intro will be skipped (fail-open):\n• " + string.Join("\n• ", validationIssues),
            MessageType.Warning);
    }

    // ------------------------------------------------------------------ roster

    void BuildCharacterOptions()
    {
        characterDatabase = LoadCharacterDatabase();

        var labels = new List<string> { "(none)" };
        var options = new List<CharacterStats> { null };

        if (characterDatabase != null)
        {
            for (int i = 0; i < characterDatabase.characters.Count; i++)
            {
                CharacterStats stats = characterDatabase.characters[i];
                if (stats == null)
                    continue;

                labels.Add(string.IsNullOrWhiteSpace(stats.characterName) ? stats.name : stats.characterName);
                options.Add(stats);
            }
        }

        characterLabels = labels.ToArray();
        characterOptions = options.ToArray();
    }

    static CharacterDatabase LoadCharacterDatabase()
    {
        string[] guids = AssetDatabase.FindAssets($"t:{nameof(CharacterDatabase)}");
        if (guids.Length == 0)
            return null;

        return AssetDatabase.LoadAssetAtPath<CharacterDatabase>(AssetDatabase.GUIDToAssetPath(guids[0]));
    }

    void DrawRoster(StageIntroRig rig)
    {
        EditorGUILayout.LabelField("Preview Roster", EditorStyles.boldLabel);

        if (characterDatabase == null)
        {
            EditorGUILayout.HelpBox("No CharacterDatabase asset found, so the character list is empty.",
                MessageType.Warning);
            return;
        }

        bool respawnNeeded = false;
        bool clipsChanged = false;

        IReadOnlyList<StageIntroPreviewRoster.Slot> slots = roster.Slots;
        for (int i = 0; i < slots.Count; i++)
        {
            StageIntroPreviewRoster.Slot slot = slots[i];

            EditorGUILayout.LabelField(slot.Role.ToString(), EditorStyles.miniBoldLabel);
            EditorGUI.indentLevel++;

            int current = Mathf.Max(0, System.Array.IndexOf(characterOptions, slot.Character));
            EditorGUI.BeginChangeCheck();
            int picked = EditorGUILayout.Popup("Character", current, characterLabels);
            if (EditorGUI.EndChangeCheck())
            {
                slot.Character = characterOptions[picked];
                roster.Save();
                respawnNeeded = true;
            }

            EditorGUI.BeginChangeCheck();
            slot.ClipOverride = (AnimationClip)EditorGUILayout.ObjectField(
                "Intro Clip (Preview)", slot.ClipOverride, typeof(AnimationClip), false);
            if (EditorGUI.EndChangeCheck())
                clipsChanged = true;

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.HelpBox(
            "Leave a clip empty to preview what the game actually plays: the character's anim profile " +
            "Stage Intro Clip, or locomotion idle when it has none. Characters that share an anim " +
            "profile share the pose. An override here is a scratch value — it is not saved and never " +
            "reaches the build.",
            MessageType.None);

        if (!StageIntroPreviewSession.OwnsRig(rig))
            return;

        // Deferred: instantiating or destroying actors inside a layout pass breaks the IMGUI layout.
        StageIntroPreviewRoster capturedRoster = roster;
        if (respawnNeeded)
            EditorApplication.delayCall += () => StageIntroPreviewSession.Spawn(rig, capturedRoster);
        else if (clipsChanged)
            EditorApplication.delayCall += () => StageIntroPreviewSession.RefreshClips(capturedRoster);
    }

    // ------------------------------------------------------------------ preview controls

    void DrawPreviewControls(StageIntroRig rig)
    {
        EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

        bool hasClip = rig.CameraClip != null && rig.CameraClip.length > 0f;
        bool hasPreview = StageIntroPreviewSession.OwnsRig(rig) && StageIntroPreviewSession.HasPreview;

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(hasPreview))
            {
                if (GUILayout.Button("Spawn Preview Party"))
                    StageIntroPreviewSession.Spawn(rig, roster);
            }

            using (new EditorGUI.DisabledScope(!hasPreview))
            {
                if (GUILayout.Button("Clear Preview"))
                    StageIntroPreviewSession.Clear();
            }
        }

        using (new EditorGUI.DisabledScope(!hasClip))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(StageIntroPreviewSession.IsPlaying ? "Pause" : "Play"))
                    StageIntroPreviewSession.SetPlaying(!StageIntroPreviewSession.IsPlaying);

                if (GUILayout.Button("Stop"))
                {
                    StageIntroPreviewSession.SetPlaying(false);
                    StageIntroPreviewSession.SetTime(0f);
                }
            }

            float length = hasClip ? rig.CameraClip.length : 1f;
            EditorGUI.BeginChangeCheck();
            float newTime = EditorGUILayout.Slider("Time", StageIntroPreviewSession.PreviewTime, 0f, length);
            if (EditorGUI.EndChangeCheck())
            {
                StageIntroPreviewSession.SetPlaying(false);
                StageIntroPreviewSession.SetTime(newTime);
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUI.BeginChangeCheck();
            lockSceneViewToIntroCamera = EditorGUILayout.ToggleLeft(
                "Look Through Intro Camera", lockSceneViewToIntroCamera);
            if (EditorGUI.EndChangeCheck() && lockSceneViewToIntroCamera)
                AlignSceneViewToIntroCamera(rig);

            if (GUILayout.Button("Frame Now", GUILayout.Width(90f)))
                AlignSceneViewToIntroCamera(rig);
        }

        bool canSolo = rig.IntroCamera != null && ResolvePreviewBrainCamera(rig) != null;
        using (new EditorGUI.DisabledScope(!canSolo))
        {
            EditorGUI.BeginChangeCheck();
            bool wantSolo = EditorGUILayout.ToggleLeft(
                "Preview In Game View (Solo)", gameViewSolo && canSolo);
            if (EditorGUI.EndChangeCheck())
                SetGameViewSolo(rig, wantSolo);
        }

        EditorGUILayout.HelpBox(
            canSolo
                ? "\"Preview In Game View\" Solos the intro camera so the Game view shows the real shot " +
                  "at the real aspect ratio while you scrub. This project adds the CinemachineBrain at " +
                  "runtime, so the preview temporarily enables the intro camera and adds a brain to the " +
                  "scene camera. Both are undone when the toggle is switched off — do not save the scene " +
                  "while it is on."
                : "No scene Camera found, so Game view preview is unavailable. Prefab Mode has no camera: " +
                  "use \"Look Through Intro Camera\" here, or drag the Start room prefab into a gameplay " +
                  "scene to preview in Game view.",
            MessageType.None);

        if (!hasClip)
        {
            EditorGUILayout.HelpBox(
                "Assign a Camera Clip to enable playback and scrubbing. " +
                "Spawning the preview party still works so markers can be blocked out.",
                MessageType.Info);
        }

        if (hasPreview)
        {
            EditorGUILayout.HelpBox(
                "Preview actors stay alive while you select and drag markers, and follow them live. " +
                "They are removed by Clear Preview, closing the prefab or scene, a recompile, and " +
                "entering Play Mode.",
                MessageType.None);
        }
    }

    // ------------------------------------------------------------------ game view solo

    /// <summary>
    /// Solos the intro camera so the Game view renders the real shot in Edit Mode.
    /// </summary>
    void SetGameViewSolo(StageIntroRig rig, bool enabled)
    {
        if (rig.IntroCamera == null)
        {
            gameViewSolo = false;
            return;
        }

        if (enabled)
        {
            if (!EnsurePreviewBrain(rig))
            {
                gameViewSolo = false;
                return;
            }

            if (!introCameraStateCaptured)
            {
                introCameraWasActive = rig.IntroCamera.gameObject.activeSelf;
                introCameraStateCaptured = true;
            }

            if (!rig.IntroCamera.gameObject.activeSelf)
                rig.IntroCamera.gameObject.SetActive(true);

            CinemachineCore.SoloCamera = rig.IntroCamera;
            gameViewSolo = true;
        }
        else
        {
            ReleaseGameViewSolo(rig);
        }

        EditorApplication.QueuePlayerLoopUpdate();
    }

    void ReleaseGameViewSolo(StageIntroRig rig)
    {
        gameViewSolo = false;

        if (rig == null || rig.IntroCamera == null)
        {
            introCameraStateCaptured = false;
            ReleasePreviewBrain();
            return;
        }

        if (ReferenceEquals(CinemachineCore.SoloCamera, rig.IntroCamera))
            CinemachineCore.SoloCamera = null;

        if (introCameraStateCaptured)
        {
            rig.IntroCamera.gameObject.SetActive(introCameraWasActive);
            introCameraStateCaptured = false;
        }

        ReleasePreviewBrain();
    }

    /// <summary>
    /// Finds the scene Camera the preview should render through. `GameplayCameraController` only adds
    /// the <see cref="CinemachineBrain"/> at runtime, so Edit Mode normally has a camera and no brain.
    /// </summary>
    static Camera ResolvePreviewBrainCamera(StageIntroRig rig)
    {
        Scene rigScene = rig.gameObject.scene;
        Camera[] cameras = Object.FindObjectsByType<Camera>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        Camera best = null;
        for (int i = 0; i < cameras.Length; i++)
        {
            Camera candidate = cameras[i];
            if (candidate == null || !candidate.gameObject.scene.IsValid())
                continue;

            // Never hijack the Scene view's own camera.
            if (candidate.cameraType != CameraType.Game)
                continue;

            if (rigScene.IsValid() && candidate.gameObject.scene != rigScene)
                continue;

            if (candidate.CompareTag("MainCamera"))
                return candidate;

            if (best == null || candidate.depth > best.depth)
                best = candidate;
        }

        return best;
    }

    bool EnsurePreviewBrain(StageIntroRig rig)
    {
        if (previewBrain != null)
            return true;

        previewBrainCamera = ResolvePreviewBrainCamera(rig);
        if (previewBrainCamera == null)
            return false;

        previewBrainCameraPosition = previewBrainCamera.transform.position;
        previewBrainCameraRotation = previewBrainCamera.transform.rotation;

        previewBrain = previewBrainCamera.GetComponent<CinemachineBrain>();
        if (previewBrain == null)
        {
            previewBrain = previewBrainCamera.gameObject.AddComponent<CinemachineBrain>();
            previewBrain.hideFlags = HideFlags.DontSaveInEditor;
            previewBrainAdded = true;
        }

        return true;
    }

    void ReleasePreviewBrain()
    {
        if (previewBrainAdded && previewBrain != null)
            DestroyImmediate(previewBrain);

        previewBrainAdded = false;
        previewBrain = null;

        // The brain drives the camera transform, so put the authored pose back.
        if (previewBrainCamera != null)
        {
            previewBrainCamera.transform.SetPositionAndRotation(
                previewBrainCameraPosition, previewBrainCameraRotation);
        }

        previewBrainCamera = null;
    }

    // ------------------------------------------------------------------ scene view

    /// <summary>
    /// Puts the Scene view camera at the intro camera's sampled pose and lens. This is the closest
    /// WYSIWYG framing available without a CinemachineBrain, which Prefab Mode has no room for.
    /// </summary>
    void AlignSceneViewToIntroCamera(StageIntroRig rig)
    {
        SceneView view = SceneView.lastActiveSceneView;
        if (view == null)
            return;

        Transform pose = rig.IntroCamera != null
            ? rig.IntroCamera.transform
            : rig.CameraAnimationRoot;
        if (pose == null)
            return;

        if (rig.IntroCamera != null)
            view.cameraSettings.fieldOfView = rig.IntroCamera.Lens.FieldOfView;

        view.in2DMode = false;
        view.orthographic = false;

        // A near-zero pivot size parks the Scene view camera essentially on the pivot point.
        view.LookAtDirect(pose.position + pose.forward * 0.01f, pose.rotation, 0.01f);
        view.Repaint();
    }
}
