using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// The scene-side half of a conversation. Plugs into the existing interact system, decides whether
/// this dialogue may play again, and owns the scene consequences (quest flags, doors, spawns) that
/// must not live on the reusable <see cref="DialogueSequenceSO"/>.
///
/// <see cref="onCompleted"/> only fires when the player actually reaches the end of the sequence.
/// A death, scene change, or forced interruption ends the conversation without it.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(InteractableLink))]
public sealed class DialogueTrigger : MonoBehaviour, IInteractable
{
    public enum RepeatMode
    {
        /// <summary>Plays once per save slot. Completion is persisted under the sequence's dialogueId.</summary>
        PlayOnce = 0,

        /// <summary>Plays every time the player interacts.</summary>
        Replay = 1,
    }

    [Header("Interaction")]
    [SerializeField] private int priority;
    [SerializeField] private string prompt = "Talk";

    [Header("Dialogue")]
    [SerializeField] private DialogueSequenceSO sequence;
    [SerializeField] private RepeatMode repeatMode = RepeatMode.PlayOnce;

    [Header("Stage cast")]
    [SerializeField, Tooltip("Scene actors this conversation puts on stage — an NPC has no character " +
                             "context, so it cannot be found through the party and has to be listed " +
                             "here. Left empty, any DialogueStageActorSource on this object or its " +
                             "children is used.")]
    private List<DialogueStageActorSource> stageActors = new();

    [Header("On Completed")]
    [SerializeField, Tooltip("Scene consequences of finishing this conversation. Never runs on an abort.")]
    private UnityEvent onCompleted;

    [SerializeField, Tooltip("Hide or disable this object once the conversation has been completed.")]
    private bool deactivateAfterCompleted;

    [SerializeField, Tooltip("Object deactivated after completion. Empty uses this GameObject.")]
    private GameObject objectToDeactivate;

    public int Priority => priority;
    public DialogueSequenceSO Sequence => sequence;
    public RepeatMode Repeat => repeatMode;
    public IReadOnlyList<DialogueStageActorSource> StageActors => stageActors;

    /// <summary>True when a play-once dialogue has already been completed on the current save slot.</summary>
    public bool IsCompleted
    {
        get
        {
            if (repeatMode == RepeatMode.Replay || sequence == null)
                return false;

            string dialogueId = sequence.DialogueId;
            if (string.IsNullOrWhiteSpace(dialogueId))
                return false;

            return SaveManager.Instance != null && SaveManager.Instance.IsDialogueCompleted(dialogueId);
        }
    }

    void Awake()
    {
        // An NPC almost always casts itself, so the common case needs no wiring.
        if (stageActors == null || stageActors.Count == 0)
            stageActors = new List<DialogueStageActorSource>(GetComponentsInChildren<DialogueStageActorSource>(true));
    }

    void Start()
    {
        // A play-once dialogue completed in an earlier session must not offer its prompt again.
        if (deactivateAfterCompleted && IsCompleted)
            Deactivate();
    }

    public string GetPrompt(Interactor interactor)
    {
        return prompt;
    }

    public bool CanInteract(Interactor interactor)
    {
        if (sequence == null || interactor == null || IsCompleted)
            return false;

        DialogueDirector director = DialogueDirector.Instance;
        if (director == null || director.IsPlaying)
            return false;

        // The gate is re-checked at play time; this only keeps the prompt from lighting up while the
        // player is dashing, shooting, reloading, mid-skill, or already in a cinematic.
        return DialogueSafeStateGate.CanStart(interactor.OwnerContext, out _);
    }

    public void Interact(Interactor interactor)
    {
        if (sequence == null || interactor == null || IsCompleted)
            return;

        DialogueDirector director = DialogueDirector.Instance;
        if (director == null)
        {
            Debug.LogWarning(
                "[Dialogue] No DialogueDirector in the scene. Is DialoguePresentation loaded?", this);
            return;
        }

        director.TryPlay(sequence, interactor.OwnerContext, HandleCompleted, stageActors);
    }

    void HandleCompleted()
    {
        if (repeatMode == RepeatMode.PlayOnce &&
            sequence != null &&
            !string.IsNullOrWhiteSpace(sequence.DialogueId))
        {
            SaveManager.Instance?.MarkDialogueCompleted(sequence.DialogueId);
        }

        onCompleted?.Invoke();

        if (deactivateAfterCompleted)
            Deactivate();
    }

    void Deactivate()
    {
        GameObject target = objectToDeactivate != null ? objectToDeactivate : gameObject;
        target.SetActive(false);
    }
}
