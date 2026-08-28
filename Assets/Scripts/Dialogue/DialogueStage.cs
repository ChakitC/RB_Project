using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The presentation stage that lives in the preloaded DialoguePresentation scene. Each of its three
/// isolated cells owns one actor, one portrait camera, and one runtime RenderTexture; the dialogue UI
/// composes those textures over the frozen gameplay view.
/// </summary>
[DisallowMultipleComponent]
public sealed class DialogueStage : MonoBehaviour
{
    [Header("Slots")]
    [SerializeField] private List<DialogueStageSlot> slots = new();

    [Header("Lighting")]
    [SerializeField, Tooltip("Parent of every dialogue light. Disabled between conversations.")]
    private GameObject lightRigRoot;

    [SerializeField, Tooltip("Ambient fill for the whole stage.")]
    private Light fillLight;

    [SerializeField] private DialogueLightRigSO defaultLightRig;

    [Header("Staging")]
    [SerializeField, Tooltip("Inactive root that clones are built under, so no stripped gameplay " +
                             "component ever gets an Awake. Must stay disabled in the scene.")]
    private Transform cloneStagingRoot;

    [SerializeField, Min(0.1f), Tooltip("How tall a slice of the world a portrait shows, in metres. " +
                                        "Fixed rather than derived from the actor, so a tall hat no " +
                                        "longer zooms that character out.")]
    private float framingViewHeight = 2.4f;

    [SerializeField, Min(0f), Tooltip("Unscaled seconds to blend the 3D key/rim lights when the " +
                                      "speaker changes. Keep it matched with " +
                                      "DialogueUI.emphasisBlendSeconds so the portrait and its lights " +
                                      "change together.")]
    private float emphasisBlendSeconds = 0.25f;

    readonly List<DialogueActorVisual> activeActors = new();
    readonly Dictionary<DialogueSlot, DialogueStageSlot> slotLookup = new();

    /// <summary>Start and end intensity of the light blend that is currently running for one slot.</summary>
    struct LightBlend
    {
        public float KeyFrom;
        public float KeyTo;
        public float RimFrom;
        public float RimTo;
    }

    readonly Dictionary<DialogueSlot, LightBlend> lightBlends = new();
    float lightProgress = 1f;

    // Slots whose camera still needs the confirming fit described in ResolvePendingFraming.
    readonly List<DialogueSlot> pendingFraming = new();

    DialogueLightRigSO activeRig;

    // Held for the whole session: a mid-conversation swap has to resolve a character the same way
    // BeginSession did, without the director handing the tables over again on every line.
    IReadOnlyDictionary<string, DialogueCastSource> sessionCastSources;
    Func<string, CharacterDialogueAnimationProfileSO> sessionProfileResolver;

    public DialogueSlot? SpeakingSlot { get; private set; }
    public bool HasSession { get; private set; }

    void Awake()
    {
        BuildSlotLookup();
        SetPresentationEnabled(false);
    }

    void OnDestroy()
    {
        EndSession();
    }

    void BuildSlotLookup()
    {
        slotLookup.Clear();
        for (int i = 0; i < slots.Count; i++)
        {
            DialogueStageSlot stageSlot = slots[i];
            if (stageSlot != null && !slotLookup.ContainsKey(stageSlot.Slot))
                slotLookup.Add(stageSlot.Slot, stageSlot);
        }
    }

    /// <summary>
    /// Builds an actor clone for every cast entry the live party can supply, poses them, fits the
    /// camera in that actor's isolated cell, and turns the occupied cells on. A missing live actor
    /// leaves its slot empty rather than failing the sequence.
    /// </summary>
    public void BeginSession(
        DialogueSequenceSO sequence,
        IReadOnlyDictionary<string, DialogueCastSource> castSources,
        Func<string, CharacterDialogueAnimationProfileSO> profileResolver)
    {
        if (sequence == null)
            return;

        EndSession();
        BuildSlotLookup();

        activeRig = sequence.LightRig != null ? sequence.LightRig : defaultLightRig;
        sessionCastSources = castSources;
        sessionProfileResolver = profileResolver;
        HasSession = true;

        IReadOnlyList<DialogueCastEntry> cast = sequence.Cast;
        for (int i = 0; cast != null && i < cast.Count; i++)
        {
            DialogueCastEntry entry = cast[i];
            if (entry == null || !entry.IsValid)
                continue;

            TryOccupySlot(entry.slot, entry.characterId, entry.idlePoseId);
        }

        SetPresentationEnabled(true);
        ApplyLighting(null);
    }

    /// <summary>
    /// Emphasizes the speaker's lights and poses them for the line. UI-space portrait emphasis is
    /// handled independently by <see cref="DialogueUI"/>.
    /// </summary>
    public void SetSpeaker(string characterId, string poseId)
    {
        if (!HasSession)
            return;

        DialogueSlot? speaking = null;
        for (int i = 0; i < activeActors.Count; i++)
        {
            DialogueActorVisual actor = activeActors[i];
            if (actor == null)
                continue;

            bool isSpeaker = actor.Matches(characterId);

            if (isSpeaker)
            {
                speaking = actor.Slot;
                actor.PlayPoseId(poseId);
            }
            else
            {
                actor.PlayIdlePose();
            }

        }

        // The camera is deliberately NOT refitted here. Framing is settled once, when the actor takes
        // the slot, and then held for the rest of the conversation. Refitting per line re-measured the
        // head bone mid-animation, so every slot — including the slots of characters who were not
        // even involved — jumped sideways by however far the idle loop had swayed. Emphasis is the
        // portrait's job, not the camera's.
        ApplyLighting(speaking);
    }

    /// <summary>True when a slot currently has an actor standing in it.</summary>
    public bool IsSlotOccupied(DialogueSlot slot)
    {
        return slotLookup.TryGetValue(slot, out DialogueStageSlot stageSlot) &&
               stageSlot != null &&
               stageSlot.Occupant != null;
    }

    /// <summary>
    /// Whether this change would actually alter the stage. Lets the caller skip the entrance/exit
    /// animation for a line that merely restates the current line-up.
    /// </summary>
    public bool WillChangeSlot(DialogueStageChange change)
    {
        if (!HasSession || change == null)
            return false;

        if (!slotLookup.TryGetValue(change.slot, out DialogueStageSlot stageSlot) || stageSlot == null)
            return false;

        DialogueActorVisual occupant = stageSlot.Occupant;

        if (change.IsClear)
            return occupant != null;

        return occupant == null || !occupant.Matches(change.characterId);
    }

    /// <summary>
    /// Applies one mid-conversation stage change. Placing a character in an occupied slot takes the
    /// previous occupant off, so the three slots are themselves the cap on how many can be on stage.
    /// Re-placing whoever is already standing there is a no-op, so a line that restates the current
    /// line-up does not rebuild clones or re-fit cameras for nothing.
    /// </summary>
    public void ApplyStageChange(DialogueStageChange change)
    {
        if (!HasSession || change == null)
            return;

        if (change.IsClear)
        {
            ClearSlot(change.slot);
            return;
        }

        TryOccupySlot(change.slot, change.characterId, change.idlePoseId);
    }

    bool TryOccupySlot(DialogueSlot slot, string characterId, string idlePoseId)
    {
        if (!slotLookup.TryGetValue(slot, out DialogueStageSlot stageSlot) || stageSlot == null)
            return false;

        // Already standing there — leave the clone alone rather than rebuilding an identical one.
        if (stageSlot.Occupant != null && stageSlot.Occupant.Matches(characterId))
            return true;

        if (sessionCastSources == null ||
            !sessionCastSources.TryGetValue(characterId, out DialogueCastSource source) ||
            !source.IsValid)
        {
            return false;
        }

        // The pose profile belongs to the character, not to the key used to reach them, so a role
        // cast still poses whoever is actually filling that role.
        CharacterDialogueAnimationProfileSO profile =
            sessionProfileResolver?.Invoke(source.CharacterId);
        DialogueActorVisual actor = DialogueActorCloneFactory.TryCreateClone(
            source, characterId, slot, profile, cloneStagingRoot);

        if (actor == null)
            return false;

        // Only evict once the replacement actually exists, so a failed swap cannot empty the stage.
        ClearSlot(slot);

        actor.transform.SetParent(stageSlot.Anchor, false);
        actor.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        actor.gameObject.SetActive(true);

        actor.SetIdlePose(idlePoseId);
        actor.PlayIdlePose();

        stageSlot.SetOccupant(actor);
        activeActors.Add(actor);
        PrepareOccupiedSlot(stageSlot, actor);
        return true;
    }

    void ClearSlot(DialogueSlot slot)
    {
        if (!slotLookup.TryGetValue(slot, out DialogueStageSlot stageSlot) || stageSlot == null)
            return;

        DialogueActorVisual occupant = stageSlot.Occupant;
        if (occupant == null)
            return;

        activeActors.Remove(occupant);
        stageSlot.ClearOccupant();
        stageSlot.SetLightsEnabled(false);
        Destroy(occupant.gameObject);
    }

    /// <summary>Destroys every clone and switches the stage back off. Safe to call when idle.</summary>
    public void EndSession()
    {
        SetPresentationEnabled(false);

        for (int i = 0; i < activeActors.Count; i++)
        {
            DialogueActorVisual actor = activeActors[i];
            if (actor != null)
                Destroy(actor.gameObject);
        }

        activeActors.Clear();
        lightBlends.Clear();
        lightProgress = 1f;
        pendingFraming.Clear();

        foreach (KeyValuePair<DialogueSlot, DialogueStageSlot> pair in slotLookup)
        {
            if (pair.Value == null)
                continue;

            pair.Value.ClearOccupant();
            pair.Value.SetLightsEnabled(false);
            pair.Value.ReleaseOutputTexture();
        }

        SpeakingSlot = null;
        activeRig = null;
        sessionCastSources = null;
        sessionProfileResolver = null;
        HasSession = false;
    }

    void PrepareOccupiedSlot(DialogueStageSlot stageSlot, DialogueActorVisual actor)
    {
        int width = Mathf.CeilToInt(Mathf.Max(2, Screen.width) / 3f);
        int height = Mathf.Max(2, Screen.height);
        RenderTexture texture = stageSlot.EnsureOutputTexture(width, height);
        FitCameraToActor(actor, stageSlot, texture);

        // Fit again on the next tick. This one runs the frame the actor is placed, and a clone built
        // under the inactive staging root has not necessarily had its animator applied yet, so the
        // head bone can still be reading its bind position. Since the camera is never refitted
        // afterwards, a bad first measurement would stick for the whole conversation.
        if (!pendingFraming.Contains(stageSlot.Slot))
            pendingFraming.Add(stageSlot.Slot);
    }

    /// <summary>
    /// Frames one cell's camera on its actor. The actor never moves off its authored cell origin.
    ///
    /// Height is deliberately **not** equalised: the camera sits at its authored height for every
    /// slot, so a taller character reads as taller. Only the sideways axis tracks the actor, anchored
    /// on the head bone so a model whose pivot is off-centre still has its face in the middle of its
    /// band.
    ///
    /// The portrait cameras are **orthographic** — `DialogueStageSlot` pins that every session — so
    /// `orthographicSize` is the half-height and camera distance does not affect scale at all.
    ///
    /// Bounds are used only for standing the camera back far enough and for the clip planes; they
    /// deliberately do not widen the frame, or a character holding a parasol would be pushed back
    /// until it rendered at half everyone else's size.
    /// </summary>
    void FitCameraToActor(DialogueActorVisual actor, DialogueStageSlot stageSlot, RenderTexture texture)
    {
        Camera camera = stageSlot != null ? stageSlot.PortraitCamera : null;
        if (actor == null || camera == null)
            return;

        actor.EvaluatePose();
        if (!actor.TryGetWorldBounds(out Bounds bounds))
            return;

        CharacterDialogueAnimationProfileSO profile = actor.Profile;
        float viewHeight = profile != null && profile.FramingViewHeight > 0f
            ? profile.FramingViewHeight
            : framingViewHeight;
        // Deliberately NOT widened to contain the actor. A portrait band is far taller than it is
        // wide, so fitting a held prop — a parasol, a slung rifle — pushed those characters back until
        // they rendered at less than half the size of everyone else. Framing is a fixed slice of the
        // world instead: everybody the same size, props crop at the edges the way a portrait should.
        // A character that genuinely needs more room gets it from its own profile override.
        float halfHeight = viewHeight * 0.5f;

        // Orthographic: half-height IS orthographicSize, and distance does not affect scale at all, so
        // the camera only has to stand far enough back to keep the actor inside its clip planes.
        camera.orthographicSize = halfHeight;

        Vector3 forward = camera.transform.forward;
        float depth = bounds.extents.magnitude + 2f;
        Vector3 fitted = bounds.center - forward * depth;

        Transform parent = camera.transform.parent;
        Vector3 authored = stageSlot.AuthoredCameraLocalPosition;
        bool hasHead = actor.TryGetHeadPosition(out Vector3 head);

        // Horizontally the head is still the anchor: the model sits at a different lateral offset from
        // its pivot on every character — Feno's head is 21cm off his root, Abbygail's is dead on it —
        // so pivot-centred framing leaves some faces off to one side, and bounds would drag the camera
        // toward whatever prop they are holding. With no head bone, fall back to the cell centreline.
        if (hasHead)
            fitted.x = head.x;

        if (parent != null)
        {
            Vector3 local = parent.InverseTransformPoint(fitted);

            // Height is NOT equalised. The camera sits where it was authored, identical for every
            // slot, so a taller character simply reads as taller — which is the point. Only the
            // sideways axis tracks the actor.
            local.y = authored.y;

            if (!hasHead)
                local.x = authored.x;

            camera.transform.localPosition = local;
        }
        else
        {
            if (!hasHead)
                fitted.x = authored.x;

            camera.transform.position = fitted;
        }

        camera.nearClipPlane = 0.01f;
        camera.farClipPlane = depth + bounds.extents.magnitude * 2f + 2f;
    }

    void SetPresentationEnabled(bool enabled)
    {
        foreach (KeyValuePair<DialogueSlot, DialogueStageSlot> pair in slotLookup)
        {
            if (pair.Value != null)
                pair.Value.SetRenderingEnabled(enabled);
        }

        if (lightRigRoot != null)
            lightRigRoot.SetActive(enabled);
    }

    void ApplyLighting(DialogueSlot? speaking)
    {
        SpeakingSlot = speaking;
        float listenerScale = activeRig != null ? activeRig.ListenerIntensityScale : 0.35f;

        if (fillLight != null && activeRig != null)
        {
            fillLight.color = activeRig.FillColor;
            fillLight.intensity = activeRig.FillIntensity;
            fillLight.renderingLayerMask = (int)DialogueLayers.DialogueRenderingLayerMask;
        }

        foreach (KeyValuePair<DialogueSlot, DialogueStageSlot> pair in slotLookup)
        {
            DialogueStageSlot stageSlot = pair.Value;
            if (stageSlot == null)
                continue;

            if (stageSlot.Occupant == null)
            {
                stageSlot.SetLightsEnabled(false);
                lightBlends.Remove(stageSlot.Slot);
                continue;
            }

            bool isSpeaking = speaking.HasValue && speaking.Value == stageSlot.Slot;
            stageSlot.SetLightsEnabled(true);

            float weight = isSpeaking ? 1f : listenerScale;
            var blend = new LightBlend
            {
                KeyFrom = stageSlot.KeyLight != null ? stageSlot.KeyLight.intensity : 0f,
                KeyTo = activeRig != null ? activeRig.KeyIntensity * weight : 0f,
                RimFrom = stageSlot.RimLight != null ? stageSlot.RimLight.intensity : 0f,
                RimTo = activeRig != null ? activeRig.RimIntensity * weight : 0f,
            };

            lightBlends[stageSlot.Slot] = blend;
            ApplyLightConstants(stageSlot.KeyLight, true);
            ApplyLightConstants(stageSlot.RimLight, false);
        }

        // Restart the blend from whatever the lights are showing now, so a speaker change that lands
        // mid-blend carries on from the visible brightness instead of snapping back.
        lightProgress = 0f;
        ApplyLightBlend();
    }

    /// <summary>
    /// Eases the key/rim intensities toward the brightness the current speaker calls for.
    ///
    /// Dropping a listener straight from full to `ListenerIntensityScale` in one frame was the
    /// loudest cut in the whole speaker change — a brightness step reads far more sharply than the
    /// portrait scale it was supposed to accompany. Same clock and same curve as the UI emphasis.
    /// </summary>
    public void Tick(float unscaledDeltaTime)
    {
        if (!HasSession)
            return;

        ResolvePendingFraming();

        if (lightProgress >= 1f)
            return;

        lightProgress = emphasisBlendSeconds <= 0f
            ? 1f
            : Mathf.Clamp01(lightProgress + unscaledDeltaTime / emphasisBlendSeconds);

        ApplyLightBlend();
    }

    /// <summary>
    /// Re-fits a camera one tick after its actor was placed, then leaves it alone for good.
    ///
    /// The fit in <see cref="PrepareOccupiedSlot"/> happens on the frame the clone is parented in,
    /// when its animator may not have run yet. One tick later the pose is genuinely on the bones, so
    /// this second measurement is the one that lands. It is not a per-frame correction: each slot
    /// appears in the queue once, when it is filled.
    /// </summary>
    void ResolvePendingFraming()
    {
        for (int i = pendingFraming.Count - 1; i >= 0; i--)
        {
            if (!slotLookup.TryGetValue(pendingFraming[i], out DialogueStageSlot stageSlot) ||
                stageSlot == null ||
                stageSlot.Occupant == null)
            {
                pendingFraming.RemoveAt(i);
                continue;
            }

            FitCameraToActor(stageSlot.Occupant, stageSlot, stageSlot.OutputTexture);
            pendingFraming.RemoveAt(i);
        }
    }

    void ApplyLightBlend()
    {
        float blend = Mathf.SmoothStep(0f, 1f, lightProgress);

        foreach (KeyValuePair<DialogueSlot, LightBlend> pair in lightBlends)
        {
            if (!slotLookup.TryGetValue(pair.Key, out DialogueStageSlot stageSlot) || stageSlot == null)
                continue;

            LightBlend value = pair.Value;
            if (stageSlot.KeyLight != null)
                stageSlot.KeyLight.intensity = Mathf.Lerp(value.KeyFrom, value.KeyTo, blend);

            if (stageSlot.RimLight != null)
                stageSlot.RimLight.intensity = Mathf.Lerp(value.RimFrom, value.RimTo, blend);
        }
    }

    void ApplyLightConstants(Light light, bool isKey)
    {
        if (light == null || activeRig == null)
            return;

        light.color = isKey ? activeRig.KeyColor : activeRig.RimColor;
        light.renderingLayerMask = (int)DialogueLayers.DialogueRenderingLayerMask;
    }

    /// <summary>Every authoring problem that would stop the stage from presenting a conversation.</summary>
    public void CollectValidationIssues(List<string> issues)
    {
        if (issues == null)
            throw new ArgumentNullException(nameof(issues));

        if (cloneStagingRoot == null)
            issues.Add("Clone staging root is not assigned.");
        else if (cloneStagingRoot.gameObject.activeSelf)
            issues.Add("Clone staging root must stay disabled so stripped components never wake up.");

        if (lightRigRoot == null)
            issues.Add("Light rig root is not assigned.");

        if (defaultLightRig == null)
            issues.Add("Default DialogueLightRigSO is not assigned.");

        var seen = new HashSet<DialogueSlot>();
        for (int i = 0; i < slots.Count; i++)
        {
            DialogueStageSlot stageSlot = slots[i];
            if (stageSlot == null)
            {
                issues.Add($"Slot reference {i} is missing.");
                continue;
            }

            if (!seen.Add(stageSlot.Slot))
                issues.Add($"Stage slot '{stageSlot.Slot}' is assigned more than once.");

            stageSlot.CollectValidationIssues(issues);
        }

        foreach (DialogueSlot required in (DialogueSlot[])Enum.GetValues(typeof(DialogueSlot)))
        {
            if (!seen.Contains(required))
                issues.Add($"Stage slot '{required}' is missing.");
        }

    }
}
