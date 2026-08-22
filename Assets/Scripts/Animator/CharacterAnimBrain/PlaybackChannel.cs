internal sealed class PlaybackChannel
{
    public readonly PlaybackRequestState Request = new();
    public CharacterAnimBrain.PlaybackKind Kind;

    public int RequestId => Request.RequestId;
    public bool IsActive => Request.IsOpen;

    public void Clear()
    {
        Kind = CharacterAnimBrain.PlaybackKind.None;
        Request.Clear();
    }
}
