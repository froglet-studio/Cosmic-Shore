// Round 11f (Docs/ECOLOGY_LOD.md §2): one conserved stomach replaces the starvation clock
// (research DISCOVERIES "Hierarchical ecology", recommended game architecture item 3).
// Pure C#: compiled and RUN headless by Tools/Build/ecology_lod_harness (group "stomach").
//
// Fauna.starvationSeconds measured time since the last feed - a timer standing in for an energy budget. This is the
// budget itself: metabolism drains it continuously, feeding fills it with the volume actually eaten, and the creature
// starves when it reaches zero. It is the same prey-linked starvation (Docs/ECOSYSTEM.md §6 option C) but CONSERVED:
//   - what metabolism burns, and what a full stomach cannot hold, is RETURNED (the caller pays it to the cell's soil);
//   - the body still goes to the skeleton through the sealed wither;
//   - a predator that eats a creature takes its stomach (SurrenderAll) plus its body.
// And it is what lets a macro cohort summarise many creatures exactly (Σstomach, Σstomach² are additive).
//
// It is evaluated LAZILY (level at a timestamp + a constant drain), so a creature pays nothing per frame: the level is
// read when a behaviour tick asks, and settled on a feed.
using System;

namespace CosmicShore.Gameplay
{
    public struct FaunaStomach
    {
        /// <summary>The most it holds (prism volume). A feed beyond it is returned as overflow.</summary>
        public float Capacity;
        /// <summary>Volume burned per second, always. 0 = this creature never starves (the old starvationSeconds 0).</summary>
        public float Metabolism;
        float _level, _at;

        /// <summary>
        /// The migration from the authored starvation clock: one stomach holds ONE nominal meal and burns it in exactly
        /// <paramref name="starvationSeconds"/>, so a creature born full - or that eats a meal of at least that size -
        /// starves exactly <paramref name="starvationSeconds"/> after it, as the clock did. Smaller meals buy
        /// proportionally less time (that is the conservation), larger ones overflow to the soil.
        /// <paramref name="starvationSeconds"/> &lt;= 0 keeps "never starves".
        /// </summary>
        public static FaunaStomach FromStarvationClock(float starvationSeconds, float mealVolume)
        {
            float cap = Math.Max(1e-3f, mealVolume);
            return new FaunaStomach { Capacity = cap, Metabolism = starvationSeconds > 0f ? cap / starvationSeconds : 0f };
        }

        /// <summary>Starts full at time <paramref name="now"/> (a creature is born fed - the clock started at birth).
        /// Returns the volume the stomach now holds (the caller books it as moved into the creature).</summary>
        public float Fill(float now)
        {
            _level = Capacity; _at = now;
            return _level;
        }

        public float Level(float now) => Math.Max(0f, _level - Metabolism * Math.Max(0f, now - _at));

        /// <summary>True once metabolism has burned the stomach to zero (never, with Metabolism 0).</summary>
        public bool IsEmpty(float now) => Metabolism > 0f && _level - Metabolism * Math.Max(0f, now - _at) <= 0f;

        /// <summary>Seconds of upkeep left (∞ when it never starves).</summary>
        public float SecondsLeft(float now) => Metabolism > 0f ? Level(now) / Metabolism : float.PositiveInfinity;

        /// <summary>Brings the level to <paramref name="now"/>; returns what metabolism burned since the last settle
        /// (pay it to the soil). Exact: burned + level(now) == level(before).</summary>
        public float Settle(float now)
        {
            float lvl = Level(now), burned = _level - lvl;
            _level = lvl; _at = Math.Max(_at, now);
            return burned;
        }

        /// <summary>A meal of <paramref name="volume"/> lands. Returns what goes back to the soil: the upkeep burned
        /// since the last settle plus whatever a full stomach could not hold. Exact:
        /// level(before) + volume == level(after) + returned.</summary>
        public float Feed(float volume, float now)
        {
            float back = Settle(now);
            if (!(volume > 0f)) return back;
            float room = Math.Max(0f, Capacity - _level), take = Math.Min(room, volume);
            _level += take;
            return back + (volume - take);
        }

        /// <summary>A predator takes everything: returns the stomach's contents and the upkeep burned since the last
        /// settle (out). The stomach is empty afterwards.</summary>
        public float SurrenderAll(float now, out float burned)
        {
            burned = Settle(now);
            float all = _level;
            _level = 0f;
            return all;
        }
    }
}
