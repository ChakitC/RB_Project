using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Owns the temporary hold-to-skip action for the stage intro.
/// Gameplay input is deactivated for the duration, and a standalone action mirroring the
/// effective bindings of <c>Player/Interract</c> is used purely to read the hold.
/// A press that was already held when the intro started is ignored until it is released once.
/// </summary>
internal sealed class StageIntroSkipInput
{
    const string InteractActionName = "Interract";

    readonly float holdSeconds;

    PlayerInput playerInput;
    bool playerInputWasActive;
    bool playerInputCaptured;

    InputAction skipAction;
    bool armed;

    public StageIntroSkipInput(float holdSeconds)
    {
        this.holdSeconds = Mathf.Max(0.01f, holdSeconds);
    }

    public string BindingLabel { get; private set; } = "F";
    public float Progress01 { get; private set; }
    public bool IsAvailable => skipAction != null;
    public bool Completed { get; private set; }

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
            skipAction = BuildStandaloneAction(source);
        }

        playerInputWasActive = playerInput.inputIsActive;
        playerInputCaptured = true;
        playerInput.DeactivateInput();

        skipAction?.Enable();

        // Never consume a button that was already down when the intro started.
        armed = skipAction == null || !skipAction.IsPressed();
    }

    public void Tick(float unscaledDeltaTime)
    {
        if (skipAction == null || Completed)
            return;

        bool pressed = skipAction.IsPressed();

        if (!armed)
        {
            if (!pressed)
                armed = true;

            Progress01 = 0f;
            return;
        }

        if (!pressed)
        {
            Progress01 = 0f;
            return;
        }

        Progress01 = Mathf.Clamp01(Progress01 + unscaledDeltaTime / holdSeconds);
        if (Progress01 >= 1f)
            Completed = true;
    }

    public void Dispose()
    {
        if (skipAction != null)
        {
            skipAction.Disable();
            skipAction.Dispose();
            skipAction = null;
        }

        if (playerInputCaptured && playerInput != null && playerInputWasActive)
            playerInput.ActivateInput();

        playerInputCaptured = false;
        playerInput = null;
        Progress01 = 0f;
        armed = false;
    }

    static InputAction BuildStandaloneAction(InputAction source)
    {
        var action = new InputAction("StageIntroSkip", InputActionType.Button);
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
    /// Reads the key name from the binding path rather than the resolved control's display name.
    /// <c>GetBindingDisplayString</c> localises to the active OS keyboard layout — on a Thai layout
    /// <c>&lt;Keyboard&gt;/f</c> comes back as "ด", which the prompt font cannot render and which does
    /// not match the physical key the player is told to hold.
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
