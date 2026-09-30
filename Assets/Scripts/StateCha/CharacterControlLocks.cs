using System.Collections.Generic;
using System.Text;

public enum CharacterControlSource { StatusEffects, Stagger, Reaction, Legacy }

public sealed class CharacterControlLocks
{
    readonly Dictionary<CharacterControlSource, (ControlBlockFlags flags, bool stunned)> sources = new();
    readonly Dictionary<int, (ControlBlockFlags flags, string owner)> tokens = new();
    int nextToken;
    public ControlBlockFlags Flags { get; private set; }
    public bool Stunned { get; private set; }

    public bool Set(CharacterControlSource source, ControlBlockFlags flags, bool stunned)
    {
        if (sources.TryGetValue(source, out var old) && old.flags == flags && old.stunned == stunned) return false;
        sources[source] = (flags, stunned);
        Rebuild();
        return true;
    }
    public int Acquire(ControlBlockFlags flags, string owner)
    {
        if (flags == ControlBlockFlags.None) return 0;
        do { nextToken = nextToken == int.MaxValue ? 1 : nextToken + 1; } while (tokens.ContainsKey(nextToken));
        tokens.Add(nextToken, (flags, string.IsNullOrEmpty(owner) ? "External" : owner));
        Rebuild();
        return nextToken;
    }
    public bool Release(int token)
    {
        if (token == 0 || !tokens.Remove(token)) return false;
        Rebuild();
        return true;
    }
    void Rebuild()
    {
        Flags = ControlBlockFlags.None;
        Stunned = false;
        foreach (var value in sources.Values) { Flags |= value.flags; Stunned |= value.stunned; }
        foreach (var value in tokens.Values) Flags |= value.flags;
    }
    public string Describe()
    {
        var text = new StringBuilder();
        foreach (var pair in sources)
            if (pair.Value.flags != ControlBlockFlags.None || pair.Value.stunned)
                text.AppendLine($"{pair.Key}: {pair.Value.flags}, stun={pair.Value.stunned}");
        foreach (var pair in tokens) text.AppendLine($"{pair.Value.owner} [{pair.Key}]: {pair.Value.flags}");
        return text.ToString();
    }
}
