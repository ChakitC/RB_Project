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
    StaggerMeter staggerMeter;

    public void Bind(HealthSystem health, StaggerMeter stagger)
    {
        Unbind();

        healthSystem = health;
        staggerMeter = stagger;

        if (healthSystem != null)
        {
            healthSystem.HealthChanged += OnHealthChanged;
            OnHealthChanged(healthSystem.currentHealth, healthSystem.maximumHealth);
        }
        else
        {
            SetSliderValue(healthSlider, 0f, 1f);
        }

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

        if (staggerMeter != null)
            staggerMeter.MeterChanged -= OnStaggerChanged;

        chainReadyPrompt?.Bind(null);
        healthSystem = null;
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
