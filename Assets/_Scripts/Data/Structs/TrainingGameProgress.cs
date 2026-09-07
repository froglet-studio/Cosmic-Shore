using System;

namespace CosmicShore.Data
{
    /// <summary>
    /// Per-training-game tier progression: which of the four intensity tiers the player has
    /// SATISFIED (beaten) and which rewards they have CLAIMED, plus the highest intensity
    /// unlocked. Tier indices are 1-BASED throughout the API, matching the UI.
    ///
    /// <para>Persisted by <c>TrainingGameProgressSystem</c> through <c>DataAccessor</c>, which
    /// serializes with Newtonsoft BY PUBLIC PROPERTY NAME. <see cref="CurrentIntensity"/> and
    /// <see cref="Progress"/> are therefore the on-disk schema and must keep those names; the
    /// private backing fields are not serialized.</para>
    ///
    /// <para><b>Both accessors HEAL a value that was never properly constructed</b>, and that is
    /// load-bearing rather than defensive clutter. A struct in C# 9 cannot declare a parameterless
    /// constructor, so <c>new TrainingGameProgress()</c> ALWAYS zero-initialises - null tier array,
    /// intensity 0 - no matter what constructors exist. This type used to paper over that with
    /// <c>TrainingGameProgress(int dummy1 = 1, TrainingGameTier[] dummy2 = null)</c>, whose default
    /// arguments made <c>new TrainingGameProgress()</c> LOOK like it would run initialisation. It
    /// never did: C# binds that expression to the implicit default, not to an all-optional
    /// constructor. Every one of the 16 TrainingGameProgressTests failed on the null array.
    ///
    /// The same hole is reachable in production without any constructor at all: a save file whose
    /// <c>Progress</c> is absent, null, or the wrong length deserializes to exactly that state and
    /// then throws inside <c>ReportProgress</c>. Healing on read closes both doors at once, so
    /// there is no way to hold an instance of this type that indexes out of range.</para>
    /// </summary>
    [Serializable]
    public struct TrainingGameProgress
    {
        /// <summary>The four intensity tiers. 1-based at the API, 0-based in the array.</summary>
        public const int TierCount = 4;

        int _currentIntensity;
        TrainingGameTier[] _tiers;

        /// <summary>Highest intensity unlocked. Never below 1, however the value was created.</summary>
        public int CurrentIntensity
        {
            get => _currentIntensity < 1 ? 1 : _currentIntensity;
            set => _currentIntensity = value;
        }

        /// <summary>The tier flags, always exactly <see cref="TierCount"/> long.</summary>
        public TrainingGameTier[] Progress
        {
            get
            {
                if (_tiers == null || _tiers.Length != TierCount)
                    _tiers = Conform(_tiers);
                return _tiers;
            }
            set => _tiers = value;
        }

        /// <summary>
        /// Returns a correctly-sized tier array, carrying over whatever the caller had. A short
        /// array keeps the tiers it did hold rather than being discarded - a save written before a
        /// tier count change must not silently lose the player's progress.
        /// </summary>
        static TrainingGameTier[] Conform(TrainingGameTier[] existing)
        {
            var tiers = new TrainingGameTier[TierCount];
            int carry = existing == null ? 0 : Math.Min(existing.Length, TierCount);
            for (int i = 0; i < carry; i++)
                tiers[i] = existing[i];
            return tiers;
        }

        public void SatisfyTier(int tier)
        {
            Progress[tier - 1].Satisfied = true;
            CurrentIntensity = Math.Max(CurrentIntensity, tier);
        }

        public void ClaimTier(int tier)
        {
            Progress[tier - 1].Claimed = true;
        }

        public bool IsTierClaimed(int tier)
        {
            return Progress[tier - 1].Claimed;
        }

        public bool IsTierSatisfied(int tier)
        {
            return Progress[tier - 1].Satisfied;
        }
    }

    [Serializable]
    public struct TrainingGameTier
    {
        public bool Satisfied;
        public bool Claimed;
    }
}
