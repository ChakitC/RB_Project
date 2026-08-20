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
    int _nextSlowHandle = 1;

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

    /// <summary>Handle ของ slow ที่กำลังทำงานอยู่ (0 = ไม่มี) ใช้กันไม่ให้ผู้เรียกคนหนึ่งไปหยุด slow ของอีกคน</summary>
    public int ActiveSlowHandle { get; private set; }

    /// <summary>ความเข้มของสโลว์ตาม curve ณ ตอนนี้ 0 = ปกติ, 1 = สโลว์เต็ม
    /// ให้ภาพ (tint/vignette) เกาะค่านี้แทนการนับเวลาเอง จะได้ไม่มีทางหลุด sync กับ curve</summary>
    public float SlowBlend01 { get; private set; }

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
                StopSlow();
            }
            else
            {
                float progress = _duration > 0f ? _elapsed / _duration : 1f;
                float blend = _activeShape != null ? _activeShape.Evaluate(progress) : 1f;
                blend = Mathf.Clamp01(blend);
                SlowBlend01 = blend;
                WorldTimeScale = Mathf.Lerp(1f, _targetScale, blend);
                WorldTimeScale = Mathf.Clamp(WorldTimeScale, 0.05f, 1f);
            }
        }

        WorldTime += UnscaledWorldDeltaTime;
    }

    public int StartSlow(float scale, float duration)
        => StartSlow(scale, duration, null);

    public int StartSlow(float scale, float duration, AnimationCurve shape)
    {
        scale = Mathf.Clamp(scale, 0.05f, 1f);
        duration = Mathf.Max(0f, duration);

        if (duration <= 0f || scale >= 1f)
        {
            StopSlow();
            return 0;
        }

        _targetScale = scale;
        _duration = duration;
        _elapsed = 0f;
        _activeShape = ResolveShape(shape);
        _active = true;
        IsSlowing = true;
        ActiveSlowHandle = _nextSlowHandle++;

        float blend = _activeShape != null ? _activeShape.Evaluate(0f) : 1f;
        blend = Mathf.Clamp01(blend);
        SlowBlend01 = blend;
        WorldTimeScale = Mathf.Lerp(1f, _targetScale, blend);
        WorldTimeScale = Mathf.Clamp(WorldTimeScale, 0.05f, 1f);
        return ActiveSlowHandle;
    }

    /// <summary>หยุดเฉพาะเมื่อ slow ที่ทำงานอยู่เป็นของ handle นี้จริงๆ</summary>
    public bool StopSlow(int handle)
    {
        if (handle == 0 || handle != ActiveSlowHandle)
            return false;

        StopSlow();
        return true;
    }

    public void StopSlow()
    {
        _active = false;
        _elapsed = 0f;
        _duration = 0f;
        _activeShape = null;
        ActiveSlowHandle = 0;
        SlowBlend01 = 0f;
        WorldTimeScale = 1f;
        IsSlowing = false;
    }

    AnimationCurve ResolveShape(AnimationCurve shape)
    {
        if (shape != null && shape.length > 0)
            return shape;

        if (_defaultSlowShape == null || _defaultSlowShape.length == 0)
            _defaultSlowShape = BuildDefaultShape();

        return _defaultSlowShape;
    }

    static AnimationCurve BuildDefaultShape()
    {
        return AnimationCurve.Constant(0f, 1f, 1f);
    }
}
