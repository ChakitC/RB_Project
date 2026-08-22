using UnityEngine;

[DisallowMultipleComponent]
public sealed class StagePlacardButton : MonoBehaviour
{
    [SerializeField] private MapRunConfigSO runConfig;

    public MapRunConfigSO RunConfig => runConfig;

    /// <summary>
    /// Binds a placard created at runtime from a stage catalog. Authored placards keep using the
    /// serialized reference and never call this.
    /// </summary>
    public void SetRunConfig(MapRunConfigSO config)
    {
        runConfig = config;
    }

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
