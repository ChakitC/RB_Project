using System;
using System.Text;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class StateHubDebugView : MonoBehaviour
{
    [SerializeField] bool debugInInspector = true;
    [SerializeField] bool logTransitions = true;
    [SerializeField] bool showOnScreen;
    [SerializeField] Vector2 screenPos = new(10, 10);
    [SerializeField, TextArea(4, 20)] string snapshot;
    [SerializeField, HideInInspector] string legacyDebugSignature;
    StateHub hub;
    bool subscribed;

    public void Configure(bool inspector, bool log, bool screen, Vector2 position)
    {
        debugInInspector = inspector; logTransitions = log; showOnScreen = screen; screenPos = position;
    }
    void OnEnable() { hub = GetComponent<StateHub>(); Subscribe(); }
    void Subscribe()
    {
        if (subscribed || hub == null || !hub.IsInitialized) return;
        hub.MoveSM.OnChanged += MoveChanged;
        hub.WeaponSM.OnChanged += WeaponChanged;
        hub.LifeSM.OnChanged += LifeChanged;
        hub.UISM.OnChanged += UiChanged;
        subscribed = true;
    }
    void OnDisable()
    {
        if (!subscribed || hub == null) return;
        hub.MoveSM.OnChanged -= MoveChanged;
        hub.WeaponSM.OnChanged -= WeaponChanged;
        hub.LifeSM.OnChanged -= LifeChanged;
        hub.UISM.OnChanged -= UiChanged;
        subscribed = false;
    }
    void MoveChanged(MoveStateId from, MoveStateId to) => Log("Move", from, to);
    void WeaponChanged(WeaponStateId from, WeaponStateId to) => Log("Weapon", from, to);
    void LifeChanged(LifeStateId from, LifeStateId to) => Log("Life", from, to);
    void UiChanged(UIStateId from, UIStateId to) => Log("UI", from, to);
    void Log<T>(string category, T from, T to)
    {
        if (logTransitions) Debug.Log($"[StateHub] {category}: {from} -> {to}", this);
    }
    void Update()
    {
        Subscribe();
        if ((!debugInInspector && !showOnScreen) || hub == null || !hub.IsInitialized) return;
        var text = new StringBuilder();
        text.AppendLine($"Life: {hub.LifeSM.CurrentId}  UI: {hub.UISM.CurrentId}");
        text.AppendLine($"Move: {hub.MoveSM.CurrentId}  Weapon: {hub.WeaponSM.CurrentId}");
        foreach (CharacterAction action in Enum.GetValues(typeof(CharacterAction)))
        {
            var result = hub.GetAdmission(action);
            if (!result.Allowed) text.AppendLine($"{action}: {result.Reason}");
        }
        text.Append(hub.DescribeControlHolders()).Append(hub.DescribeUiHolders());
        snapshot = text.ToString();
    }
    void OnGUI()
    {
        if (showOnScreen) GUI.Label(new Rect(screenPos.x, screenPos.y, 600, 420), snapshot);
    }
}
