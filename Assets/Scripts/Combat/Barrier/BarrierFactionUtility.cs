/// <summary>
/// Compatibility wrapper kept so existing barrier call sites and tests keep compiling.
/// The rule itself lives in <see cref="CharacterFactionUtility"/>, which every system that needs a
/// team check shares - barriers, taunt, and party effects must not drift apart on who counts as an
/// enemy.
/// </summary>
public static class BarrierFactionUtility
{
    /// <inheritdoc cref="CharacterFactionUtility.AreHostile"/>
    public static bool AreHostile(CharacteContext a, CharacteContext b) =>
        CharacterFactionUtility.AreHostile(a, b);

    /// <inheritdoc cref="CharacterFactionUtility.AreFriendly"/>
    public static bool AreFriendly(CharacteContext a, CharacteContext b) =>
        CharacterFactionUtility.AreFriendly(a, b);
}
