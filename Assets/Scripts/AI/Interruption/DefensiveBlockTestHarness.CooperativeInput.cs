using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public sealed partial class DefensiveBlockTestHarness
{
    IEnumerator ValidateCooperativeInput(StringBuilder report)
    {
        ResetTrial(); while (resetting) yield return null;
        yield return new WaitForSecondsRealtime(.7f);
        StartCharge();
        double deadline = Time.realtimeSinceStartupAsDouble + 5d;
        while (!Rector.DefensiveBlockAttack.CanRequestBlock(Player) && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
        var keyboard = InputSystem.AddDevice<Keyboard>("CooperativeTestKeyboard");
        var inputSettings = InputSystem.settings;
        var previousBackground = inputSettings.backgroundBehavior;
#if UNITY_EDITOR
        var previousEditorInput = inputSettings.editorInputBehaviorInPlayMode;
        inputSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
        inputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        var action = new InputAction("Block", InputActionType.Button, "<Keyboard>/f12");
        var handler = Player.GetComponentInChildren<PlayerInputHandler>(true);
        action.performed += handler.OnInterruptionCommand; action.canceled += handler.OnInterruptionCommand;
        action.Enable();
        int presses = 0;
        CooperativeBlockSession session = null;
        bool startIgnored = false, holdIgnored = false, releaseRequired = false;
        try
        {
            yield return null; // Install action monitors in the player input update, not the Editor update.
            InputState.Change(keyboard, new KeyboardState(Key.F12));
            yield return null;
            session = Player.interruptionCommand.ActiveCooperativeBlock;
            if (session != null)
            {
                session.PressAccepted += _ => presses++;
                startIgnored = session.State == CooperativeBlockState.Preparing;
                yield return new WaitForSecondsRealtime(.5f);
                holdIgnored = presses == 0 && session.Meter < .5f;
                InputState.Change(keyboard, new KeyboardState()); yield return null;
                InputState.Change(keyboard, new KeyboardState(Key.F12)); yield return null;
                yield return new WaitForSecondsRealtime(.05f);
                releaseRequired = presses == 1;
                for (int i = 0; i < 12 && session.State == CooperativeBlockState.Struggling; i++)
                {
                    InputState.Change(keyboard, new KeyboardState()); yield return null;
                    InputState.Change(keyboard, new KeyboardState(Key.F12)); yield return null;
                }
                yield return new WaitForSecondsRealtime(1.2f);
                deadline = Time.realtimeSinceStartupAsDouble + 3d;
                while ((session.IsActive || !CameraReturned) && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            }
        }
        finally
        {
            action.Disable(); action.Dispose(); InputSystem.RemoveDevice(keyboard);
            inputSettings.backgroundBehavior = previousBackground;
#if UNITY_EDITOR
            inputSettings.editorInputBehaviorInPlayMode = previousEditorInput;
#endif
        }
        bool passed = startIgnored && holdIgnored && releaseRequired && session?.Outcome == CooperativeBlockState.Success &&
            Player.interruptionCommand.ActiveCooperativeBlock == null && CameraReturned;
        report.AppendLine($"{(passed ? "PASS" : "FAIL")} Input System edges: initiatingIgnored={startIgnored} heldIgnored={holdIgnored} freshPress={releaseRequired} acceptedEdges={presses} result={session?.Outcome} binding={Player.interruptionCommand.BlockBindingLabel} finished={session?.State} cameraReturned={CameraReturned}");
    }
}
