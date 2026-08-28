using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Owns one dialogue session end to end: the safe-state gate, exclusive ownership of the cinematic
/// stage, the world freeze, the actor stage, the UI, input, and cleanup.
///
/// Only one conversation runs at a time and a request that arrives while the cinematic stage is busy
/// is rejected outright — nothing is queued. Any abort path (death, scene change, disable, forced
/// interruption) restores the world but never reports completion, so a trigger's OnCompleted scene
/// events cannot fire off a conversation the player did not finish.
/// </summary>
[DisallowMultipleComponent]
public sealed class DialogueDirector : MonoBehaviour
{
    static DialogueDirector _instance;

    public static bool HasInstance => _instance != null;

    public static DialogueDirector Instance
    {
        get
        {
            if (_instance == null)
                _instance = FindFirstObjectByType<DialogueDirector>(FindObjectsInactive.Include);

            return _instance;
        }
    }

    [Header("Presentation")]
    [SerializeField] private DialogueStage stage;
    [SerializeField] private DialogueUI ui;
    [SerializeField] private DialogueProfileDatabaseSO profileDatabase;

    [Header("Timing")]
    [SerializeField, Range(0.2f, 0.3f), Tooltip("Unscaled seconds for the open and close fades.")]
    private float fadeSeconds = 0.25f;

    [Header("Debug")]
    [SerializeField] private bool logLifecycle;

    readonly DialogueWorldPauseScope pauseScope = new();
    readonly Dictionary<string, DialogueCastSource> castSources =
        new(StringComparer.OrdinalIgnoreCase);
    readonly List<CharacteContext> frozenContexts = new();
    readonly List<DialogueSlot> changingSlots = new();

    IReadOnlyList<DialogueStageActorSource> pendingExtraActors;
    DialogueInputController input;
    DialogueSequenceSO activeSequence;
    CharacteContext activeInitiator;
    StateHub watchedLifeHub;
    Action completedCallback;
    Coroutine playRoutine;

    bool completionInvoked;
    bool abortRequested;

    public bool IsPlaying { get; private set; }
    public DialogueSequenceSO ActiveSequence => activeSequence;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(this);
            return;
        }

        _instance = this;
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
    }

    void OnDestroy()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;

        if (IsPlaying)
            Abort("Director destroyed");

        if (_instance == this)
            _instance = null;
    }

    void OnDisable()
    {
        if (IsPlaying)
            Abort("Director disabled");
    }

    void OnActiveSceneChanged(Scene from, Scene to)
    {
        if (IsPlaying)
            Abort("Active scene changed");
    }

    /// <summary>
    /// Starts a conversation. Returns false — without touching the world — when the initiator is not
    /// in a safe state, the cinematic stage is busy, or the sequence is unplayable.
    /// <paramref name="onCompleted"/> only ever runs when the player reaches the end (or skips to it).
    /// </summary>
    /// <param name="extraActors">
    /// Scene-authored cast members — NPCs that have no <see cref="CharacteContext"/> and so cannot be
    /// discovered through the party. Supplied by the trigger that owns the conversation.
    /// </param>
    public bool TryPlay(
        DialogueSequenceSO sequence,
        CharacteContext initiator,
        Action onCompleted = null,
        IReadOnlyList<DialogueStageActorSource> extraActors = null)
    {
        if (IsPlaying)
        {
            Log("A dialogue is already playing; request rejected.");
            return false;
        }

        if (sequence == null)
            return false;

        if (!sequence.IsPlayable(out string sequenceError))
        {
            Debug.LogWarning($"[Dialogue] Sequence is not playable:\n{sequenceError}", sequence);
            return false;
        }

        if (!DialogueSafeStateGate.CanStart(initiator, out string gateReason))
        {
            Log($"Rejected: {gateReason}");
            return false;
        }

        if (stage == null || ui == null)
        {
            Debug.LogWarning("[Dialogue] Director is missing its stage or UI reference.", this);
            return false;
        }

        // Exclusive ownership of the cinematic stage. Busy means reject, never queue.
        if (!CutsceneDirector.Instance.TryBegin(this))
        {
            Log("Cinematic stage is busy; request rejected.");
            return false;
        }

        activeSequence = sequence;
        activeInitiator = initiator;
        pendingExtraActors = extraActors;

        // Everything from here to `IsPlaying = true` is the start transaction: it may still fail, and
        // nothing it touches is world state. Casting and input are resolved here rather than in the
        // coroutine precisely so a failure can hand the game back untouched — once the coroutine has
        // paused the world, taken control tokens and hidden the HUD, "refuse to start" is no longer
        // an option, only "abort", which the player sees as a visible flicker.
        ResolveCastSources();

        if (!TryResolveRequiredCast(out string castError))
        {
            Debug.LogWarning($"[Dialogue] {castError}", sequence);
            AbandonStart();
            return false;
        }

        var pendingInput = new DialogueInputController(sequence.HoldToSkipSeconds, sequence.AllowHoldToSkip);
        if (!pendingInput.TryBind(initiator))
        {
            AbandonStart();
            return false;
        }

        input = pendingInput;
        completedCallback = onCompleted;
        completionInvoked = false;
        abortRequested = false;
        IsPlaying = true;

        playRoutine = StartCoroutine(PlayRoutine());
        return true;
    }

    /// <summary>
    /// Every opening cast entry marked non-optional must have resolved to a real visual.
    ///
    /// An optional entry that cannot resolve simply leaves its slot empty and the remaining portraits
    /// re-centre, which is what a conversation written for "whoever is deployed" wants. A required
    /// entry that cannot resolve means the conversation would play without a character it is written
    /// around, so the start is refused instead.
    ///
    /// Only the opening cast is covered. A `stageChanges` entry that cannot resolve mid-conversation
    /// keeps whoever is already standing there and warns, because by then refusing is not available.
    /// </summary>
    bool TryResolveRequiredCast(out string error)
    {
        error = null;

        IReadOnlyList<DialogueCastEntry> cast = activeSequence != null ? activeSequence.Cast : null;
        for (int i = 0; cast != null && i < cast.Count; i++)
        {
            DialogueCastEntry entry = cast[i];
            if (entry == null || !entry.IsValid || entry.optional)
                continue;

            if (castSources.ContainsKey(entry.characterId))
                continue;

            error =
                $"'{activeSequence.name}' requires cast member '{entry.characterId}' for slot " +
                $"{entry.slot}, but nothing in the live party or the trigger's scene actors resolves " +
                "it. Mark the entry optional if the conversation can play without them.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Unwinds a start that was refused after cinematic ownership was taken. Mirrors exactly what
    /// <see cref="TryPlay"/> had set, and nothing else — no pause token, control token, HUD state or
    /// stage exists yet at any point where this is reachable.
    /// </summary>
    void AbandonStart()
    {
        castSources.Clear();
        frozenContexts.Clear();
        activeSequence = null;
        activeInitiator = null;
        pendingExtraActors = null;
        input = null;

        CutsceneDirector.Instance.End(this);
    }

    /// <summary>
    /// Ends the conversation immediately and restores the world without reporting completion. Used by
    /// death, scene change, and any other forced interruption.
    /// </summary>
    public void Abort(string reason)
    {
        if (!IsPlaying)
            return;

        Log($"Aborted: {reason}");
        abortRequested = true;

        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }

        Cleanup(reportCompletion: false);
    }

    IEnumerator PlayRoutine()
    {
        // Casting and input were resolved and committed by TryPlay; this routine only owns world
        // state, and every line below it is past the point of no return.
        pauseScope.Apply(frozenContexts);
        WatchInitiatorLife();

        stage.BeginSession(activeSequence, castSources, ResolveProfile);
        ui.Open(activeSequence.AllowHoldToSkip && input.IsAvailable, input.BindingLabel);

        yield return Fade(0f, 1f);

        IReadOnlyList<DialogueLine> lines = activeSequence.Lines;
        for (int i = 0; i < lines.Count && !abortRequested; i++)
        {
            DialogueLine line = lines[i];
            if (line == null)
                continue;

            // Stage changes land before the line is shown, so a character brought on for this line is
            // already standing there when it is spoken.
            if (line.HasStageChanges)
                yield return PlayStageChanges(line);

            stage.SetSpeaker(line.speakerCharacterId, line.poseId);
            ui.SetSpeaker(stage.SpeakingSlot);
            ui.ShowLine(ResolveSpeakerName(line), line, activeSequence.ResolveTypewriterSpeed(line));

            bool advanced = false;
            while (!advanced && !abortRequested)
            {
                float dt = Time.unscaledDeltaTime;
                input.Tick(dt);
                stage.Tick(dt);
                ui.Tick(dt);
                ui.SetSkipProgress(input.SkipProgress01);

                if (input.SkipRequested)
                {
                    // Hold-to-skip ends the whole sequence, and still counts as reaching the end.
                    i = lines.Count;
                    advanced = true;
                    break;
                }

                if (input.AdvancePressed)
                {
                    // First press finishes the reveal, second press moves on.
                    if (ui.IsRevealing)
                        ui.CompleteReveal();
                    else
                        advanced = true;
                }

                yield return null;
            }
        }

        if (abortRequested)
            yield break;

        yield return Fade(1f, 0f);

        playRoutine = null;
        Cleanup(reportCompletion: true);
    }

    /// <summary>
    /// Runs a line's stage changes as a visible hand-over: everyone leaving slides off together, the
    /// clones are swapped, then everyone arriving slides on together.
    ///
    /// It has to be sequential rather than a cross-fade because a slot has one camera and one
    /// RenderTexture — the outgoing and incoming actors cannot both be rendered into it at once.
    /// Slots the line does not actually change are left alone, so a line that restates the current
    /// line-up costs nothing and does not blink.
    /// </summary>
    IEnumerator PlayStageChanges(DialogueLine line)
    {
        changingSlots.Clear();
        for (int i = 0; i < line.stageChanges.Count; i++)
        {
            DialogueStageChange change = line.stageChanges[i];
            if (stage.WillChangeSlot(change) && !changingSlots.Contains(change.slot))
                changingSlots.Add(change.slot);
        }

        if (changingSlots.Count == 0)
        {
            // Still apply them: a no-op change must not leave the stage stale if it clears a slot the
            // stage already emptied for another reason.
            for (int i = 0; i < line.stageChanges.Count; i++)
                stage.ApplyStageChange(line.stageChanges[i]);

            ui.LayoutOccupiedPortraits();
            yield break;
        }

        yield return TweenChangingPortraits(1f, 0f, ui.ExitSeconds);

        for (int i = 0; i < line.stageChanges.Count; i++)
            stage.ApplyStageChange(line.stageChanges[i]);

        ui.LayoutOccupiedPortraits();

        // A slot that was cleared rather than refilled stays parked off stage.
        for (int i = changingSlots.Count - 1; i >= 0; i--)
        {
            if (!stage.IsSlotOccupied(changingSlots[i]))
                changingSlots.RemoveAt(i);
        }

        if (changingSlots.Count > 0)
            yield return TweenChangingPortraits(0f, 1f, ui.EnterSeconds);
    }

    IEnumerator TweenChangingPortraits(float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            for (int i = 0; i < changingSlots.Count; i++)
                ui.SetPortraitOnStage(changingSlots[i], to);

            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration && !abortRequested)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(from, to, Mathf.Clamp01(elapsed / duration));
            for (int i = 0; i < changingSlots.Count; i++)
                ui.SetPortraitOnStage(changingSlots[i], t);

            yield return null;
        }

        for (int i = 0; i < changingSlots.Count; i++)
            ui.SetPortraitOnStage(changingSlots[i], to);
    }

    IEnumerator Fade(float from, float to)
    {
        float duration = Mathf.Max(0.01f, fadeSeconds);
        float elapsed = 0f;

        ui.SetAlpha(from);
        while (elapsed < duration && !abortRequested)
        {
            elapsed += Time.unscaledDeltaTime;
            ui.SetAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration)));
            yield return null;
        }

        ui.SetAlpha(to);
    }

    /// <summary>
    /// Tears the session down. The world, input, and HUD are handed back here and nowhere else, so
    /// both the normal end and every abort path go through exactly the same restore.
    /// </summary>
    void Cleanup(bool reportCompletion)
    {
        UnwatchInitiatorLife();

        if (stage != null)
            stage.EndSession();

        if (ui != null)
            ui.Close();

        if (input != null)
        {
            input.Dispose();
            input = null;
        }

        pauseScope.Restore();

        castSources.Clear();
        frozenContexts.Clear();
        pendingExtraActors = null;

        DialogueSequenceSO finished = activeSequence;
        Action callback = completedCallback;

        activeSequence = null;
        activeInitiator = null;
        completedCallback = null;
        IsPlaying = false;
        playRoutine = null;

        CutsceneDirector.Instance.End(this);

        if (!reportCompletion || completionInvoked)
            return;

        completionInvoked = true;
        Log($"Completed '{(finished != null ? finished.DialogueId : "?")}'.");

        try
        {
            callback?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Dialogue] OnCompleted handler threw: {ex}", this);
        }
    }

    /// <summary>
    /// Maps every live party character by <see cref="CharacterStats.characterId"/> so the stage can
    /// clone the real, currently equipped actors rather than a prefab.
    /// </summary>
    void ResolveCastSources()
    {
        castSources.Clear();
        frozenContexts.Clear();

        var spawnPoint = FindFirstObjectByType<PartySpawnPoint>(FindObjectsInactive.Include);
        PartyRuntime party = spawnPoint != null ? spawnPoint.CurrentParty : null;

        if (party != null)
        {
            for (int i = 0; i < party.Actors.Count; i++)
            {
                PartyRuntimeActor actor = party.Actors[i];
                CharacteContext ctx = actor?.Context;
                if (ctx == null)
                    continue;

                // Registered twice: once under the character's own id, and once under its party role.
                // Casting by role is what lets a sequence survive the player changing their team,
                // which they can do at any time in the Basement.
                RegisterCastSource(ctx, actor.Role);
            }
        }

        // The initiator may not be part of the party runtime (test scenes, standalone setups), and it
        // always has to be frozen even when it is not on stage.
        if (activeInitiator != null)
            RegisterCastSource(activeInitiator, ChainActorRole.None);

        // Scene-authored actors last, so an NPC deliberately cast under a party member's id wins.
        for (int i = 0; pendingExtraActors != null && i < pendingExtraActors.Count; i++)
        {
            DialogueStageActorSource extra = pendingExtraActors[i];
            if (extra == null || !extra.IsValid)
                continue;

            DialogueCastSource source = extra.BuildSource();
            if (source.IsValid)
                castSources[extra.CharacterId] = source;
        }
    }

    void RegisterCastSource(CharacteContext ctx, ChainActorRole role)
    {
        if (ctx == null)
            return;

        // The freeze list is per context; the key registration below may still need to run for a
        // context already frozen (the initiator is usually also a party member).
        if (!frozenContexts.Contains(ctx))
            frozenContexts.Add(ctx);

        ctx.ResolveReferences();

        DialogueCastSource source = DialogueCastSource.FromCharacter(ctx);
        if (!source.IsValid)
            return;

        if (!string.IsNullOrWhiteSpace(source.CharacterId))
            castSources[source.CharacterId] = source;

        if (role != ChainActorRole.None)
            castSources[DialogueCastKeys.ForRole(role)] = source;
    }

    CharacterDialogueAnimationProfileSO ResolveProfile(string characterId)
    {
        return profileDatabase != null ? profileDatabase.Find(characterId) : null;
    }

    string ResolveSpeakerName(DialogueLine line)
    {
        if (line == null)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(line.speakerNameOverride))
            return line.speakerNameOverride;

        if (!line.HasSpeaker)
            return string.Empty;

        if (castSources.TryGetValue(line.speakerCharacterId, out DialogueCastSource source) &&
            source.IsValid && !string.IsNullOrWhiteSpace(source.DisplayName))
        {
            return source.DisplayName;
        }

        return line.speakerCharacterId;
    }

    void WatchInitiatorLife()
    {
        UnwatchInitiatorLife();

        watchedLifeHub = activeInitiator != null ? activeInitiator.stateHub : null;
        if (watchedLifeHub != null)
            watchedLifeHub.Died += OnInitiatorDied;
    }

    void UnwatchInitiatorLife()
    {
        if (watchedLifeHub != null)
            watchedLifeHub.Died -= OnInitiatorDied;

        watchedLifeHub = null;
    }

    void OnInitiatorDied()
    {
        Abort("Initiating character died");
    }

    void Log(string message)
    {
        if (logLifecycle)
            Debug.Log($"[Dialogue] {message}", this);
    }

    /// <summary>Every authoring problem that would stop the director from presenting a conversation.</summary>
    public void CollectValidationIssues(List<string> issues)
    {
        if (issues == null)
            throw new ArgumentNullException(nameof(issues));

        if (stage == null)
            issues.Add("DialogueStage is not assigned on the director.");
        else
            stage.CollectValidationIssues(issues);

        if (ui == null)
            issues.Add("DialogueUI is not assigned on the director.");
        else
            ui.CollectValidationIssues(issues);

        if (profileDatabase == null)
            issues.Add("DialogueProfileDatabaseSO is not assigned; no actor will be posed.");
    }
}
