using Unity.Cinemachine;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Basement/Menu Camera Preset", fileName = "BasementMenuCamera")]
public sealed class BasementMenuCameraPresetSO : ScriptableObject
{
    [Tooltip("Authoring reference only. Never instantiated by the runtime menu.")]
    public GameObject sourceFbx;
    public string cameraPath;
    public Vector3 position;
    public Quaternion rotation = Quaternion.identity;
    public LensSettings lens = LensSettings.Default;
}
