using System.Collections;
using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class UIFader : MonoBehaviour
{
    [Header("Fade")]
    public float fadeDuration = 0.25f;
    public bool useUnscaledTime = true;

    [Header("After Fade Out")]
    public bool disableObjectWhenHidden = false; // ถ้า true จะ SetActive(false) หลังเฟดออกเสร็จ

    CanvasGroup _cg;
    Coroutine _co;

    void Awake()
    {
        _cg = GetComponent<CanvasGroup>();
    }

    // ---------- Public API (เรียกจากปุ่ม/สคริปต์อื่น) ----------

    public void FadeIn()  => FadeTo(1f);
    public void FadeOut() => FadeTo(0f);

    public void Toggle()
    {
        float target = (_cg.alpha >= 0.5f) ? 0f : 1f;
        FadeTo(target);
    }

    public void FadeTo(float targetAlpha)
    {
        targetAlpha = Mathf.Clamp01(targetAlpha);

        // ถ้าปิดไว้ แล้วกำลังจะเฟดเข้า ต้องเปิดก่อน
        if (targetAlpha > 0f && disableObjectWhenHidden && !gameObject.activeSelf)
            gameObject.SetActive(true);

        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(FadeRoutine(targetAlpha));
    }

    // ---------- Internal ----------

    IEnumerator FadeRoutine(float targetAlpha)
    {
        float start = _cg.alpha;
        float t = 0f;

        // กันกด/ลาก ระหว่างเฟด (จะปล่อยก็ต่อเมื่อ alpha > 0.01)
        _cg.interactable = false;
        _cg.blocksRaycasts = false;

        float dur = Mathf.Max(0.0001f, fadeDuration);

        while (t < dur)
        {
            t += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float a = Mathf.Lerp(start, targetAlpha, t / dur);
            _cg.alpha = a;
            yield return null;
        }

        _cg.alpha = targetAlpha;

        bool visible = _cg.alpha > 0.01f;
        _cg.interactable = visible;
        _cg.blocksRaycasts = visible;

        if (!visible && disableObjectWhenHidden)
            gameObject.SetActive(false);

        _co = null;
    }
}