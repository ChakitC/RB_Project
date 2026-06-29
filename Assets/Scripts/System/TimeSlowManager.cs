using UnityEngine;

[DefaultExecutionOrder(-10000)]
public sealed class TimeSlowManager : MonoBehaviour
{
    static TimeSlowManager _instance;

    [SerializeField] AnimationCurve _defaultSlowShape;

    float _targetScale;
    float _duration;
    float _elapsed;
    AnimationCurve _activeShape;
    bool _active;

    public static TimeSlowManager Instance
    {
        get
        {
            if (_instance != null)
                return _instance;

            _instance = FindAnyObjectByType<TimeSlowManager>();
            if (_instance != null)
                return _instance;

            GameObject managerObject = new GameObject(nameof(TimeSlowManager));
            _instance = managerObject.AddComponent<TimeSlowManager>();
            return _instance;
        }
    }

    public float WorldTimeScale { get; private set; } = 1f;
    public bool IsSlowing { get; private set; }
    public float WorldDeltaTime => Time.deltaTime * WorldTimeScale;
    public float UnscaledWorldDeltaTime => Time.unscaledDeltaTime * WorldTimeScale;
    public float WorldTime { get; private set; }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);

        if (_defaultSlowShape == null || _defaultSlowShape.length == 0)
            _defaultSlowShape = BuildDefaultShape();
    }

    void Update()
    {
        if (_active)
        {
            _elapsed += Time.unscaledDeltaTime;

            if (_elapsed >= _duration)
            {
                _active = false;
                WorldTimeScale = 1f;
                IsSlowing = false;
            }
            else
            {
                float progress = _duration > 0f ? _elapsed / _duration : 1f;
                float blend = _activeShape != null ? _activeShape.Evaluate(progress) : 1f;
                blend = Mathf.Clamp01(blend);
                WorldTimeScale = Mathf.Lerp(1f, _targetScale, blend);
                WorldTimeScale = Mathf.Clamp(WorldTimeScale, 0.05f, 1f);
            }
        }

        WorldTime += UnscaledWorldDeltaTime;
    }

    public void StartSlow(float scale, float duration)
        => StartSlow(scale, duration, null);

    public void StartSlow(float scale, float duration, AnimationCurve shape)
    {
        scale = Mathf.Clamp(scale, 0.05f, 1f);
        duration = Mathf.Max(0f, duration);

        if (duration <= 0f || scale >= 1f)
        {
            _active = false;
            WorldTimeScale = 1f;
            IsSlowing = false;
            return;
        }

        _targetScale = scale;
        _duration = duration;
        _elapsed = 0f;
        _activeShape = shape ?? _defaultSlowShape;
        _active = true;
        IsSlowing = true;

        float blend = _activeShape != null ? _activeShape.Evaluate(0f) : 1f;
        blend = Mathf.Clamp01(blend);
        WorldTimeScale = Mathf.Lerp(1f, _targetScale, blend);
        WorldTimeScale = Mathf.Clamp(WorldTimeScale, 0.05f, 1f);
    }

    static AnimationCurve BuildDefaultShape()
    {
        return AnimationCurve.Constant(0f, 1f, 1f);
    }
}
