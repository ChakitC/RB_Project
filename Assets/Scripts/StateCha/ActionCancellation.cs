[System.Flags]
public enum ActionCancellation
{
    None = 0,
    Sprint = 1 << 0,
    Slide = 1 << 1,
    Reload = 1 << 2,
    Firing = 1 << 3,
    Dash = 1 << 4
}
