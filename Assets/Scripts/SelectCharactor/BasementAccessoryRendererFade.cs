using System;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Temporary material instances for fading a Basement party preview without changing its assets.</summary>
public sealed class BasementAccessoryRendererFade : IDisposable
{
    static readonly int DitherAlpha = Shader.PropertyToID("_DitherAlpha");
    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    readonly Renderer renderer;
    readonly bool originallyHidden;
    readonly Material[] originalMaterials;
    readonly Material[] fadeMaterials;
    readonly float[] originalDither;
    readonly Color[] originalColors;

    public BasementAccessoryRendererFade(Renderer target)
    {
        renderer = target;
        originallyHidden = target.forceRenderingOff;
        originalMaterials = target.sharedMaterials;
        fadeMaterials = new Material[originalMaterials.Length];
        originalDither = new float[originalMaterials.Length];
        originalColors = new Color[originalMaterials.Length];
        for (int i = 0; i < originalMaterials.Length; i++)
        {
            var source = originalMaterials[i];
            if (source == null) continue;
            var material = new Material(source) { hideFlags = HideFlags.HideAndDontSave };
            fadeMaterials[i] = material;
            if (material.HasProperty(DitherAlpha))
                originalDither[i] = material.GetFloat(DitherAlpha);
            else if (material.shader.name == "Universal Render Pipeline/Lit")
            {
                // A few preview eye/prop materials use URP Lit instead of the character shader.
                originalColors[i] = material.GetColor(BaseColor);
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.DisableKeyword("_ALPHAMODULATE_ON");
                material.SetOverrideTag("RenderType", "Transparent");
                material.renderQueue = (int)RenderQueue.Transparent;
                material.SetShaderPassEnabled("ShadowCaster", false);
            }
            else
                Debug.LogWarning($"[Basement Accessory] {source.name} uses unsupported fade shader {source.shader.name}.", target);
        }
        target.sharedMaterials = fadeMaterials;
    }

    public void Apply(float hiddenAmount)
    {
        if (renderer == null) return;
        hiddenAmount = Mathf.Clamp01(hiddenAmount);
        renderer.forceRenderingOff = originallyHidden || hiddenAmount >= 1f;
        for (int i = 0; i < fadeMaterials.Length; i++)
        {
            var material = fadeMaterials[i];
            if (material == null) continue;
            if (material.HasProperty(DitherAlpha))
                material.SetFloat(DitherAlpha, Mathf.Lerp(originalDither[i], 1f, hiddenAmount));
            else if (material.shader.name == "Universal Render Pipeline/Lit")
            {
                var color = originalColors[i];
                color.a *= 1f - hiddenAmount;
                material.SetColor(BaseColor, color);
            }
        }
    }

    public void Dispose()
    {
        if (renderer != null)
        {
            renderer.sharedMaterials = originalMaterials;
            renderer.forceRenderingOff = originallyHidden;
        }
        foreach (var material in fadeMaterials)
            if (material != null) UnityEngine.Object.Destroy(material);
    }
}
