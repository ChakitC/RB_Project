using System;
using Animancer;

public static class AnimationVfxEventBinder
{
    public static int Bind(AnimancerEvent.Sequence runtimeEvents, Action<int> handleCue)
    {
        if (runtimeEvents == null || handleCue == null)
            return 0;

        StringReference vfxEventName =
            CombatTimelineEventNames.ToStringReference(CombatTimelineEventName.Vfx);
        int cueIndex = 0;
        for (int eventIndex = 0; eventIndex < runtimeEvents.Count; eventIndex++)
        {
            if (runtimeEvents.GetName(eventIndex) != vfxEventName)
                continue;

            int capturedCueIndex = cueIndex++;
            runtimeEvents.SetCallback(eventIndex, () => handleCue(capturedCueIndex));
        }

        return cueIndex;
    }
}
