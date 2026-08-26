using UnityEngine;

public static class CharacterPlacementReservationRegistry
{
    static readonly CharacterPlacementReservationService shared =
        new CharacterPlacementReservationService();

    public static CharacterPlacementReservationService Shared => shared;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetForSubsystem()
    {
        shared.Clear();
    }
}
