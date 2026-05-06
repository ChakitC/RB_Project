using System.Collections;
using UnityEngine;

[DefaultExecutionOrder(-10000)]
public sealed class TimeSlowManager : MonoBehaviour
{
    static TimeSlowManager _instance;

    Coroutine _slowRoutine;

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
    }

    void Update()
    {
        WorldTime += UnscaledWorldDeltaTime;
    }

    public void StartSlow(float scale, float duration)
    {
        scale = Mathf.Clamp(scale, 0.05f, 1f);
        duration = Mathf.Max(0f, duration);

        if (_slowRoutine != null)
        {
            StopCoroutine(_slowRoutine);
            _slowRoutine = null;
        }

        if (duration <= 0f || scale >= 1f)
        {
            WorldTimeScale = 1f;
            IsSlowing = false;
            return;
        }

        _slowRoutine = StartCoroutine(SlowRoutine(scale, duration));
    }

    IEnumerator SlowRoutine(float scale, float duration)
    {
        WorldTimeScale = scale;
        IsSlowing = true;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        WorldTimeScale = 1f;
        IsSlowing = false;
        _slowRoutine = null;
    }
}
