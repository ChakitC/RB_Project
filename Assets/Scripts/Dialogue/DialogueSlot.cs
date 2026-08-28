/// <summary>
/// Fixed stage positions for dialogue actors. V1 has exactly three; an actor keeps its slot for the
/// whole sequence and is emphasized in place when it speaks, so nobody slides between slots.
/// </summary>
public enum DialogueSlot
{
    Left = 0,
    Center = 1,
    Right = 2,
}
