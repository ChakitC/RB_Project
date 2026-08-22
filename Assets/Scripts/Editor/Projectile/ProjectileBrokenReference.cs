#if UNITY_EDITOR
using System;

/// <summary>One serialized reference in a prefab whose target GUID no longer exists in the project.</summary>
public readonly struct ProjectileBrokenReference
{
    public readonly string AssetPath;
    public readonly string PropertyName;
    public readonly string MissingGuid;

    public ProjectileBrokenReference(string assetPath, string propertyName, string missingGuid)
    {
        AssetPath = assetPath;
        PropertyName = propertyName;
        MissingGuid = missingGuid;
    }

    /// <summary>
    /// True for the fields that point at a projectile or bullet prefab. Keeps projectile validation
    /// focused instead of reporting every unrelated dangling reference in the project.
    /// </summary>
    public bool IsProjectileReference =>
        !string.IsNullOrEmpty(PropertyName) &&
        (PropertyName.IndexOf("projectile", StringComparison.OrdinalIgnoreCase) >= 0 ||
         PropertyName.IndexOf("bullet", StringComparison.OrdinalIgnoreCase) >= 0);

    public override string ToString() =>
        $"{AssetPath}: '{PropertyName}' points at missing asset guid {MissingGuid}.";
}
#endif
