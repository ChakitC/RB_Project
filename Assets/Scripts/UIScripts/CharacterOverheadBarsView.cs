using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CharacterOverheadBarsView : MonoBehaviour
{
    [SerializeField] private Slider healthSlider;
    [SerializeField] private Slider staggerSlider;
    [SerializeField] private GameObject staggerRoot;
    [SerializeField] private ChainReadyPromptView chainReadyPrompt;

    HealthSystem healthSystem;
    ChainAttackTestTarget testTarget;
    StaggerMeter staggerMeter;
    CanvasGroup canvasGroup;
    Transform ownerRoot;
    float nextVisibilityCheck;

    void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    void LateUpdate()
    {
        UpdateVisibility();
    }

    public void Bind(HealthSystem health, StaggerMeter stagger)
    {
        Unbind();

        healthSystem = health;
        ownerRoot = healthSystem != null ? healthSystem.transform.root : transform.root;
        if (healthSystem != null)
        {
            healthSystem.HealthChanged += OnHealthChanged;
            OnHealthChanged(healthSystem.currentHealth, healthSystem.maximumHealth);
        }
        else
        {
            SetSliderValue(healthSlider, 0f, 1f);
        }

        BindStagger(stagger);
    }

    public void Bind(ChainAttackTestTarget target, StaggerMeter stagger)
    {
        Unbind();

        testTarget = target;
        ownerRoot = testTarget != null ? testTarget.transform.root : transform.root;
        if (testTarget != null)
        {
            testTarget.HealthChanged += OnHealthChanged;
            OnHealthChanged(testTarget.CurrentHealth, testTarget.MaxHealth);
        }
        else
        {
            SetSliderValue(healthSlider, 0f, 1f);
        }

        BindStagger(stagger);
    }

    void BindStagger(StaggerMeter stagger)
    {
        staggerMeter = stagger;

        if (staggerRoot != null)
            staggerRoot.SetActive(staggerMeter != null);

        if (staggerMeter != null)
        {
            staggerMeter.MeterChanged += OnStaggerChanged;
            OnStaggerChanged(staggerMeter.CurrentStagger, staggerMeter.MaxStagger);
        }
        else
        {
            SetSliderValue(staggerSlider, 0f, 1f);
        }

        chainReadyPrompt?.Bind(staggerMeter);
    }

    public void Unbind()
    {
        if (healthSystem != null)
            healthSystem.HealthChanged -= OnHealthChanged;

        if (testTarget != null)
            testTarget.HealthChanged -= OnHealthChanged;

        if (staggerMeter != null)
            staggerMeter.MeterChanged -= OnStaggerChanged;

        chainReadyPrompt?.Bind(null);
        healthSystem = null;
        testTarget = null;
        staggerMeter = null;
        ownerRoot = null;
    }

    void OnDisable()
    {
        Unbind();
    }

    void OnHealthChanged(float current, float maximum)
    {
        SetSliderValue(healthSlider, current, maximum);
    }

    void OnStaggerChanged(float current, float maximum)
    {
        SetSliderValue(staggerSlider, current, maximum);
    }

    static void SetSliderValue(Slider slider, float current, float maximum)
    {
        if (slider == null)
            return;

        slider.maxValue = Mathf.Max(1f, maximum);
        slider.value = Mathf.Clamp(current, 0f, slider.maxValue);
    }

    void UpdateVisibility()
    {
        if (canvasGroup == null)
            return;
        if (Time.unscaledTime < nextVisibilityCheck)
            return;

        nextVisibilityCheck = Time.unscaledTime + 0.1f;

        Camera camera = Camera.main;
        if (camera == null)
        {
            canvasGroup.alpha = 1f;
            return;
        }

        Vector3 direction = transform.position - camera.transform.position;
        float distance = direction.magnitude;
        if (distance <= 0.001f)
        {
            canvasGroup.alpha = 1f;
            return;
        }

        bool blocked = Physics.Raycast(
            camera.transform.position,
            direction / distance,
            out RaycastHit hit,
            distance,
            ~0,
            QueryTriggerInteraction.Ignore) &&
            ownerRoot != null &&
            hit.transform != ownerRoot &&
            !hit.transform.IsChildOf(ownerRoot);
        canvasGroup.alpha = blocked ? 0f : 1f;
    }
}
