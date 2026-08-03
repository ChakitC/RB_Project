using UnityEngine;

[DisallowMultipleComponent]
public sealed class StagePlacardButton : MonoBehaviour
{
    [SerializeField] private MapRunConfigSO runConfig;

    public MapRunConfigSO RunConfig => runConfig;

    public void EnterStage()
    {
        if (SceneLoaderSystem.Instance == null)
        {
            Debug.LogWarning("[StagePlacardButton] SceneLoaderSystem is missing.", this);
            return;
        }

        SceneLoaderSystem.Instance.LoadStage(runConfig);
    }
}
