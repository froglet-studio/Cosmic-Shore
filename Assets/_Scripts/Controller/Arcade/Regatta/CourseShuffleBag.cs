using System;
using System.Collections.Generic;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Deals the entries of a course pool like a shuffled deck: every ENABLED entry once, in a random order,
    /// before any entry comes round again, and never the same entry twice in a row across the reshuffle
    /// (when the pool has two or more). A uniform roll can lay one circuit three matches running; a bag keeps a
    /// session's races spread over the whole pool, which is what makes a pool of courses whose spreads CANCEL
    /// fair over an evening (Garrett, 2026-11-06: a 1.25x course is fine only if the pool's spreads cancel).
    ///
    /// <para>Pure (no UnityEngine): the host holds one per pool asset for the app session, and the tests drive
    /// it with a seeded <see cref="Random"/>. A change in WHICH entries are enabled (or how many there are)
    /// starts a fresh deck.</para>
    /// </summary>
    public sealed class CourseShuffleBag
    {
        readonly List<int> _deck = new();
        int _signature = int.MinValue;

        /// <summary>The entry dealt last, or -1 before the first deal.</summary>
        public int Last { get; private set; } = -1;

        /// <summary>Entries still to come before the next reshuffle.</summary>
        public int Remaining => _deck.Count;

        /// <summary>
        /// The next entry, 0-based, among <paramref name="enabled"/>.Count entries; -1 when none is enabled.
        /// </summary>
        public int Next(IReadOnlyList<bool> enabled, Random rng)
        {
            if (enabled == null || rng == null) return -1;

            int signature = Signature(enabled);
            if (signature != _signature)
            {
                _signature = signature;
                _deck.Clear();
            }

            if (_deck.Count == 0)
            {
                for (int i = 0; i < enabled.Count; i++)
                    if (enabled[i]) _deck.Add(i);
                if (_deck.Count == 0) return -1;

                // Fisher-Yates; the deck is dealt from its END.
                for (int i = _deck.Count - 1; i > 0; i--)
                {
                    int j = rng.Next(i + 1);
                    (_deck[i], _deck[j]) = (_deck[j], _deck[i]);
                }

                // No repeat across the reshuffle: if the first card of the new deck is the one just dealt,
                // swap it with another card of the deck.
                int top = _deck.Count - 1;
                if (_deck.Count > 1 && _deck[top] == Last)
                {
                    int k = rng.Next(top);
                    (_deck[top], _deck[k]) = (_deck[k], _deck[top]);
                }
            }

            int dealt = _deck[_deck.Count - 1];
            _deck.RemoveAt(_deck.Count - 1);
            Last = dealt;
            return dealt;
        }

        static int Signature(IReadOnlyList<bool> enabled)
        {
            unchecked
            {
                int h = 17 + 31 * enabled.Count;
                for (int i = 0; i < enabled.Count; i++) h = h * 31 + (enabled[i] ? 1 : 0) * (i + 1);
                return h;
            }
        }
    }
}
