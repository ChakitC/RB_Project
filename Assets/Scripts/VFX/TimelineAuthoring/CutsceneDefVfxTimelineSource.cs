#if UNITY_EDITOR
using UnityEngine;

// Cutscene VFX entry for a standalone CutsceneDefSO asset (e.g. CharacterStats.introChainCutscene).
public sealed class CutsceneDefVfxTimelineSource : CutsceneVfxTimelineSourceBase
{
    readonly CutsceneDefSO _asset;

    public CutsceneDefVfxTimelineSource(CutsceneDefSO asset)
    {
        _asset = asset;
        LoadSavedCues();
    }

    protected override ScriptableObject Owner => _asset;
    protected override CutsceneDef Cutscene => _asset?.cutscene;
}
#endif
