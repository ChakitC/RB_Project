using TMPro;
using UnityEngine;

/// <summary>
/// Reveals a line character by character on the unscaled clock, because the world is frozen at
/// timeScale 0 while dialogue plays. Reveals by moving <see cref="TMP_Text.maxVisibleCharacters"/>
/// rather than re-assigning the string, so the layout is measured once and rich text tags are never
/// cut in half.
/// </summary>
internal sealed class DialogueTypewriter
{
    TMP_Text label;
    float charactersPerSecond;
    float revealed;
    int totalCharacters;

    public bool IsRevealing { get; private set; }

    public void Begin(TMP_Text target, string text, float speedCharactersPerSecond)
    {
        label = target;
        if (label == null)
            return;

        charactersPerSecond = Mathf.Max(1f, speedCharactersPerSecond);
        label.text = text ?? string.Empty;
        label.ForceMeshUpdate();

        totalCharacters = label.textInfo != null ? label.textInfo.characterCount : 0;
        revealed = 0f;
        label.maxVisibleCharacters = 0;
        IsRevealing = totalCharacters > 0;

        if (!IsRevealing)
            label.maxVisibleCharacters = int.MaxValue;
    }

    public void Tick(float unscaledDeltaTime)
    {
        if (!IsRevealing || label == null)
            return;

        revealed += charactersPerSecond * unscaledDeltaTime;
        int visible = Mathf.FloorToInt(revealed);

        if (visible >= totalCharacters)
        {
            CompleteImmediately();
            return;
        }

        label.maxVisibleCharacters = visible;
    }

    /// <summary>Shows the whole line at once. Used when the player advances mid-reveal.</summary>
    public void CompleteImmediately()
    {
        IsRevealing = false;
        if (label == null)
            return;

        label.maxVisibleCharacters = int.MaxValue;
    }

    public void Clear()
    {
        IsRevealing = false;
        if (label == null)
            return;

        label.text = string.Empty;
        label.maxVisibleCharacters = int.MaxValue;
        label = null;
    }
}
