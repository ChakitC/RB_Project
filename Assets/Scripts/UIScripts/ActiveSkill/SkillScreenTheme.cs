using UnityEngine;

[CreateAssetMenu(fileName = "SkillScreenTheme", menuName = "Game/UI/Skill Screen Theme")]
public sealed class SkillScreenTheme : ScriptableObject
{
    [Header("Optional Sprites")]
    public Sprite screenBackground;
    public Sprite cardFrame;
    public Sprite nodeFrame;
    public Sprite importantNodeFrame;
    public Sprite connectionSprite;

    [Header("States")]
    public Color lockedColor = new(0.28f, 0.28f, 0.28f, 1f);
    public Color availableColor = new(1f, 0.72f, 0.18f, 1f);
    public Color unlockedColor = new(0.95f, 0.62f, 0.12f, 1f);
    public Color selectedColor = new(0f, 0.85f, 1f, 1f);
    public Color inactiveCardColor = new(0.45f, 0.45f, 0.45f, 1f);
    public Color activeCardColor = Color.white;
    public Color lockedConnectionColor = new(0.32f, 0.32f, 0.32f, 1f);
    public Color activeConnectionColor = new(1f, 0.72f, 0.18f, 1f);
}
