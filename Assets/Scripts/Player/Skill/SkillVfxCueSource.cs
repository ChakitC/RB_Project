using System.Collections.Generic;

public sealed class SkillVfxCueSource : IAnimationVfxCueSource
{
    readonly IReadOnlyList<SkillVfxEvent> _cues;

    public SkillVfxCueSource(IReadOnlyList<SkillVfxEvent> cues)
    {
        _cues = cues;
    }

    public int CueCount => _cues != null ? _cues.Count : 0;

    public IAnimationVfxCue GetCue(int index)
    {
        return _cues != null && index >= 0 && index < _cues.Count ? _cues[index] : null;
    }
}
