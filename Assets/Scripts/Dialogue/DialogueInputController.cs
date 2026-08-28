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

    /// <summary>
    /// Takes over input for the conversation, or reports failure having changed nothing.
    ///
    /// Everything is resolved — the <see cref="PlayerInput"/>, the source action, and at least one
    /// usable binding — **before** gameplay input is deactivated. The old order deactivated first and
    /// tolerated a null action afterwards, which left the player frozen in a conversation that could
    /// never be advanced because nothing was listening for the key. Failing here is recoverable;
    /// failing after `DeactivateInput` is not.
    ///
    /// There is deliberately no hard-coded fallback key. One would paper over a broken Input Actions
    /// asset and ignore whatever the player rebound the interact key to.
    /// </summary>
    public bool TryBind(CharacteContext playerContext)
    {
        if (playerContext == null)
            return false;

        PlayerInput resolvedInput = playerContext.GetComponentInChildren<PlayerInput>(true);
        if (resolvedInput == null)
        {
            Debug.LogWarning(
                $"[Dialogue] '{playerContext.name}' has no PlayerInput, so the conversation would " +
                "have no way to advance. Start refused.", playerContext);
            return false;
        }

        InputAction source = resolvedInput.actions != null
            ? resolvedInput.actions.FindAction(InteractActionName, false)
            : null;

        if (source == null)
        {
            Debug.LogWarning(
                $"[Dialogue] Input action '{InteractActionName}' was not found on " +
                $"'{playerContext.name}'. Start refused.", playerContext);
            return false;
        }

        InputAction standalone = BuildStandaloneAction(source);
        if (standalone == null)
        {
            Debug.LogWarning(
                $"[Dialogue] Input action '{InteractActionName}' has no usable non-composite " +
                "binding, so nothing could advance the conversation. Start refused.", playerContext);
            return false;
        }

        // Past this point the takeover is committed, and every step below cannot fail.
        playerInput = resolvedInput;
        dialogueAction = standalone;
        BindingLabel = ResolveBindingLabel(source);

        playerInputWasActive = playerInput.inputIsActive;
        playerInputCaptured = true;
        playerInput.DeactivateInput();

        // The interact press that opened the conversation must not also advance its first line.
        armed = !dialogueAction.IsPressed();
        wasPressed = dialogueAction.IsPressed();
        return true;
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

    /// <summary>
    /// Mirrors the interact action's effective bindings onto a standalone action, or returns null if
    /// nothing usable came of it.
    ///
    /// "Usable" means the action actually resolves to a control on a present device, not merely that
    /// a binding string exists. Counting binding strings was not enough: a path that resolves to
    /// nothing — a rebind to a key that does not exist, or a device that is not connected — passes a
    /// string check and then silently never fires, which is the exact hang this class exists to
    /// prevent. Controls only resolve once the action is enabled, so it is enabled here and handed
    /// over already running.
    /// </summary>
    static InputAction BuildStandaloneAction(InputAction source)
    {
        var action = new InputAction("DialogueAdvance", InputActionType.Button);

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
        }

        action.Enable();
        if (action.controls.Count > 0)
            return action;

        action.Disable();
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
