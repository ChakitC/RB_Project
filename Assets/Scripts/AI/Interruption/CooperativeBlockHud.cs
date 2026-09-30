using TMPro;
using UnityEngine;

// Can be bound to authored uGUI fields, or creates a small default overlay on first use.
public sealed class CooperativeBlockHud : MonoBehaviour
{
    public UnityEngine.UI.Image meterFill;
    public TMP_Text prompt, timeLabel, helperLabel;
    public CanvasGroup group;
    [Header("Authored Spacebar HUD")]
    public bool struggleOnly;
    public SlantedBarFillEffect slantedMeterFill;
    public Vector2 meterAxis = new Vector2(1f, -.75f);
    public float meterStart = -.31f, meterEnd = .30f;
    public GameObject[] restingArtwork, pressedArtwork;
    [Min(.1f), Tooltip("Seconds between artwork swaps while waiting for Block presses.")]
    public float blinkInterval = .3f;
    InterruptionCommandController command;
    CooperativeBlockSession session;
    float feedbackRemaining, resultRemaining, blinkElapsed;

    public static CooperativeBlockHud Ensure(PlayerContext player, InterruptionCommandController command)
    {
        var existing = player.playerUIContext != null
            ? player.playerUIContext.GetComponentInChildren<CooperativeBlockHud>(true) : null;
        if (existing == null) existing = player.GetComponentInChildren<CooperativeBlockHud>(true);
        if (existing != null) { existing.Bind(command); return existing; }
        var root = new GameObject("Cooperative Block HUD", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(CanvasGroup));
        root.transform.SetParent(player.transform, false);
        var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 120;
        var scaler = root.GetComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);
        var hud = root.AddComponent<CooperativeBlockHud>(); hud.group = root.GetComponent<CanvasGroup>();
        hud.group.blocksRaycasts = false; hud.group.interactable = false;
        var panel = Rect("Guard meter", root.transform, new Vector2(620, 170), new Vector2(0, -295));
        panel.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(.025f, .04f, .075f, .92f);
        var track = Rect("Track", panel, new Vector2(552, 22), new Vector2(0, -10));
        track.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(.12f, .18f, .23f);
        var fill = Rect("Fill", track, new Vector2(552, 22), Vector2.zero);
        fill.pivot = new Vector2(0, .5f); fill.anchoredPosition = new Vector2(-276, 0);
        hud.meterFill = fill.gameObject.AddComponent<UnityEngine.UI.Image>(); hud.meterFill.color = new Color(.2f, .85f, 1f);
        hud.prompt = Label("Prompt", panel, new Vector2(0, 48), 28);
        hud.helperLabel = Label("Partner", panel, new Vector2(0, 16), 20);
        hud.timeLabel = Label("Time", panel, new Vector2(0, -52), 22);
        hud.Bind(command); return hud;
    }

    static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.sizeDelta = size; rect.anchoredPosition = position; return rect;
    }
    static TMP_Text Label(string name, Transform parent, Vector2 position, float fontSize)
    {
        var text = Rect(name, parent, new Vector2(570, 34), position).gameObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize; text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false; return text;
    }
    public void Bind(InterruptionCommandController owner)
    {
        if (slantedMeterFill != null) slantedMeterFill.Configure(meterAxis, meterStart, meterEnd);
        if (command == owner) return;
        Unbind(); command = owner;
        Refresh();
        if (command == null) return;
        command.CooperativeBlockStarted += Show;
        if (command.ActiveCooperativeBlock != null) Show(command.ActiveCooperativeBlock);
    }
    void Show(CooperativeBlockSession next)
    {
        if (session == next) return;
        DetachSession(); session = next; resultRemaining = feedbackRemaining = blinkElapsed = 0f;
        session.PressAccepted += OnPress; session.Ended += OnEnd;
        Refresh();
    }
    void OnPress(CooperativeBlockSession _) { feedbackRemaining = .12f; blinkElapsed = 0f; Refresh(); }
    void OnEnd(CooperativeBlockSession _, CooperativeBlockState result) { resultRemaining = .8f; Refresh(); }
    void Update() => Tick(GlobalTimeScaleManager.Instance.IsPaused ? 0f : Time.unscaledDeltaTime);
    void Tick(float deltaTime)
    {
        feedbackRemaining = Mathf.Max(0, feedbackRemaining - deltaTime);
        resultRemaining = Mathf.Max(0, resultRemaining - deltaTime);
        blinkElapsed = session != null && session.State == CooperativeBlockState.Struggling
            ? Mathf.Repeat(blinkElapsed + deltaTime, Mathf.Max(.1f, blinkInterval) * 2f) : 0f;
        Refresh();
    }
    void Refresh()
    {
        if (group == null) return;
        group.alpha = session != null && (struggleOnly
            ? session.State == CooperativeBlockState.Struggling
            : session.IsActive || resultRemaining > 0f) ? 1f : 0f;
        bool animated = session != null && session.State == CooperativeBlockState.Struggling &&
            (feedbackRemaining > 0f || blinkElapsed >= Mathf.Max(.1f, blinkInterval));
        SetArtwork(restingArtwork, !animated);
        SetArtwork(pressedArtwork, animated);
        if (session == null) return;
        if (meterFill != null)
        {
            if (slantedMeterFill != null)
                slantedMeterFill.SetFillAmount(session.Meter);
            else
            {
                meterFill.rectTransform.localScale = new Vector3(session.Meter, 1, 1);
                meterFill.color = feedbackRemaining > 0 ? Color.white : new Color(.2f, .85f, 1f);
            }
        }
        if (prompt != null) prompt.text = session.Outcome.HasValue ? session.Outcome.ToString().ToUpperInvariant() :
            session.State == CooperativeBlockState.Preparing ? "GET READY" : "PRESS [" + command.BlockBindingLabel + "] REPEATEDLY";
        if (helperLabel != null) helperLabel.text = session.Ally != null ? "PARTNER  " +
            (!string.IsNullOrEmpty(session.Ally.baseStats?.characterName) ? session.Ally.baseStats.characterName : session.Ally.name.Replace("(Clone)", "")) : "";
        if (timeLabel != null) timeLabel.text = $"{session.Remaining:0.0}s    {session.Meter:P0}";
    }
    static void SetArtwork(GameObject[] artwork, bool active)
    {
        if (artwork == null) return;
        foreach (var item in artwork)
            if (item != null && item.activeSelf != active) item.SetActive(active);
    }
    void DetachSession()
    {
        if (session != null) { session.PressAccepted -= OnPress; session.Ended -= OnEnd; }
        session = null;
    }
    void Unbind()
    {
        if (command != null) command.CooperativeBlockStarted -= Show;
        DetachSession(); command = null;
        resultRemaining = feedbackRemaining = blinkElapsed = 0f;
        SetArtwork(restingArtwork, true);
        SetArtwork(pressedArtwork, false);
        if (group != null) group.alpha = 0f;
    }
    void OnDestroy() => Unbind();
}
