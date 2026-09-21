using UnityEngine;
using UnityEngine.InputSystem;

public sealed partial class DefensiveBlockTestHarness
{
    public bool TestControlsOpen { get; private set; } = true;
    PlayerInput suspendedInput;
    bool inputWasActive, ownsCursor;
    CursorLockMode previousCursorLock;
    bool previousCursorVisible;

    public void SetTestControlsOpen(bool open)
    {
        TestControlsOpen = open;
        if (!open) ClosePicker();
        if (!Application.isPlaying) return;
        SyncTestControlsInput();
        UpdateTestCursor();
    }

    void HandleTestControlsInput()
    {
        if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
            SetTestControlsOpen(!TestControlsOpen);
        SyncTestControlsInput();
    }

    void SyncTestControlsInput()
    {
        // This scene-local fixture suspends only the Player's action map. The camera,
        // Block eligibility, simulation and the harness's own buttons keep working.
        var input = Player != null ? Player.GetComponentInChildren<PlayerInput>(true) : null;
        if (!TestControlsOpen || suspendedInput != input) ReleaseTestInput();
        if (!TestControlsOpen || input == null || suspendedInput == input) return;
        suspendedInput = input;
        inputWasActive = input.inputIsActive;
        if (inputWasActive) input.DeactivateInput();
        Player.moveInput = Vector2.zero;
        Player.lookInput = Vector2.zero;
        Player.stateHub?.RequestCanceledFire();
        Player.WeaponSystem?.OnAim(false);
    }

    void ReleaseTestInput()
    {
        var input = suspendedInput;
        suspendedInput = null;
        if (input != null && inputWasActive) input.ActivateInput();
        inputWasActive = false;
    }

    void LateUpdate()
    {
        SyncTestControlsInput();
        UpdateTestCursor();
    }

    void UpdateTestCursor()
    {
        if (!TestControlsOpen) { ReleaseTestCursor(); return; }
        if (!ownsCursor)
        {
            previousCursorLock = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            ownsCursor = true;
        }
        // Run after the gameplay camera so it cannot recapture the test menu pointer.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void ReleaseTestCursor()
    {
        if (!ownsCursor) return;
        ownsCursor = false;
        var camera = GameplayCameraController.Instance;
        Cursor.lockState = camera != null
            ? camera.GameplayInputEnabled ? CursorLockMode.Locked : CursorLockMode.None
            : previousCursorLock;
        Cursor.visible = camera != null ? !camera.GameplayInputEnabled : previousCursorVisible;
    }

    void ReleaseTestControls()
    {
        ReleaseTestInput();
        ReleaseTestCursor();
    }
}
