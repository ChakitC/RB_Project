using UnityEngine;

[CreateAssetMenu(menuName = "Game/Basement/Menu Definition", fileName = "BasementMenu")]
public sealed class BasementMenuDefinitionSO : ScriptableObject
{
    public BasementMenuCameraPresetSO defaultCamera;
    [Min(0f)] public float blendDuration = 0.8f;
    [Min(0f)] public float uiFadeDuration = 0.5f;
    public bool hideOtherCharacters = true;
    [Min(0f)] public float otherCharactersFadeDuration = 0.45f;
}
