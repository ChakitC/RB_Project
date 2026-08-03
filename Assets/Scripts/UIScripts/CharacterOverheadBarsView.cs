using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CharacterOverheadBarsView : MonoBehaviour
{
    [SerializeField] private Slider healthSlider;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private Slider staggerSlider;
    [SerializeField] private GameObject staggerRoot;
    [SerializeField] private ChainReadyPromptView chainReadyPrompt;

    HealthSystem healthSystem;
    LevelSystem levelSystem;
    EnemyLevelSystem enemyLevelSystem;
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
        CharacteContext context = health != null
            ? health.GetComponentInParent<CharacteContext>()
            : null;
        Bind(health, stagger, context);
    }

    public void Bind(HealthSystem health, StaggerMeter stagger, CharacteContext context)
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

        BindLevel(context);
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

        BindLevel(null);
        BindStagger(stagger);
    }

    void BindLevel(CharacteContext context)
    {
        if (context is EnemyContext enemyContext && enemyContext.EnemyLevelSystem != null)
        {
            enemyLevelSystem = enemyContext.EnemyLevelSystem;
            enemyLevelSystem.LevelChanged += OnLevelChanged;
            OnLevelChanged(enemyLevelSystem.Level);
            return;
        }

        if (context != null && context.levelSystem != null)
        {
            levelSystem = context.levelSystem;
            levelSystem.LevelChanged += OnLevelChanged;
            OnLevelChanged(levelSystem.Level);
            return;
        }

        if (levelText != null)
            levelText.gameObject.SetActive(false);
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

        if (levelSystem != null)
            levelSystem.LevelChanged -= OnLevelChanged;

        if (enemyLevelSystem != null)
            enemyLevelSystem.LevelChanged -= OnLevelChanged;

        if (staggerMeter != null)
            staggerMeter.MeterChanged -= OnStaggerChanged;

        chainReadyPrompt?.Bind(null);
        healthSystem = null;
        levelSystem = null;
        enemyLevelSystem = null;
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

    void OnLevelChanged(int level)
    {
        if (levelText == null)
            return;

        levelText.gameObject.SetActive(true);
        levelText.text = $"Lv {Mathf.Max(1, level)}";
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
