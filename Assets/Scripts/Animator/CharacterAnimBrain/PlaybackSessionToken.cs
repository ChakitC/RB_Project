internal readonly struct PlaybackSessionToken
{
    internal readonly PlaybackChannel Channel;
    internal readonly int RequestId, BindingGeneration;
    internal readonly ulong PlaybackGeneration;
    internal PlaybackSessionToken(PlaybackChannel channel, int bindingGeneration)
    {
        Channel = channel;
        RequestId = channel.RequestId;
        PlaybackGeneration = channel.Request.Generation;
        BindingGeneration = bindingGeneration;
    }
}
