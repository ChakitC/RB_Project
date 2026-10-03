using System;
using System.Collections.Generic;

internal sealed class PlaybackSignalDispatcher
{
    private readonly List<Action> pending = new();
    private int depth;
    private bool flushing;
    internal int PendingCount => pending.Count;
    internal Scope Begin() { depth++; return new Scope(this); }
    internal readonly struct Scope : IDisposable
    {
        private readonly PlaybackSignalDispatcher owner;
        internal Scope(PlaybackSignalDispatcher owner) => this.owner = owner;
        public void Dispose() { if (--owner.depth == 0) owner.Flush(); }
    }
    internal void Enqueue(Action notification)
    {
        pending.Add(notification);
        if (depth == 0) Flush();
    }
    private void Flush()
    {
        if (flushing) return;
        flushing = true;
        try
        {
            for (int i = 0; i < pending.Count; i++) SafeAnimationEvents.Invoke(pending[i]);
        }
        finally { pending.Clear(); flushing = false; }
    }
}
