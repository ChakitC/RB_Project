using System.Collections.Generic;

internal sealed class StatusLocomotionIntentResolver
{
    public StatusLocomotionPose Resolve(
        IReadOnlyList<StatusEffectInstance> activeEffects,
        StatusLocomotionPose externalPose,
        StatusLocomotionPose staggerPose)
    {
        StatusLocomotionPose best = StatusLocomotionPose.None;
        int bestPriority = 0;

        if (activeEffects != null)
        {
            for (int i = 0; i < activeEffects.Count; i++)
            {
                var instance = activeEffects[i];
                var definition = instance?.Definition;
                if (definition == null || instance.CurrentStacks <= 0)
                    continue;

                StatusLocomotionPose candidate = ResolveFromDefinition(definition);
                if (candidate == StatusLocomotionPose.None)
                    continue;

                int priority = GetPriority(candidate);
                if (priority <= bestPriority)
                    continue;

                best = candidate;
                bestPriority = priority;
            }
        }

        if (externalPose != StatusLocomotionPose.None)
        {
            int priority = GetPriority(externalPose);
            if (priority > bestPriority)
            {
                best = externalPose;
                bestPriority = priority;
            }
        }

        if (staggerPose != StatusLocomotionPose.None)
        {
            int priority = GetPriority(staggerPose);
            if (priority > bestPriority)
                return staggerPose;
        }

        return best;
    }

    public static StatusLocomotionPose ResolveFromDefinition(StatusEffectDef def)
    {
        if (def == null) return StatusLocomotionPose.None;
        if (def.locomotionPose != StatusLocomotionPose.Auto) return def.locomotionPose;
        if (def.pushStunnedState) return StatusLocomotionPose.Stun;
        if ((def.controlBlocks & ControlBlockFlags.Move) != 0) return StatusLocomotionPose.Root;
        return StatusLocomotionPose.None;
    }

    public static int GetPriority(StatusLocomotionPose kind)
    {
        return kind switch
        {
            StatusLocomotionPose.Freeze => 40,
            StatusLocomotionPose.Stun => 30,
            StatusLocomotionPose.MiniStun => 20,
            StatusLocomotionPose.Root => 10,
            _ => 0,
        };
    }

    public static StatusLocomotionPose MapReaction(ImpactReactionKind reaction)
    {
        return reaction switch
        {
            ImpactReactionKind.Root => StatusLocomotionPose.Root,
            ImpactReactionKind.MiniStun => StatusLocomotionPose.MiniStun,
            ImpactReactionKind.Stun => StatusLocomotionPose.Stun,
            _ => StatusLocomotionPose.None,
        };
    }
}
