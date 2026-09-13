using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Opt-in input owned by the separate HUD prefab. Never edits the shared action asset.</summary>
public sealed class PartyHudInputRouter : MonoBehaviour
{
    [SerializeField] PartyHudPresenter presenter;
    readonly PartyHudPressState[] presses = { new(), new(), new(), new() };
    readonly List<(InputAction action, int index, InputBinding binding)> suspended = new();
    PlayerContext player;
    InputActionMap gameplayMap;

    void Update()
    {
        if (presenter == null) return;
        if (player != presenter.Player) Bind(presenter.Player);
        bool allowed = player != null && Application.isFocused && Time.timeScale > 0f &&
            !CutsceneDirector.IsCinematicPlaying &&
            (player.stateHub?.UISM == null || player.stateHub.UISM.CurrentId == UIStateId.Normal) &&
            (gameplayMap == null || gameplayMap.enabled);
        if (!allowed) { CancelPresses(); return; }
        var keyboard = Keyboard.current;
        if (keyboard == null) return;
        double now = Time.unscaledTimeAsDouble;
        for (int i = 0; i < 4; i++)
        {
            var key = keyboard[(Key)((int)Key.Digit1 + i)];
            if (key.wasPressedThisFrame) presses[i].Begin(now);
            if (presses[i].Hold(now)) presenter.Cast(i, 1);
            if (key.wasReleasedThisFrame)
            {
                int skill = presses[i].Release(now);
                if (skill >= 0) presenter.Cast(i, skill);
            }
        }
        if (keyboard.eKey.wasPressedThisFrame)
        {
            // Even a rejected/stale Combo attempt consumes this press, never also melee.
            if (!presenter.TryUseFirstCombo())
            {
                ThirdPersonTargetingUtility.FacePlayerTowardSoftTarget(player, actionRange: 4f);
                player.stateHub?.RequestOnMelee();
            }
        }
    }

    void Bind(PlayerContext source)
    {
        Restore();
        player = source;
        if (player == null) return;
        var input = player.GetComponentInChildren<PlayerInput>(true);
        if (input == null || input.actions == null) return;
        foreach (string name in new[] { "SkillSlot1", "SkillSlot2", "SkillSlot3", "PartyComboSlot1", "PartyComboSlot2", "PartyComboHelper", "Melee" })
        {
            var action = input.actions.FindAction(name, false);
            if (action == null) continue;
            gameplayMap = action.actionMap;
            for (int i = 0; i < action.bindings.Count; i++)
            {
                var binding = action.bindings[i];
                if (binding.effectivePath == null || !binding.effectivePath.StartsWith("<Keyboard>")) continue;
                suspended.Add((action, i, binding));
                action.ApplyBindingOverride(i, new InputBinding { overridePath = "" });
            }
        }
    }
    void CancelPresses() { foreach (var press in presses) press.Cancel(); }
    void Restore()
    {
        CancelPresses();
        foreach (var item in suspended)
        {
            item.action.RemoveBindingOverride(item.index);
            item.action.ApplyBindingOverride(item.index, item.binding);
        }
        suspended.Clear(); gameplayMap = null; player = null;
    }
    void OnDisable() => Restore();
}
