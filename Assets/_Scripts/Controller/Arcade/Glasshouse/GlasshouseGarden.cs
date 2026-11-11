using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Glasshouse's garden arithmetic, kept pure so the edit-mode tests can hold it without a scene.
    ///
    /// <para><b>A domain's GARDEN is the plants standing in its colour outside the nucleus.</b> An Angler's seed is
    /// planted in the pilot's domain (<c>AnglerCommandExecutor.PlantSeeds</c>, <c>domainOverride</c>), and a plant's
    /// offspring inherit its domain (<c>Flora.SpawnOffspring</c>), so every plant of a colour in this arena was grown
    /// from that colour's seeds: the cell plants nothing wild (the mode's flora configs have SpawnProbability 0). A
    /// plant's volume is its live prisms times its leaf (<see cref="Flora.LeafVolume"/>): the species Glasshouse grows
    /// is a branching flora, which lays every prism at its leaf size, so the product is exact. Plants inside the
    /// nucleus are left out - the nucleus is a sanctuary no grazer may touch, so mass there could never be taken.</para>
    ///
    /// <para><b>The score composes with the platform's own count.</b> <c>StatsManager</c> already moves a pilot's
    /// VolumeRemaining by every prism of their TRAIL laid, eaten or stolen. The controller adds the garden on top by
    /// DELTAS (<see cref="ShareBook"/>): each sample moves a pilot's stat by what their share of the garden gained or
    /// lost since the last sample, so the two producers never overwrite each other and the stat is always
    /// "trail standing + garden standing".</para>
    /// </summary>
    public static class GlasshouseGarden
    {
        public const int DomainSlots = 3;

        /// <summary>Jade 0, Ruby 1, Gold 2; -1 for Blue (no team) and anything else.</summary>
        public static int Slot(Domains domain) => domain switch
        {
            Domains.Jade => 0,
            Domains.Ruby => 1,
            Domains.Gold => 2,
            _ => -1,
        };

        /// <summary>One plant's standing volume: its live prisms times its leaf. Never negative.</summary>
        public static float PlantVolume(int prisms, float leafVolume) =>
            prisms > 0 && leafVolume > 0f ? prisms * leafVolume : 0f;

        /// <summary>A pilot's share of their domain's garden: split evenly between the domain's pilots.</summary>
        public static float Share(float domainGarden, int pilotsInDomain) =>
            pilotsInDomain > 0 && domainGarden > 0f ? domainGarden / pilotsInDomain : 0f;

        /// <summary>
        /// Sums every live plant's volume into <paramref name="byDomain"/> (length <see cref="DomainSlots"/>, cleared
        /// first). A dying plant, a plant in no playable domain, and a plant whose heart is inside
        /// <paramref name="cell"/>'s nucleus count nothing.
        /// </summary>
        public static void Measure(IReadOnlyList<Flora> live, Cell cell, float[] byDomain)
        {
            for (int i = 0; i < byDomain.Length; i++) byDomain[i] = 0f;
            if (live == null) return;
            for (int i = 0; i < live.Count; i++)
            {
                var flora = live[i];
                if (!flora || flora.IsDying) continue;
                int slot = Slot(flora.Domain);
                if (slot < 0 || slot >= byDomain.Length) continue;
                if (cell && cell.HasNucleusControlZone && cell.IsInsideNucleus(flora.HeartTransform.position)) continue;
                byDomain[slot] += PlantVolume(flora.HealthBlockCount, flora.LeafVolume);
            }
        }

        /// <summary>
        /// What each pilot has already been credited from the garden, so the next sample moves their stat by the
        /// DIFFERENCE only. Keyed by pilot name (the RoundStats key every peer shares).
        /// </summary>
        public sealed class ShareBook
        {
            readonly Dictionary<string, float> _credited = new();

            /// <summary>The garden volume <paramref name="pilot"/> has been credited so far.</summary>
            public float Credited(string pilot) =>
                pilot != null && _credited.TryGetValue(pilot, out var v) ? v : 0f;

            /// <summary>Records <paramref name="share"/> as the pilot's garden and returns how far their stat must
            /// move to match it (positive: it grew; negative: it was eaten or withered).</summary>
            public float Settle(string pilot, float share)
            {
                if (pilot == null) return 0f;
                share = Mathf.Max(0f, share);
                float delta = share - Credited(pilot);
                _credited[pilot] = share;
                return delta;
            }

            /// <summary>Forgets every credit (a new round: the stats were zeroed too).</summary>
            public void Clear() => _credited.Clear();
        }
    }
}
