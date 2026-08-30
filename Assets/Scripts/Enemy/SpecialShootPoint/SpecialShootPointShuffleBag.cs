using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Anchor rotation for Special Shoot Point rounds.
///
/// Pure and Unity-free apart from <see cref="Random"/>, so the "every enabled anchor is consumed
/// before any repeats" rule is directly testable. The bag holds anchor <em>indices</em> rather than
/// anchors: the owning controller's authored list is the source of truth, and an anchor that
/// becomes unusable between rounds is skipped on draw instead of corrupting the bag.
/// </summary>
public sealed class SpecialShootPointShuffleBag
{
    readonly List<int> _bag = new();
    int _sourceCount;

    /// <summary>Indices still waiting to be drawn in the current pass.</summary>
    public int Remaining => _bag.Count;

    /// <summary>
    /// Draws <paramref name="count"/> distinct anchor indices, refilling the bag as it empties so a
    /// pass never repeats an anchor until every eligible one has been used.
    /// </summary>
    /// <param name="isEligible">
    /// Called per candidate index. An index that fails is dropped from this pass without being
    /// counted as consumed, which is how a disabled or destroyed anchor leaves the rotation.
    /// </param>
    /// <returns>False when fewer than <paramref name="count"/> distinct anchors are available.</returns>
    public bool TryDraw(
        int sourceCount,
        int count,
        System.Func<int, bool> isEligible,
        List<int> results)
    {
        if (results == null)
            return false;

        results.Clear();

        if (count <= 0 || sourceCount <= 0)
            return false;

        if (_sourceCount != sourceCount)
        {
            _sourceCount = sourceCount;
            _bag.Clear();
        }

        // A full refill can only ever hand back what is eligible, so the caller's count is capped by
        // the eligible population, not by the authored list length.
        int eligibleCount = 0;
        for (int i = 0; i < sourceCount; i++)
        {
            if (isEligible == null || isEligible(i))
                eligibleCount++;
        }

        if (eligibleCount < count)
            return false;

        // Two refills are the worst case: the current pass may be entirely ineligible entries.
        int refillsAllowed = 2;

        while (results.Count < count)
        {
            if (_bag.Count == 0)
            {
                if (refillsAllowed-- <= 0)
                    break;

                Refill(sourceCount);
            }

            int index = _bag[_bag.Count - 1];
            _bag.RemoveAt(_bag.Count - 1);

            if (isEligible != null && !isEligible(index))
                continue;

            if (results.Contains(index))
                continue;

            results.Add(index);
        }

        if (results.Count == count)
            return true;

        results.Clear();
        return false;
    }

    /// <summary>Drops the current pass. The next draw starts from a full, freshly shuffled bag.</summary>
    public void Reset()
    {
        _bag.Clear();
        _sourceCount = 0;
    }

    void Refill(int sourceCount)
    {
        _bag.Clear();
        for (int i = 0; i < sourceCount; i++)
            _bag.Add(i);

        // Fisher-Yates. Draws pop from the tail, so the shuffle is what makes the order random.
        for (int i = _bag.Count - 1; i > 0; i--)
        {
            int swap = Random.Range(0, i + 1);
            (_bag[i], _bag[swap]) = (_bag[swap], _bag[i]);
        }
    }
}
