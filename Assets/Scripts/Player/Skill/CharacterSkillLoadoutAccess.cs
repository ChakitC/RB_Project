using UnityEngine;

/// <summary>Shared permission for player/ally selection requests; initialization does not use this gate.</summary>
public static class CharacterSkillLoadoutAccess
{
    public static bool CanChange(CharacterStats stats)
    {
        if (stats == null)
            return false;
        if (stats.IsHelperRole)
            return true;

        MapRunController run = Object.FindFirstObjectByType<MapRunController>();
        if (run != null && run.IsLoadoutLocked)
            return false;

        BasementContext basement = Object.FindFirstObjectByType<BasementContext>();
        return basement != null && basement.isActiveAndEnabled && basement.gameObject.scene.isLoaded;
    }
}
