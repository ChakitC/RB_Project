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

    public void Bind(HealthSystem health, StaggerMeter stagger)
    {
        Unbind();

        healthSystem = health;
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
}
