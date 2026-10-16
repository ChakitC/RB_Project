using System;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Per-renderer fading; only shaders requiring transparent keywords need material copies.</summary>
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
    readonly MaterialPropertyBlock[] originalBlocks;
    readonly MaterialPropertyBlock[] fadeBlocks;
    readonly bool hasMaterialCopies;

    public BasementAccessoryRendererFade(Renderer target)
    {
        renderer = target;
        originallyHidden = target.forceRenderingOff;
        originalMaterials = target.sharedMaterials;
        fadeMaterials = (Material[])originalMaterials.Clone();
        originalDither = new float[originalMaterials.Length];
        originalColors = new Color[originalMaterials.Length];
        originalBlocks = new MaterialPropertyBlock[originalMaterials.Length];
        fadeBlocks = new MaterialPropertyBlock[originalMaterials.Length];
        for (int i = 0; i < originalMaterials.Length; i++)
        {
            var source = originalMaterials[i];
            if (source == null) continue;
            if (source.HasProperty(DitherAlpha))
            {
                originalBlocks[i] = new MaterialPropertyBlock();
                target.GetPropertyBlock(originalBlocks[i], i);
                var block = fadeBlocks[i] = new MaterialPropertyBlock();
                target.GetPropertyBlock(block, i);
                // A per-material block takes precedence over the renderer-wide block.
                if (block.isEmpty) target.GetPropertyBlock(block);
                originalDither[i] = block.HasFloat(DitherAlpha) ? block.GetFloat(DitherAlpha) : source.GetFloat(DitherAlpha);
                continue;
            }
            if (source.shader.name != "Universal Render Pipeline/Lit")
            {
                Debug.LogWarning($"[Basement Accessory] {source.name} uses unsupported fade shader {source.shader.name}.", target);
                continue;
            }
            var material = new Material(source) { hideFlags = HideFlags.HideAndDontSave };
            fadeMaterials[i] = material;
            hasMaterialCopies = true;
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
        }
        if (hasMaterialCopies) target.sharedMaterials = fadeMaterials;
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
            if (fadeBlocks[i] != null)
            {
                fadeBlocks[i].SetFloat(DitherAlpha, Mathf.Lerp(originalDither[i], 1f, hiddenAmount));
                renderer.SetPropertyBlock(fadeBlocks[i], i);
            }
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
            if (hasMaterialCopies) renderer.sharedMaterials = originalMaterials;
            for (int i = 0; i < originalBlocks.Length; i++)
                if (originalBlocks[i] != null)
                    renderer.SetPropertyBlock(originalBlocks[i].isEmpty ? null : originalBlocks[i], i);
            renderer.forceRenderingOff = originallyHidden;
        }
        for (int i = 0; i < fadeMaterials.Length; i++)
            if (fadeMaterials[i] != null && fadeMaterials[i] != originalMaterials[i])
                UnityEngine.Object.Destroy(fadeMaterials[i]);
    }
}
