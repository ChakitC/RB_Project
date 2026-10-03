/// <summary>One press produces either a released Active or a held Ultimate, never both.</summary>
public sealed class PartyHudPressState
{
    public const double HoldSeconds = 0.4;
    bool pressed;
    double started;
    public void Begin(double now) { pressed = true; started = now; }
    public bool Hold(double now)
    {
        if (!pressed || now - started < HoldSeconds) return false;
        pressed = false;
        return true;
    }
    public int Release(double now)
    {
        if (!pressed) return -1;
        pressed = false;
        return now - started >= HoldSeconds ? 1 : 0;
    }
    public void Cancel() => pressed = false;
}
