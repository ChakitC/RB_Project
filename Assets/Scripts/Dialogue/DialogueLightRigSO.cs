using UnityEngine;

/// <summary>
/// Lighting values applied to the dialogue stage's own light rig. The rig only affects the
/// dialogue rendering layer, so nothing here touches the frozen gameplay world behind it.
/// </summary>
[CreateAssetMenu(menuName = "Game/Dialogue/Dialogue Light Rig", fileName = "DialogueLightRig")]
public sealed class DialogueLightRigSO : ScriptableObject
{
    [Header("Speaking actor")]
    [SerializeField] private Color keyColor = Color.white;
    [SerializeField, Min(0f)] private float keyIntensity = 2.2f;
    [SerializeField] private Color rimColor = new Color(0.65f, 0.75f, 1f);
    [SerializeField, Min(0f)] private float rimIntensity = 1.4f;

    [Header("Listening actors")]
    [SerializeField, Range(0f, 1f), Tooltip("Fraction of the speaking intensities applied to actors " +
                                            "that are on stage but not speaking.")]
    private float listenerIntensityScale = 0.55f;

    [Header("Ambient")]
    [SerializeField] private Color fillColor = new Color(0.35f, 0.38f, 0.5f);
    [SerializeField, Min(0f)] private float fillIntensity = 0.8f;

    public Color KeyColor => keyColor;
    public float KeyIntensity => keyIntensity;
    public Color RimColor => rimColor;
    public float RimIntensity => rimIntensity;
    public float ListenerIntensityScale => Mathf.Clamp01(listenerIntensityScale);
    public Color FillColor => fillColor;
    public float FillIntensity => fillIntensity;
}
