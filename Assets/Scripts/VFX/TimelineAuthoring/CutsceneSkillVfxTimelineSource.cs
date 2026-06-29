#if UNITY_EDITOR
using UnityEngine;

// Cutscene VFX entry for a cutscene SkillGemDefinition (its CutsceneDef lives inline on the skill).
public sealed class CutsceneSkillVfxTimelineSource : CutsceneVfxTimelineSourceBase
{
    readonly SkillGemDefinition _skill;

    public CutsceneSkillVfxTimelineSource(SkillGemDefinition skill)
    {
        _skill = skill;
        LoadSavedCues();
    }

    protected override ScriptableObject Owner => _skill;
    protected override CutsceneDef Cutscene => _skill?.CutsceneDef;
}
#endif
