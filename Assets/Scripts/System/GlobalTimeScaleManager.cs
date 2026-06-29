using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-10000)]
public sealed class GlobalTimeScaleManager : MonoBehaviour
{
    static GlobalTimeScaleManager _instance;

    readonly List<HitLagRequest> _requests = new List<HitLagRequest>();
    bool _paused;

    public static GlobalTimeScaleManager Instance
    {
        get
        {
            if (_instance != null)
                return _instance;

            _instance = FindAnyObjectByType<GlobalTimeScaleManager>();
            if (_instance != null)
                return _instance;

            GameObject managerObject = new GameObject(nameof(GlobalTimeScaleManager));
            _instance = managerObject.AddComponent<GlobalTimeScaleManager>();
            return _instance;
        }
    }

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        if (_instance == this)
            _instance = null;
    }

    void Update()
    {
        float unscaledDt = Time.unscaledDeltaTime;
        bool anyExpired = false;

        for (int i = _requests.Count - 1; i >= 0; i--)
        {
            HitLagRequest req = _requests[i];
            req.Remaining -= unscaledDt;
            if (req.Remaining <= 0f)
            {
                _requests.RemoveAt(i);
                anyExpired = true;
            }
            else
            {
                _requests[i] = req;
            }
        }

        if (anyExpired || _requests.Count > 0)
            ApplyComposedScale();
    }

    public void RequestHitLag(float duration, float timeScale)
    {
        duration = Mathf.Max(0f, duration);
        timeScale = Mathf.Clamp(timeScale, 0.01f, 1f);

        if (duration <= 0f)
            return;

        _requests.Add(new HitLagRequest(duration, timeScale));
        ApplyComposedScale();
    }

    public void SetPaused(bool paused)
    {
        _paused = paused;
        ApplyComposedScale();
    }

    public void ForceReset()
    {
        _requests.Clear();
        _paused = false;
        Time.timeScale = 1f;
    }

    void ApplyComposedScale()
    {
        if (_paused)
        {
            Time.timeScale = 0f;
            return;
        }

        float scale = 1f;
        for (int i = 0; i < _requests.Count; i++)
        {
            if (_requests[i].TimeScale < scale)
                scale = _requests[i].TimeScale;
        }

        Time.timeScale = scale;
    }

    void OnSceneUnloaded(Scene scene)
    {
        ForceReset();
    }

    struct HitLagRequest
    {
        public float Remaining;
        public readonly float TimeScale;

        public HitLagRequest(float duration, float timeScale)
        {
            Remaining = duration;
            TimeScale = timeScale;
        }
    }
}
