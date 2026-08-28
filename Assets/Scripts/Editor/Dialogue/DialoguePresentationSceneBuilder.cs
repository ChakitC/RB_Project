using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Builds the additive DialoguePresentation scene from scratch: the stage root with its three slots,
/// one portrait camera per isolated slot, the dialogue-only light rig, the inactive clone staging root, and the
/// dialogue canvas. Everything is wired up and the scene is added to Build Settings, so the only
/// remaining authoring work is art tuning (slot offsets, light values, box styling).
///
/// Re-running it rebuilds the scene from scratch, so tuned values are overwritten — tune in the
/// scene, not by re-running this.
/// </summary>
public static class DialoguePresentationSceneBuilder
{
    const string ScenePath = "Assets/Scenes/DialoguePresentation.unity";
    const string DataFolder = "Assets/Data/Dialogue";
    const string LightRigPath = DataFolder + "/DefaultDialogueLightRig.asset";
    const string ProfileDatabasePath = DataFolder + "/DialogueProfileDatabase.asset";

    [MenuItem("Tools/Dialogue/Build DialoguePresentation Scene")]
    public static void Build()
    {
        if (File.Exists(ScenePath) &&
            !Application.isBatchMode &&
            !EditorUtility.DisplayDialog(
                "Rebuild DialoguePresentation?",
                $"'{ScenePath}' already exists. Rebuilding replaces it and discards any tuning done " +
                "in the scene.\n\nRebuild?",
                "Rebuild",
                "Cancel"))
        {
            return;
        }

        BuildScene();
    }

    static void BuildScene()
    {
        DialogueProjectSetup.SetUpLayers();

        DialogueLightRigSO lightRig = EnsureAsset<DialogueLightRigSO>(LightRigPath);
        DialogueProfileDatabaseSO profileDatabase = EnsureAsset<DialogueProfileDatabaseSO>(ProfileDatabasePath);

        Scene loadedPresentation = SceneManager.GetSceneByPath(ScenePath);
        if (loadedPresentation.IsValid() && loadedPresentation.isLoaded)
            EditorSceneManager.CloseScene(loadedPresentation, true);

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

        DialogueStage stage = BuildStage(out DialogueStageSlot[] slots,
                                         out GameObject lightRigRoot,
                                         out Light fillLight,
                                         out Transform cloneStaging);

        DialogueUI ui = BuildCanvas(slots, out CanvasGroup rootGroup);

        var director = stage.gameObject.AddComponent<DialogueDirector>();

        WireStage(stage, slots, lightRigRoot, fillLight, lightRig, cloneStaging);
        WireDirector(director, stage, ui, profileDatabase);

        Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddSceneToBuildSettings(ScenePath);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"[Dialogue] Built '{ScenePath}'. Tune slot anchors, light values, and the dialogue box " +
            "in the scene; add each character's CharacterDialogueAnimationProfile to " +
            $"'{ProfileDatabasePath}'.");
    }

    // ---------------------------------------------------------------- stage

    static DialogueStage BuildStage(
        out DialogueStageSlot[] slots,
        out GameObject lightRigRoot,
        out Light fillLight,
        out Transform cloneStaging)
    {
        var rootObject = new GameObject("DialogueStageRoot");
        var stage = rootObject.AddComponent<DialogueStage>();

        // Distance IS the isolation. Clones share layer 0 with the rest of the game (see
        // DialogueLayers), so nothing but this gap stops a gameplay camera from drawing the stage.
        // -5000 was not enough: measured against the live gameplay camera, the nearest clone sat
        // 5000.0 units away against a 5000 far plane — inside it, and saved only by the camera
        // happening to look horizontally rather than down. -20000 leaves a 15000-unit margin.
        rootObject.transform.position = new Vector3(0f, -20000f, 0f);

        var slotsRoot = new GameObject("Slots");
        slotsRoot.transform.SetParent(rootObject.transform, false);

        slots = new[]
        {
            // Each camera has a 30m far clip; 100m spacing guarantees it cannot catch a neighbour.
            CreateSlot(slotsRoot.transform, DialogueSlot.Left, new Vector3(-100f, 0f, 0f)),
            CreateSlot(slotsRoot.transform, DialogueSlot.Center, Vector3.zero),
            CreateSlot(slotsRoot.transform, DialogueSlot.Right, new Vector3(100f, 0f, 0f)),
        };

        lightRigRoot = new GameObject("LightRig");
        lightRigRoot.transform.SetParent(rootObject.transform, false);

        for (int i = 0; i < slots.Length; i++)
            CreateSlotLights(lightRigRoot.transform, slots[i]);

        fillLight = CreateLight(lightRigRoot.transform, "Fill", new Vector3(0f, 3f, -2f),
                                new Vector3(35f, 0f, 0f), LightType.Directional, 0.6f);

        var stagingObject = new GameObject("CloneStaging");
        stagingObject.transform.SetParent(rootObject.transform, false);
        stagingObject.SetActive(false);
        cloneStaging = stagingObject.transform;

        return stage;
    }

    static DialogueStageSlot CreateSlot(Transform parent, DialogueSlot slot, Vector3 position)
    {
        var slotObject = new GameObject($"Slot_{slot}");
        slotObject.transform.SetParent(parent, false);
        slotObject.transform.SetLocalPositionAndRotation(position, Quaternion.identity);

        var component = slotObject.AddComponent<DialogueStageSlot>();
        SetSerialized(component, "slot", (int)slot);

        var anchorObject = new GameObject("ActorAnchor");
        anchorObject.transform.SetParent(slotObject.transform, false);
        anchorObject.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, 180f, 0f));
        SetSerialized(component, "anchor", anchorObject.transform);

        var cameraObject = new GameObject("PortraitCamera");
        cameraObject.transform.SetParent(slotObject.transform, false);
        // This height is THE height for every character — framing no longer equalises it — so it has
        // to frame the whole cast on its own. orthographicSize 1.2 shows +/-1.2m, so 1.39 covers
        // 0.19 up to 2.59: the tallest hat in the cast still clears the top, and the crop lands just
        // above the feet, which sit behind the dialogue box anyway. Tuned in Play Mode against the
        // live cast; raising it wastes the top of every cell on empty sky.
        cameraObject.transform.SetLocalPositionAndRotation(new Vector3(0f, 1.39f, -4f), Quaternion.identity);

        var portraitCamera = cameraObject.AddComponent<Camera>();
        portraitCamera.clearFlags = CameraClearFlags.SolidColor;
        portraitCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
        portraitCamera.cullingMask = DialogueLayers.ActorLayerMask;
        portraitCamera.orthographic = true;
        portraitCamera.orthographicSize = 1.2f;   // overwritten per actor by the framing pass
        portraitCamera.fieldOfView = 34f;
        portraitCamera.nearClipPlane = 0.1f;
        portraitCamera.farClipPlane = 30f;
        portraitCamera.useOcclusionCulling = false;
        portraitCamera.enabled = false;
        SetSerialized(component, "portraitCamera", portraitCamera);

        return component;
    }

    static void CreateSlotLights(Transform lightRigRoot, DialogueStageSlot slot)
    {
        Transform anchor = slot.transform;

        Light key = CreateLight(lightRigRoot, $"{slot.Slot}_Key",
                                anchor.localPosition + new Vector3(0.8f, 2.1f, -1.6f),
                                new Vector3(28f, 200f, 0f), LightType.Spot, 2.2f);

        Light rim = CreateLight(lightRigRoot, $"{slot.Slot}_Rim",
                                anchor.localPosition + new Vector3(-1.1f, 2.3f, 1.4f),
                                new Vector3(30f, -25f, 0f), LightType.Spot, 1.4f);

        SetSerialized(slot, "keyLight", key);
        SetSerialized(slot, "rimLight", rim);
    }

    static Light CreateLight(
        Transform parent, string name, Vector3 localPosition, Vector3 euler, LightType type, float intensity)
    {
        var lightObject = new GameObject(name);
        lightObject.transform.SetParent(parent, false);
        lightObject.transform.SetLocalPositionAndRotation(localPosition, Quaternion.Euler(euler));

        var light = lightObject.AddComponent<Light>();
        light.type = type;
        light.intensity = intensity;
        light.range = 12f;
        light.spotAngle = 55f;
        light.shadows = LightShadows.None;

        // Dialogue lights only touch the dialogue rendering channel, never the frozen world.
        light.renderingLayerMask = (int)DialogueLayers.DialogueRenderingLayerMask;
        return light;
    }

    // ---------------------------------------------------------------- canvas

    static DialogueUI BuildCanvas(DialogueStageSlot[] slots, out CanvasGroup rootGroup)
    {
        var canvasObject = new GameObject(
            "DialogueCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler),
            typeof(GraphicRaycaster), typeof(CanvasGroup));

        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 31000;

        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        // Match height, so an ultrawide screen widens the dialogue box instead of shrinking the text.
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 1f;

        rootGroup = canvasObject.GetComponent<CanvasGroup>();
        rootGroup.alpha = 0f;
        rootGroup.interactable = false;
        rootGroup.blocksRaycasts = false;

        Image dim = CreateFullscreenImage(canvasObject.transform, "Dim", new Color(0f, 0f, 0f, 0.55f));
        Image vignette = CreateFullscreenImage(canvasObject.transform, "Vignette", new Color(0f, 0f, 0f, 0.35f));

        var actorsObject = new GameObject("Actors", typeof(RectTransform));
        actorsObject.transform.SetParent(canvasObject.transform, false);
        StretchFull(actorsObject.GetComponent<RectTransform>());

        for (int i = 0; i < slots.Length; i++)
        {
            var imageObject = new GameObject($"Actor_{slots[i].Slot}", typeof(RectTransform), typeof(RawImage));
            imageObject.transform.SetParent(actorsObject.transform, false);

            RectTransform imageRect = imageObject.GetComponent<RectTransform>();
            imageRect.anchorMin = new Vector2(i / 3f, 0f);
            imageRect.anchorMax = new Vector2((i + 1f) / 3f, 1f);
            imageRect.offsetMin = Vector2.zero;
            imageRect.offsetMax = Vector2.zero;

            // Bottom pivot: emphasis scaling shortens a listening portrait from the top, into the
            // empty headroom, instead of lifting it off the bottom of the screen. DialogueUI enforces
            // this at runtime too; authoring it here keeps the editor view honest.
            imageRect.pivot = new Vector2(0.5f, 0f);

            RawImage image = imageObject.GetComponent<RawImage>();
            image.raycastTarget = false;
            image.enabled = false;
            SetSerialized(slots[i], "portraitImage", image);
        }

        GameObject box = CreateDialogueBox(
            canvasObject.transform, out TMP_Text speaker, out TMP_Text body, out GameObject advance);

        CreateSkipPrompt(canvasObject.transform, out CanvasGroup skipGroup, out TMP_Text skipLabel,
                         out Image skipFill);

        var voiceObject = new GameObject("VoiceSource", typeof(AudioSource));
        voiceObject.transform.SetParent(canvasObject.transform, false);
        var voiceSource = voiceObject.GetComponent<AudioSource>();
        voiceSource.playOnAwake = false;
        // The world is frozen and the listener may be paused; voice has to keep playing regardless.
        voiceSource.ignoreListenerPause = true;

        var ui = canvasObject.AddComponent<DialogueUI>();
        SetSerialized(ui, "rootGroup", rootGroup);
        SetSerialized(ui, "dimImage", dim);
        SetSerialized(ui, "vignetteImage", vignette);
        SetSerializedList(ui, "actorSlots", slots);
        SetSerialized(ui, "dialogueBoxRoot", box);
        SetSerialized(ui, "speakerLabel", speaker);
        SetSerialized(ui, "bodyLabel", body);
        SetSerialized(ui, "advanceIndicator", advance);
        SetSerialized(ui, "skipGroup", skipGroup);
        SetSerialized(ui, "skipLabel", skipLabel);
        SetSerialized(ui, "skipProgressFill", skipFill);
        SetSerialized(ui, "voiceSource", voiceSource);
        SetSerialized(ui, "offStageOffset", new Vector2(0f, -90f));
        SetSerialized(ui, "exitSeconds", 0.16f);
        SetSerialized(ui, "enterSeconds", 0.22f);
        // Must match DialogueStage.emphasisBlendSeconds below: the portrait and its 3D lights are
        // two halves of one speaker change and drift apart if they run on different durations.
        SetSerialized(ui, "emphasisBlendSeconds", 0.25f);
        return ui;
    }

    static GameObject CreateDialogueBox(
        Transform parent, out TMP_Text speaker, out TMP_Text body, out GameObject advance)
    {
        var boxObject = new GameObject("DialogueBox", typeof(RectTransform), typeof(Image));
        boxObject.transform.SetParent(parent, false);

        var rect = boxObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 60f);
        rect.sizeDelta = new Vector2(1500f, 280f);

        var background = boxObject.GetComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.78f);
        background.raycastTarget = false;

        // Every label is anchor-stretched inside the box. Sizing them by sizeDelta around a centred
        // pivot pushed the speaker name off the left edge of the screen.
        speaker = CreateStretchedLabel(
            boxObject.transform, "Speaker", 40f, FontStyles.Bold, TextAlignmentOptions.TopLeft,
            new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(44f, -74f), new Vector2(-44f, -16f));

        body = CreateStretchedLabel(
            boxObject.transform, "Body", 32f, FontStyles.Normal, TextAlignmentOptions.TopLeft,
            Vector2.zero, Vector2.one, new Vector2(44f, 34f), new Vector2(-44f, -80f));

        advance = CreateStretchedLabel(
            boxObject.transform, "AdvanceIndicator", 28f, FontStyles.Normal,
            TextAlignmentOptions.BottomRight,
            new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-80f, 20f), new Vector2(-20f, 60f))
            .gameObject;
        advance.GetComponent<TMP_Text>().text = "▼";
        advance.SetActive(false);

        return boxObject;
    }

    static void CreateSkipPrompt(
        Transform parent, out CanvasGroup group, out TMP_Text label, out Image fill)
    {
        var promptObject = new GameObject("SkipPrompt", typeof(RectTransform), typeof(CanvasGroup));
        promptObject.transform.SetParent(parent, false);

        var rect = promptObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-48f, -40f);
        rect.sizeDelta = new Vector2(420f, 64f);

        group = promptObject.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        label = CreateStretchedLabel(
            promptObject.transform, "Label", 26f, FontStyles.Normal, TextAlignmentOptions.MidlineRight,
            new Vector2(0f, 0.35f), Vector2.one, Vector2.zero, Vector2.zero);

        var fillObject = new GameObject("ProgressFill", typeof(RectTransform), typeof(Image));
        fillObject.transform.SetParent(promptObject.transform, false);
        var fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = new Vector2(1f, 0.22f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        fill = fillObject.GetComponent<Image>();
        fill.color = Color.white;
        fill.raycastTarget = false;
        // A Filled Image needs a sprite to sweep; the author assigns one when styling the prompt.
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillAmount = 0f;
    }

    /// <summary>
    /// Creates a TMP label stretched between two anchors. Offsets, not sizeDelta, so the label always
    /// stays inside its parent regardless of screen width.
    /// </summary>
    static TMP_Text CreateStretchedLabel(
        Transform parent, string name, float fontSize, FontStyles style, TextAlignmentOptions alignment,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var labelObject = new GameObject(name, typeof(RectTransform));
        labelObject.transform.SetParent(parent, false);

        var text = labelObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.text = string.Empty;

        RectTransform rect = text.rectTransform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        return text;
    }

    static Image CreateFullscreenImage(Transform parent, string name, Color color)
    {
        var imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        StretchFull(imageObject.GetComponent<RectTransform>());

        var image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    // ---------------------------------------------------------------- wiring

    static void WireStage(
        DialogueStage stage,
        DialogueStageSlot[] slots,
        GameObject lightRigRoot,
        Light fillLight,
        DialogueLightRigSO lightRig,
        Transform cloneStaging)
    {
        var serialized = new SerializedObject(stage);

        SerializedProperty slotList = serialized.FindProperty("slots");
        slotList.arraySize = slots.Length;
        for (int i = 0; i < slots.Length; i++)
            slotList.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];

        serialized.FindProperty("lightRigRoot").objectReferenceValue = lightRigRoot;
        serialized.FindProperty("fillLight").objectReferenceValue = fillLight;
        serialized.FindProperty("defaultLightRig").objectReferenceValue = lightRig;
        // Framing is head-anchored; these are the values a rebuilt scene should carry.
        serialized.FindProperty("framingViewHeight").floatValue = 2.4f;
        serialized.FindProperty("emphasisBlendSeconds").floatValue = 0.25f;
        serialized.FindProperty("cloneStagingRoot").objectReferenceValue = cloneStaging;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetSerializedList(Object target, string propertyName, Object[] values)
    {
        var serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null || !property.isArray)
        {
            Debug.LogError($"[Dialogue] '{target.GetType().Name}' has no serialized list '{propertyName}'.");
            return;
        }

        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static void WireDirector(
        DialogueDirector director,
        DialogueStage stage,
        DialogueUI ui,
        DialogueProfileDatabaseSO profileDatabase)
    {
        var serialized = new SerializedObject(director);
        serialized.FindProperty("stage").objectReferenceValue = stage;
        serialized.FindProperty("ui").objectReferenceValue = ui;
        serialized.FindProperty("profileDatabase").objectReferenceValue = profileDatabase;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetSerialized(Object target, string propertyName, Object value)
    {
        var serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
        {
            Debug.LogError($"[Dialogue] '{target.GetType().Name}' has no serialized field '{propertyName}'.");
            return;
        }

        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetSerialized(Object target, string propertyName, int value)
    {
        var serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
        {
            Debug.LogError($"[Dialogue] '{target.GetType().Name}' has no serialized field '{propertyName}'.");
            return;
        }

        property.enumValueIndex = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetSerialized(Object target, string propertyName, Vector2 value)
    {
        var serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
        {
            Debug.LogError($"[Dialogue] '{target.GetType().Name}' has no serialized field '{propertyName}'.");
            return;
        }

        property.vector2Value = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetSerialized(Object target, string propertyName, float value)
    {
        var serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
        {
            Debug.LogError($"[Dialogue] '{target.GetType().Name}' has no serialized field '{propertyName}'.");
            return;
        }

        property.floatValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static T EnsureAsset<T>(string path) where T : ScriptableObject
    {
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        if (existing != null)
            return existing;

        Directory.CreateDirectory(Path.GetDirectoryName(path));
        var created = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(created, path);
        return created;
    }

    static void AddSceneToBuildSettings(string scenePath)
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        for (int i = 0; i < scenes.Length; i++)
        {
            if (scenes[i].path == scenePath)
                return;
        }

        var updated = new EditorBuildSettingsScene[scenes.Length + 1];
        scenes.CopyTo(updated, 0);
        updated[scenes.Length] = new EditorBuildSettingsScene(scenePath, true);
        EditorBuildSettings.scenes = updated;
    }
}
