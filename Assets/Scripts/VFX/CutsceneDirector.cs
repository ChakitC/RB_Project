using UnityEngine;

// Arbitrates the single "cinematic stage" so at most one cutscene cinematic plays at a time,
// with an unscaled-time cooldown afterward to prevent back-to-back cinematic whiplash.
[DisallowMultipleComponent]
public sealed class CutsceneDirector : MonoBehaviour
{
    static CutsceneDirector _instance;
    public static CutsceneDirector Instance
    {
        get
        {
            if (_instance != null)
                return _instance;
            _instance = FindFirstObjectByType<CutsceneDirector>(FindObjectsInactive.Include);
            if (_instance == null)
            {
                var go = new GameObject(nameof(CutsceneDirector));
                _instance = go.AddComponent<CutsceneDirector>();
            }
            return _instance;
        }
    }

    [SerializeField, Min(0f),
     Tooltip("Real-time (unscaled) seconds after a cinematic ends during which new cutscene " +
             "cinematics are rejected, to prevent back-to-back whiplash.")]
    float cinematicCooldownSeconds = 0.35f;

    enum Stage { Idle, Active, Cooldown }
    Stage _stage = Stage.Idle;
    object _owner;
    float _cooldownUntilUnscaled;

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(this); return; }
        _instance = this;
    }

    void OnDestroy() { if (_instance == this) _instance = null; }

    void Update() { RefreshCooldown(); }

    void RefreshCooldown()
    {
        if (_stage == Stage.Cooldown && Time.unscaledTime >= _cooldownUntilUnscaled)
            _stage = Stage.Idle;
    }

    // Returns true and locks the stage to `owner` when the cinematic is allowed to start.
    public bool TryBegin(object owner)
    {
        RefreshCooldown();                 // lazy in case Update hasn't ticked this frame
        if (_stage != Stage.Idle || owner == null)
            return false;
        _stage = Stage.Active;
        _owner = owner;
        return true;
    }

    // Releases the stage (only the current owner may) and starts the cooldown.
    public void End(object owner)
    {
        if (_stage != Stage.Active || !ReferenceEquals(_owner, owner))
            return;                        // ignore End from a non-owner / stale caller
        _owner = null;
        _stage = Stage.Cooldown;
        _cooldownUntilUnscaled = Time.unscaledTime + cinematicCooldownSeconds;
    }

    public bool IsOwner(object owner) => _stage == Stage.Active && ReferenceEquals(_owner, owner);
}
