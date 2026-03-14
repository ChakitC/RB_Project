using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class UiBaseMentManager : MonoBehaviour
{
    public float fadeDuration = 0.5f;

    public CanvasGroup _group;
    bool _isVisible = true;
    bool _isFading = false;

    void Awake()
    {

        if (_group == null)
        {
            Debug.LogError("[UIFader] No CanvasGroup found on this GameObject.", this);
        }
        
    }

    public void FadeOut()
    {
        if (_isFading) return;
        StartCoroutine(FadeRoutine(0f));
    }

    public void FadeIn()
    {
        if (_isFading) return;
        StartCoroutine(FadeRoutine(1f));
    }

    public void Toggle()
    {
        if (_isFading) return;
        float target = _isVisible ? 0f : 1f;
        StartCoroutine(FadeRoutine(target));
    }

    System.Collections.IEnumerator FadeRoutine(float targetAlpha)
    {
        _isFading = true;

        float startAlpha = _group.alpha;
        float time = 0f;

        if (targetAlpha > 0f)
        {
            _group.interactable = true;
            _group.blocksRaycasts = true;
        }

        while (time < fadeDuration)
        {
            time += Time.deltaTime;
            float t = time / fadeDuration;
            _group.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            yield return null;
        }

        _group.alpha = targetAlpha;

        bool visibleNow = targetAlpha > 0.99f;
        _isVisible = visibleNow;

        if (!visibleNow)
        {
            _group.interactable = false;
            _group.blocksRaycasts = false;
        }

        _isFading = false;
    }
}