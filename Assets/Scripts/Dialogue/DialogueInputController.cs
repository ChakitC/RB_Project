using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Owns dialogue input for the length of one conversation. Gameplay input is deactivated and a
/// standalone action mirroring the effective bindings of <c>Player/Interract</c> is used to read
/// advance and hold-to-skip, so the dialogue never fights the gameplay action map.
///
/// A tap advances immediately; keeping that same press held past the skip threshold skips the rest
/// of the sequence. A button already down when the dialogue opened is ignored until released once,
/// so the press that started the conversation cannot also advance its first line.
/// </summary>
internal sealed class DialogueInputController
{
    const string InteractActionName = "Interract";

    readonly float holdSeconds;
    readonly bool allowHoldToSkip;

    PlayerInput playerInput;
    bool playerInputWasActive;
    bool playerInputCaptured;

    InputAction dialogueAction;
    bool armed;
    bool wasPressed;
    float holdTime;
    bool holdConsumed;

    public DialogueInputController(float holdSeconds, bool allowHoldToSkip)
    {
        this.holdSeconds = Mathf.Max(0.05f, holdSeconds);
        this.allowHoldToSkip = allowHoldToSkip;
    }

    public string BindingLabel { get; private set; } = "F";
    public bool IsAvailable => dialogueAction != null;

    /// <summary>True for the single tick a press started on. Consumed by reading it.</summary>
    public bool AdvancePressed { get; private set; }

    /// <summary>True once the current press has been held past the skip threshold.</summary>
    public bool SkipRequested { get; private set; }

    /// <summary>0..1 progress of the current hold, for the skip prompt's fill.</summary>
    public float SkipProgress01 { get; private set; }

    public void Bind(CharacteContext playerContext)
    {
        if (playerContext == null)
            return;

        playerInput = playerContext.GetComponentInChildren<PlayerInput>(true);
        if (playerInput == null)
            return;

        InputAction source = playerInput.actions != null
            ? playerInput.actions.FindAction(InteractActionName, false)
            : null;

        if (source != null)
        {
            BindingLabel = ResolveBindingLabel(source);
            dialogueAction = BuildStandaloneAction(source);
        }

        playerInputWasActive = playerInput.inputIsActive;
        playerInputCaptured = true;
        playerInput.DeactivateInput();

        dialogueAction?.Enable();

        // The interact press that opened the conversation must not also advance its first line.
        armed = dialogueAction == null || !dialogueAction.IsPressed();
        wasPressed = dialogueAction != null && dialogueAction.IsPressed();
    }

    public void Tick(float unscaledDeltaTime)
    {
        AdvancePressed = false;

        if (dialogueAction == null)
            return;

        bool pressed = dialogueAction.IsPressed();

        if (!armed)
        {
            if (!pressed)
                armed = true;

            wasPressed = pressed;
            SkipProgress01 = 0f;
            return;
        }

        if (pressed && !wasPressed)
        {
            AdvancePressed = true;
            holdTime = 0f;
            holdConsumed = false;
        }

        if (pressed)
        {
            holdTime += unscaledDeltaTime;
            SkipProgress01 = allowHoldToSkip ? Mathf.Clamp01(holdTime / holdSeconds) : 0f;

            if (allowHoldToSkip && !holdConsumed && holdTime >= holdSeconds)
            {
                holdConsumed = true;
                SkipRequested = true;
            }
        }
        else
        {
            holdTime = 0f;
            SkipProgress01 = 0f;
        }

        wasPressed = pressed;
    }

    public void Dispose()
    {
        if (dialogueAction != null)
        {
            dialogueAction.Disable();
            dialogueAction.Dispose();
            dialogueAction = null;
        }

        if (playerInputCaptured && playerInput != null && playerInputWasActive)
            playerInput.ActivateInput();

        playerInputCaptured = false;
        playerInput = null;
        AdvancePressed = false;
        SkipRequested = false;
        SkipProgress01 = 0f;
        armed = false;
        wasPressed = false;
        holdTime = 0f;
        holdConsumed = false;
    }

    static InputAction BuildStandaloneAction(InputAction source)
    {
        var action = new InputAction("DialogueAdvance", InputActionType.Button);
        int added = 0;

        var bindings = source.bindings;
        for (int i = 0; i < bindings.Count; i++)
        {
            InputBinding binding = bindings[i];
            if (binding.isComposite || binding.isPartOfComposite)
                continue;

            string path = binding.effectivePath;
            if (string.IsNullOrEmpty(path))
                continue;

            action.AddBinding(path, groups: binding.groups, processors: binding.processors);
            added++;
        }

        if (added != 0)
            return action;

        action.Dispose();
        return null;
    }

    /// <summary>
    /// Reads the key name from the binding path rather than the resolved control's display name, for
    /// the same reason <c>StageIntroSkipInput</c> does: <c>GetBindingDisplayString</c> localises to
    /// the active OS keyboard layout, which neither matches the physical key nor renders in the
    /// prompt font.
    /// </summary>
    static string ResolveBindingLabel(InputAction source)
    {
        var bindings = source.bindings;
        for (int i = 0; i < bindings.Count; i++)
        {
            InputBinding binding = bindings[i];
            if (binding.isComposite || binding.isPartOfComposite)
                continue;

            string path = binding.effectivePath;
            if (string.IsNullOrEmpty(path))
                continue;

            string label = InputControlPath.ToHumanReadableString(
                path, InputControlPath.HumanReadableStringOptions.OmitDevice);

            if (!string.IsNullOrWhiteSpace(label))
                return label.ToUpperInvariant();
        }

        return "F";
    }
}
