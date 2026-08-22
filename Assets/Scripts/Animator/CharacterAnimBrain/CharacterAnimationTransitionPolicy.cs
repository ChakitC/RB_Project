/// <summary>
/// The single place that decides whether one animation mode may take locomotion from another.
/// Pure and stateless, so it is directly table-testable.
///
/// This layer owns <em>admission</em> only — who is allowed to interrupt whom, and under what
/// authority. It deliberately does not know about clips: whether a state has a valid clip, and
/// whether a state is currently refusing to exit (a full-body reload inside its locked window, the
/// chain before <c>AllowChainStateExit</c>), stays with the state that owns that data. The
/// effective answer a caller observes is therefore <c>policy AND state checks</c>, and a `true`
/// here only means "no priority rule objects".
/// </summary>
public static class CharacterAnimationTransitionPolicy
{
    /// <summary>
    /// Chain playback owns the character outright while it runs. Only authority above ordinary
    /// gameplay gets through: death/down, a cinematic taking over, or the character losing control.
    /// A status effect does not qualify — a stun cannot cut a chain attack short.
    /// </summary>
    public static bool AllowsExternalCommand(CharacterAnimationMode current, CharacterAnimationTransitionReason reason)
    {
        if (current != CharacterAnimationMode.Chain)
            return true;

        return reason == CharacterAnimationTransitionReason.LifeStateOverride ||
               reason == CharacterAnimationTransitionReason.CinematicOverride ||
               reason == CharacterAnimationTransitionReason.ExternalControlLoss;
    }

    public static bool CanStart(
        CharacterAnimationMode current,
        CharacterAnimationMode requested,
        CharacterAnimationTransitionReason reason,
        bool isDowned = false)
    {
        return CanStart(new CharacterAnimationTransitionRequest(current, requested, reason, isDowned));
    }

    public static bool CanStart(in CharacterAnimationTransitionRequest request)
    {
        // 1. Death is absorbing. The death pose never yields, so nothing else can begin.
        if (request.Current == CharacterAnimationMode.Dead)
            return request.Requested == CharacterAnimationMode.Dead;

        // 2. Chain ownership.
        if (!AllowsExternalCommand(request.Current, request.Reason))
            return false;

        // 3. One chain at a time.
        if (request.Requested == CharacterAnimationMode.Chain && request.Current == CharacterAnimationMode.Chain)
            return false;

        // 4. A skill will not start on top of a playback that already owns a cast request.
        //    Utility deliberately does not carry the same restriction: a warp is allowed to
        //    interrupt a skill, which is how the chain warp-out reads at the call site.
        if (request.Requested == CharacterAnimationMode.Skill &&
            (request.Current == CharacterAnimationMode.Skill ||
             request.Current == CharacterAnimationMode.Utility ||
             request.Current == CharacterAnimationMode.Chain))
        {
            return false;
        }

        // 5. A downed character keeps to what crawl allows. Dash and melee are not listed because
        //    they never carried a downed check; only these five did.
        if (request.IsDowned)
        {
            switch (request.Requested)
            {
                case CharacterAnimationMode.Skill:
                case CharacterAnimationMode.Utility:
                case CharacterAnimationMode.Chain:
                case CharacterAnimationMode.FullBodyReload:
                case CharacterAnimationMode.Knockback:
                    return false;
            }
        }

        // 6. Knockback needs a character that is still standing.
        if (request.Requested == CharacterAnimationMode.Knockback && request.IsDowned)
            return false;

        // 7. Status locomotion takeover.
        if (request.Requested == CharacterAnimationMode.SoftStatus || request.Requested == CharacterAnimationMode.HardStatus)
        {
            // Knockback drives the body itself; no status pose may fight it, hard or soft.
            if (request.Current == CharacterAnimationMode.Knockback)
                return false;

            // A hard pose (stun/mini-stun/freeze/chain-ready) overrides anything else that is
            // running. A soft pose only decorates an otherwise idle character.
            if (request.Requested == CharacterAnimationMode.SoftStatus && !IsSoftStatusTakeoverAllowed(request.Current))
                return false;
        }

        return true;
    }

    static bool IsSoftStatusTakeoverAllowed(CharacterAnimationMode current)
    {
        return current == CharacterAnimationMode.Locomotion ||
               current == CharacterAnimationMode.Crawl ||
               current == CharacterAnimationMode.SoftStatus ||
               current == CharacterAnimationMode.HardStatus;
    }
}
