using TMPro;
using UnityEngine;

/// <summary>
/// Shows a short line when a cast ran but produced nothing — for example a turret that had
/// nowhere valid to land. The cast cost the player nothing, so the message explains why the
/// button appeared to do nothing rather than reporting a failure.
/// </summary>
[DisallowMultipleComponent]
public sealed class SkillCastFeedbackPresenter : MonoBehaviour
{
    [Header("View")]
    [SerializeField] private GameObject root;
    [SerializeField] private TMP_Text messageLabel;
    [SerializeField] private CanvasGroup fadeGroup;

    [Header("Timing")]
    [SerializeField, Min(0.1f)] private float holdSeconds = 1.6f;
    [SerializeField, Min(0f)] private float fadeSeconds = 0.35f;

    CharacterSkillManager skillManager;
    float remainingHold;
    bool subscribed;

    public void Bind(CharacteContext context)
    {
        Unsubscribe();

        if (context != null)
        {
            context.ResolveReferences();
            skillManager = context.SkillManager;
        }
        else
        {
            skillManager = null;
        }

        Subscribe();
        HideImmediate();
    }

    void OnEnable() => Subscribe();

    void OnDisable()
    {
        Unsubscribe();
        HideImmediate();
    }

    void OnDestroy() => Unsubscribe();

    void Subscribe()
    {
        if (subscribed || skillManager == null || !isActiveAndEnabled)
            return;

        skillManager.CastExecutionFailed += OnCastExecutionFailed;
        subscribed = true;
    }

    void Unsubscribe()
    {
        if (!subscribed || skillManager == null)
        {
            subscribed = false;
            return;
        }

        skillManager.CastExecutionFailed -= OnCastExecutionFailed;
        subscribed = false;
    }

    void OnCastExecutionFailed(ActiveSkillCastInfo castInfo, SkillExecutionResult result)
    {
        Show(result.PublicMessage);
    }

    public void Show(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        if (messageLabel != null)
            messageLabel.text = message;

        SetRootActive(true);
        if (fadeGroup != null)
            fadeGroup.alpha = 1f;

        remainingHold = holdSeconds + fadeSeconds;
    }

    void Update()
    {
        if (remainingHold <= 0f)
            return;

        // Unscaled: a refusal message should read at the same speed during hitlag or world-slow.
        remainingHold -= Time.unscaledDeltaTime;

        if (remainingHold <= 0f)
        {
            HideImmediate();
            return;
        }

        if (fadeGroup != null && fadeSeconds > 0f && remainingHold < fadeSeconds)
            fadeGroup.alpha = Mathf.Clamp01(remainingHold / fadeSeconds);
    }

    void HideImmediate()
    {
        remainingHold = 0f;
        if (fadeGroup != null)
            fadeGroup.alpha = 0f;

        SetRootActive(false);
    }

    void SetRootActive(bool active)
    {
        GameObject target = root != null ? root : gameObject;
        if (target != gameObject && target.activeSelf != active)
            target.SetActive(active);
    }
}
