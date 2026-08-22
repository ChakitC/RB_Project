#if UNITY_EDITOR
using UnityEngine;

/// <summary>
/// Test-only witness for the atomic spawn lifecycle. It records what the projectile looked like at
/// the exact moment Unity ran OnEnable, which is the moment the old pool activated an instance
/// before its caller had written any runtime state.
///
/// It lives in the runtime assembly because Unity refuses to attach a MonoBehaviour that lives in
/// an Editor assembly to a GameObject, but it is compiled only in the Editor so it can never ship
/// on a prefab. Nothing in gameplay should reference it.
/// </summary>
// ExecuteAlways so OnEnable also fires in Edit Mode; without it the Edit Mode suite could not
// observe the activation moment at all.
[ExecuteAlways]
public sealed class ProjectileSpawnProbe : MonoBehaviour
{
    public int EnableCount { get; private set; }
    public int DisableCount { get; private set; }

    public int LayerAtEnable { get; private set; } = -1;
    public Vector3 DirectionAtEnable { get; private set; }
    public int ChainDepthAtEnable { get; private set; } = -1;
    public int SplitGenerationAtEnable { get; private set; } = -1;
    public bool UseAreaDamageAtEnable { get; private set; }
    public ProjectileConfig ConfigAtEnable { get; private set; }

    void OnEnable()
    {
        EnableCount++;

        LayerAtEnable = gameObject.layer;

        var projectile = GetComponent<Projectile>();
        if (projectile == null)
            return;

        DirectionAtEnable = projectile.Direction;
        ChainDepthAtEnable = projectile.ChainDepth;
        SplitGenerationAtEnable = projectile.SplitGeneration;
        UseAreaDamageAtEnable = projectile.useAreaDamage;
        ConfigAtEnable = projectile.config;
    }

    void OnDisable() => DisableCount++;
}
#endif
