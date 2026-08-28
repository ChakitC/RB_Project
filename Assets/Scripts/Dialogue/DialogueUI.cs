using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The dialogue canvas in the preloaded presentation scene. Draw order, back to front:
/// the frozen gameplay view (rendered by the untouched main camera), a black dim layer, a vignette,
/// three independently composed actor RenderTextures, and the dialogue box.
///
/// Everything here runs unscaled — the world is at timeScale 0 for the whole conversation.
/// </summary>
[DisallowMultipleComponent]
public sealed class DialogueUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField, Tooltip("Faded in and out at the start and end of a conversation.")]
    private CanvasGroup rootGroup;

    [Header("Background treatment")]
    [SerializeField, Tooltip("Fullscreen black. Its alpha is the dim amount.")]
    private Image dimImage;

    [SerializeField, Range(0f, 1f), Tooltip("How far the frozen gameplay view is dimmed. 0.5-0.6 is " +
                                            "the authored range for v1.")]
    private float dimAlpha = 0.55f;

    [SerializeField, Tooltip("Fullscreen vignette overlay. Optional.")]
    private Image vignetteImage;

    [Header("Actors")]
    [SerializeField, Tooltip("The three stage slots whose RawImages are composed by this canvas.")]
    private List<DialogueStageSlot> actorSlots = new();

    [SerializeField, Min(0.1f), Tooltip("Scale of the speaker's portrait. 1 is the ceiling worth " +
                                        "using: the RenderTexture is authored at exactly the band's " +
                                        "pixel size, so anything above 1 upsamples and makes the one " +
                                        "portrait the player is looking at the blurriest on screen.")]
    private float speakingScale = 1f;

    [SerializeField, Min(0.1f), Tooltip("Scale of a portrait that is not speaking. Portraits scale " +
                                        "about their bottom edge, so this shortens them from the top " +
                                        "— into the empty headroom — and never lifts them off the " +
                                        "floor line they share.")]
    private float listeningScale = 0.94f;
    [SerializeField, Tooltip("Extra shift applied to the speaker's portrait. Kept at zero by default: " +
                             "lifting the speaker breaks the head line that the camera framing works " +
                             "hard to establish. Emphasis reads through scale, tint and draw order.")]
    private Vector2 speakingOffset = Vector2.zero;
    [SerializeField] private Color speakingTint = Color.white;
    [SerializeField] private Color listeningTint = new(0.78f, 0.78f, 0.78f, 1f);
    [SerializeField, Min(0f), Tooltip("Unscaled seconds to blend UI emphasis when the speaker changes.")]
    private float emphasisBlendSeconds = 0.18f;

    [Header("Actor entrance / exit")]
    [SerializeField, Tooltip("Where a portrait sits while it is off stage. It slides from here on the " +
                             "way in and back to here on the way out.")]
    private Vector2 offStageOffset = new(0f, -90f);

    [SerializeField, Min(0f)] private float exitSeconds = 0.16f;
    [SerializeField, Min(0f)] private float enterSeconds = 0.22f;

    [Header("Dialogue box")]
    [SerializeField] private GameObject dialogueBoxRoot;
    [SerializeField] private TMP_Text speakerLabel;
    [SerializeField] private TMP_Text bodyLabel;

    [SerializeField, Tooltip("Shown once the line has finished revealing.")]
    private GameObject advanceIndicator;

    [Header("Skip prompt")]
    [SerializeField] private CanvasGroup skipGroup;
    [SerializeField] private TMP_Text skipLabel;
    [SerializeField, Tooltip("Filled Image driven by hold progress.")]
    private Image skipProgressFill;

    [Header("Voice")]
    [SerializeField, Tooltip("Must have Ignore Listener Pause enabled so voice plays while the world " +
                             "is frozen.")]
    private AudioSource voiceSource;

    readonly DialogueTypewriter typewriter = new();

    Sprite generatedFillSprite;
    Vector2[] portraitBasePositions;
    int[] portraitSiblingIndices;

    // Emphasis is kept separately from what is written to the RectTransform, so the entrance/exit
    // slide can be composed on top without the next frame's lerp reading its own offset back as the
    // current value and compounding it.
    Vector2[] emphasisPositions;
    /// <summary>Portraits scale about their bottom edge; see CapturePortraitLayout.</summary>
    static readonly Vector2 PortraitPivot = new(0.5f, 0f);

    Vector3[] emphasisScales;
    Color[] emphasisTints;
    float[] portraitOnStage;
    Vector2[] authoredAnchorMin;
    Vector2[] authoredAnchorMax;
    DialogueSlot? emphasizedSlot;

    public bool IsRevealing => typewriter.IsRevealing;

    internal float ExitSeconds => exitSeconds;
    internal float EnterSeconds => enterSeconds;

    void Awake()
    {
        EnsureSkipFillDrawable();
        CapturePortraitLayout();

        if (rootGroup != null)
        {
            rootGroup.alpha = 0f;
            rootGroup.interactable = false;
            rootGroup.blocksRaycasts = false;
        }

        gameObject.SetActive(false);
    }

    /// <summary>Turns the canvas on at zero alpha, ready for the director to fade it in.</summary>
    internal void Open(bool showSkipPrompt, string skipBindingLabel)
    {
        gameObject.SetActive(true);
        CapturePortraitLayout();
        LayoutOccupiedPortraits();
        SetSpeaker(null, true);
        ResetPortraitTransitions();

        if (dimImage != null)
        {
            Color color = dimImage.color;
            dimImage.color = new Color(color.r, color.g, color.b, dimAlpha);
            dimImage.enabled = true;
        }

        if (vignetteImage != null)
            vignetteImage.enabled = true;

        if (dialogueBoxRoot != null)
            dialogueBoxRoot.SetActive(true);

        if (skipGroup != null)
            skipGroup.alpha = showSkipPrompt ? 1f : 0f;

        if (skipLabel != null && showSkipPrompt)
            skipLabel.text = $"Hold [{skipBindingLabel}] to skip";

        SetSkipProgress(0f);
        SetAdvanceIndicatorVisible(false);

        if (rootGroup != null)
            rootGroup.alpha = 0f;
    }

    internal void SetAlpha(float alpha)
    {
        if (rootGroup != null)
            rootGroup.alpha = Mathf.Clamp01(alpha);
    }

    internal void Close()
    {
        typewriter.Clear();
        StopVoice();

        for (int i = 0; i < actorSlots.Count; i++)
        {
            DialogueStageSlot stageSlot = actorSlots[i];
            if (stageSlot != null && stageSlot.PortraitImage != null)
                stageSlot.PortraitImage.enabled = false;
        }

        if (rootGroup != null)
            rootGroup.alpha = 0f;

        gameObject.SetActive(false);
    }

    /// <summary>Starts revealing a line. The voice is fired here but never advances the line.</summary>
    internal void ShowLine(string speakerName, DialogueLine line, float charactersPerSecond)
    {
        if (speakerLabel != null)
        {
            speakerLabel.text = speakerName ?? string.Empty;
            speakerLabel.enabled = !string.IsNullOrWhiteSpace(speakerName);
        }

        typewriter.Begin(bodyLabel, line != null ? line.text : string.Empty, charactersPerSecond);
        SetAdvanceIndicatorVisible(!typewriter.IsRevealing);

        StopVoice();
        if (voiceSource != null && line != null && line.voice != null)
            voiceSource.PlayOneShot(line.voice);
    }

    internal void Tick(float unscaledDeltaTime)
    {
        TickPortraitEmphasis(unscaledDeltaTime);

        bool wasRevealing = typewriter.IsRevealing;
        typewriter.Tick(unscaledDeltaTime);

        if (wasRevealing && !typewriter.IsRevealing)
            SetAdvanceIndicatorVisible(true);
    }

    /// <summary>Reveals the rest of the current line at once.</summary>
    internal void CompleteReveal()
    {
        typewriter.CompleteImmediately();
        SetAdvanceIndicatorVisible(true);
    }

    internal void SetSkipProgress(float progress01)
    {
        if (skipProgressFill == null)
            return;

        float amount = Mathf.Clamp01(progress01);
        skipProgressFill.fillAmount = amount;

        // A Filled Image still draws its full rect at fillAmount 0, so an idle hold would leave a
        // solid bar across the prompt. Hide it outright instead.
        skipProgressFill.enabled = amount > 0.001f;
    }

    /// <summary>
    /// Moves portrait emphasis entirely in UI space. The speaking image is drawn above its peers;
    /// actor transforms and camera framing remain unchanged.
    /// </summary>
    internal void SetSpeaker(DialogueSlot? speaking, bool immediate = false)
    {
        emphasizedSlot = speaking;

        for (int i = 0; i < actorSlots.Count; i++)
        {
            DialogueStageSlot stageSlot = actorSlots[i];
            RawImage image = stageSlot != null ? stageSlot.PortraitImage : null;
            if (image == null)
                continue;

            if (portraitSiblingIndices != null && i < portraitSiblingIndices.Length)
                image.transform.SetSiblingIndex(portraitSiblingIndices[i]);
        }

        if (speaking.HasValue)
        {
            for (int i = 0; i < actorSlots.Count; i++)
            {
                DialogueStageSlot stageSlot = actorSlots[i];
                if (stageSlot != null && stageSlot.Slot == speaking.Value && stageSlot.PortraitImage != null)
                {
                    stageSlot.PortraitImage.transform.SetAsLastSibling();
                    break;
                }
            }
        }

        if (immediate)
            ApplyPortraitEmphasis(1f);
    }

    /// <summary>
    /// Centres the group of portraits that actually have an actor, so a one- or two-character
    /// conversation is not stranded against the edge of the screen by the authored three-slot layout.
    ///
    /// Each portrait keeps a band exactly one third of the width — its RenderTexture is drawn into
    /// that band, so widening it would stretch the character — and only the band's position moves.
    /// Occupied slots stay in Left→Right order, so a cast member never jumps sides.
    /// </summary>
    internal void LayoutOccupiedPortraits()
    {
        CapturePortraitLayout();

        const float BandWidth = 1f / 3f;

        int occupied = 0;
        for (int i = 0; i < actorSlots.Count; i++)
        {
            DialogueStageSlot stageSlot = actorSlots[i];
            if (stageSlot != null && stageSlot.Occupant != null && stageSlot.PortraitImage != null)
                occupied++;
        }

        if (occupied == 0)
            return;

        int placed = 0;
        for (int i = 0; i < actorSlots.Count; i++)
        {
            DialogueStageSlot stageSlot = actorSlots[i];
            RawImage image = stageSlot != null ? stageSlot.PortraitImage : null;
            if (image == null)
                continue;

            RectTransform rect = image.rectTransform;

            // An empty slot is already hidden by the stage; put it back on its authored band so the
            // next conversation starts from a known layout.
            if (stageSlot.Occupant == null)
            {
                if (authoredAnchorMin != null && i < authoredAnchorMin.Length)
                {
                    rect.anchorMin = authoredAnchorMin[i];
                    rect.anchorMax = authoredAnchorMax[i];
                }

                continue;
            }

            // Centre the run of occupied bands on the screen: with three it reproduces the authored
            // thirds exactly, with two it straddles the middle, with one it lands dead centre.
            float centre = 0.5f + (placed - (occupied - 1) * 0.5f) * BandWidth;
            placed++;

            Vector2 min = authoredAnchorMin != null && i < authoredAnchorMin.Length
                ? authoredAnchorMin[i]
                : rect.anchorMin;
            Vector2 max = authoredAnchorMax != null && i < authoredAnchorMax.Length
                ? authoredAnchorMax[i]
                : rect.anchorMax;

            rect.anchorMin = new Vector2(centre - BandWidth * 0.5f, min.y);
            rect.anchorMax = new Vector2(centre + BandWidth * 0.5f, max.y);
        }
    }

    bool IsSized(System.Array array) => array != null && array.Length == actorSlots.Count;

    void CapturePortraitLayout()
    {
        // Every array has to be checked, not just one: guarding on a single array means that if the
        // slot list changes — or a hot reload adds a field to an already-constructed instance — the
        // capture is skipped and the rest stay null, which then throws on the first portrait write.
        if (IsSized(portraitBasePositions) &&
            IsSized(portraitSiblingIndices) &&
            IsSized(authoredAnchorMin) &&
            IsSized(authoredAnchorMax) &&
            IsSized(emphasisPositions) &&
            IsSized(emphasisScales) &&
            IsSized(emphasisTints) &&
            IsSized(portraitOnStage))
        {
            return;
        }

        portraitBasePositions = new Vector2[actorSlots.Count];
        portraitSiblingIndices = new int[actorSlots.Count];
        authoredAnchorMin = new Vector2[actorSlots.Count];
        authoredAnchorMax = new Vector2[actorSlots.Count];
        emphasisPositions = new Vector2[actorSlots.Count];
        emphasisScales = new Vector3[actorSlots.Count];
        emphasisTints = new Color[actorSlots.Count];
        portraitOnStage = new float[actorSlots.Count];

        for (int i = 0; i < actorSlots.Count; i++)
        {
            DialogueStageSlot stageSlot = actorSlots[i];
            RawImage image = stageSlot != null ? stageSlot.PortraitImage : null;
            if (image == null)
                continue;

            // Portraits are authored to fill the screen exactly, so emphasis scaling has no room to
            // grow into: a centre pivot spends a shrink on BOTH edges and lifts the character off the
            // bottom of the screen, which is the gap that reads as "the frame does not reach the
            // floor". Scaling about the bottom edge instead spends the whole shrink on the top, where
            // every cell has empty headroom above the head, and pins the floor line so the cast never
            // appears to hover at different heights. The bands are stretched (sizeDelta 0), so moving
            // the pivot leaves the rect exactly where it is.
            image.rectTransform.pivot = PortraitPivot;

            portraitBasePositions[i] = image.rectTransform.anchoredPosition;
            portraitSiblingIndices[i] = image.transform.GetSiblingIndex();
            authoredAnchorMin[i] = image.rectTransform.anchorMin;
            authoredAnchorMax[i] = image.rectTransform.anchorMax;
            emphasisPositions[i] = image.rectTransform.anchoredPosition;
            emphasisScales[i] = image.rectTransform.localScale;
            emphasisTints[i] = image.color;
            portraitOnStage[i] = stageSlot.Occupant != null ? 1f : 0f;
        }
    }

    void TickPortraitEmphasis(float unscaledDeltaTime)
    {
        float blend = emphasisBlendSeconds <= 0f
            ? 1f
            : Mathf.Clamp01(unscaledDeltaTime / emphasisBlendSeconds);

        ApplyPortraitEmphasis(blend);
    }

    void ApplyPortraitEmphasis(float blend)
    {
        for (int i = 0; i < actorSlots.Count; i++)
        {
            DialogueStageSlot stageSlot = actorSlots[i];
            RawImage image = stageSlot != null ? stageSlot.PortraitImage : null;
            if (image == null)
                continue;

            bool isSpeaking = emphasizedSlot.HasValue && emphasizedSlot.Value == stageSlot.Slot;
            float targetScale = isSpeaking ? speakingScale : listeningScale;
            Vector2 basePosition = portraitBasePositions != null && i < portraitBasePositions.Length
                ? portraitBasePositions[i]
                : Vector2.zero;
            Vector2 targetPosition = basePosition + (isSpeaking ? speakingOffset : Vector2.zero);
            Color targetTint = isSpeaking ? speakingTint : listeningTint;

            emphasisPositions[i] = Vector2.Lerp(emphasisPositions[i], targetPosition, blend);
            emphasisScales[i] = Vector3.Lerp(emphasisScales[i], Vector3.one * targetScale, blend);
            emphasisTints[i] = Color.Lerp(emphasisTints[i], targetTint, blend);

            WritePortrait(i);
        }
    }

    /// <summary>
    /// Composes emphasis with the entrance/exit slide and writes the result. `portraitOnStage` runs
    /// 0 (parked off stage, invisible) to 1 (fully on stage).
    /// </summary>
    void WritePortrait(int index)
    {
        DialogueStageSlot stageSlot = actorSlots[index];
        RawImage image = stageSlot != null ? stageSlot.PortraitImage : null;
        if (image == null)
            return;

        float onStage = portraitOnStage != null && index < portraitOnStage.Length
            ? portraitOnStage[index]
            : 1f;

        image.rectTransform.anchoredPosition =
            emphasisPositions[index] + offStageOffset * (1f - onStage);
        image.rectTransform.localScale = emphasisScales[index];

        Color tint = emphasisTints[index];
        image.color = new Color(tint.r, tint.g, tint.b, tint.a * onStage);
    }

    /// <summary>
    /// Drives the entrance/exit slide for one slot. 0 parks the portrait off stage and invisible,
    /// 1 puts it fully on. The director tweens this on the unscaled clock either side of a swap.
    /// </summary>
    internal void SetPortraitOnStage(DialogueSlot slot, float onStage01)
    {
        CapturePortraitLayout();

        for (int i = 0; i < actorSlots.Count; i++)
        {
            if (actorSlots[i] == null || actorSlots[i].Slot != slot)
                continue;

            portraitOnStage[i] = Mathf.Clamp01(onStage01);
            WritePortrait(i);
            return;
        }
    }

    /// <summary>Snaps every occupied slot on stage and every empty one off. Used when a session opens.</summary>
    internal void ResetPortraitTransitions()
    {
        CapturePortraitLayout();

        for (int i = 0; i < actorSlots.Count; i++)
        {
            DialogueStageSlot stageSlot = actorSlots[i];
            if (stageSlot == null || stageSlot.PortraitImage == null)
                continue;

            portraitOnStage[i] = stageSlot.Occupant != null ? 1f : 0f;
            WritePortrait(i);
        }
    }

    /// <summary>
    /// A Filled Image with no sprite ignores fillAmount entirely and draws the whole rect. Rather
    /// than make that an authoring trap, give it a plain white sprite when none was assigned; an
    /// author who assigns a styled sprite keeps theirs.
    /// </summary>
    void EnsureSkipFillDrawable()
    {
        if (skipProgressFill == null || skipProgressFill.sprite != null)
            return;

        var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { name = "DialogueSkipFill" };
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();

        generatedFillSprite = Sprite.Create(
            texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        generatedFillSprite.name = "DialogueSkipFill";
        skipProgressFill.sprite = generatedFillSprite;
    }

    void OnDestroy()
    {
        if (generatedFillSprite == null)
            return;

        Texture2D texture = generatedFillSprite.texture;
        Destroy(generatedFillSprite);
        if (texture != null)
            Destroy(texture);

        generatedFillSprite = null;
    }

    void SetAdvanceIndicatorVisible(bool visible)
    {
        if (advanceIndicator != null)
            advanceIndicator.SetActive(visible);
    }

    void StopVoice()
    {
        if (voiceSource != null && voiceSource.isPlaying)
            voiceSource.Stop();
    }

    /// <summary>Every authoring problem that would leave the dialogue box unreadable.</summary>
    public void CollectValidationIssues(List<string> issues)
    {
        if (issues == null)
            throw new System.ArgumentNullException(nameof(issues));

        if (rootGroup == null)
            issues.Add("Dialogue UI root CanvasGroup is not assigned; the fade has nothing to drive.");

        if (dimImage == null)
            issues.Add("Dim Image is not assigned; the frozen gameplay view will not be dimmed.");

        var seen = new HashSet<DialogueSlot>();
        for (int i = 0; i < actorSlots.Count; i++)
        {
            DialogueStageSlot stageSlot = actorSlots[i];
            if (stageSlot == null)
            {
                issues.Add($"Dialogue UI actor slot reference {i} is missing.");
                continue;
            }

            if (!seen.Add(stageSlot.Slot))
                issues.Add($"Dialogue UI actor slot '{stageSlot.Slot}' is assigned more than once.");

            if (stageSlot.PortraitImage == null)
                issues.Add($"Dialogue UI actor slot '{stageSlot.Slot}' has no RawImage.");
        }

        foreach (DialogueSlot required in (DialogueSlot[])System.Enum.GetValues(typeof(DialogueSlot)))
        {
            if (!seen.Contains(required))
                issues.Add($"Dialogue UI actor slot '{required}' is missing.");
        }

        if (bodyLabel == null)
            issues.Add("Body label is not assigned; lines cannot be typed out.");

        if (speakerLabel == null)
            issues.Add("Speaker label is not assigned.");

        if (voiceSource != null && !voiceSource.ignoreListenerPause)
        {
            issues.Add("Voice AudioSource should have Ignore Listener Pause enabled so voice keeps " +
                       "playing while the world is frozen.");
        }

        if (dimAlpha < 0.5f || dimAlpha > 0.6f)
            issues.Add($"Dim alpha is {dimAlpha:0.00}; the authored range for v1 is 0.50-0.60.");
    }
}
