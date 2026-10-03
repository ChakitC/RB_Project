internal readonly struct CharacterAnimationDiagnosticSnapshot
{
    internal readonly string State;
    internal readonly CharacterAnimationMode Mode;
    internal readonly PlaybackSessionToken Skill, Utility, Chain, SpecialReaction;
    internal readonly int BindingGeneration, PendingCleanup, PendingSignals, CallbackCount, VfxSessions;
    internal readonly float LocomotionWeight, UpperWeight, FullBodyWeight;

    internal CharacterAnimationDiagnosticSnapshot(string state, CharacterAnimationMode mode,
        PlaybackSessionCoordinator sessions, int cleanup, int pendingSignals, int callbacks, int vfxSessions,
        float locomotion, float upper, float fullBody)
    {
        State = state;
        Mode = mode;
        BindingGeneration = sessions.BindingGeneration;
        Skill = sessions.Capture(sessions.Skill);
        Utility = sessions.Capture(sessions.Utility);
        Chain = sessions.Capture(sessions.Chain);
        SpecialReaction = sessions.Capture(sessions.SpecialReaction);
        PendingCleanup = cleanup;
        PendingSignals = pendingSignals;
        CallbackCount = callbacks;
        VfxSessions = vfxSessions;
        LocomotionWeight = locomotion;
        UpperWeight = upper;
        FullBodyWeight = fullBody;
    }
}
