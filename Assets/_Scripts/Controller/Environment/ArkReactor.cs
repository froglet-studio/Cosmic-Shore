using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Ark's reactor charge (<see cref="CosmicShore.ScriptableObjects.ArkReactorSO"/>): the omni power aboard, counted
    /// in hull prisms it can still regrow. Pure bookkeeping, so the rules are testable without a scene: a harvest adds one
    /// crystal's worth only when all of it fits, a regrowth spends one prism's worth, and the slot that regrows next is
    /// the empty one nearest the reactor.
    /// </summary>
    public sealed class ArkReactor
    {
        public ArkReactor(int prismsPerCrystal, float capacityCrystals, float startingCrystals)
        {
            PrismsPerCrystal = Mathf.Max(1, prismsPerCrystal);
            Capacity = Mathf.Max(1f, capacityCrystals) * PrismsPerCrystal;
            Charge = Mathf.Clamp(startingCrystals * PrismsPerCrystal, 0f, Capacity);
        }

        /// <summary>Prisms' worth of power aboard.</summary>
        public float Charge { get; private set; }

        /// <summary>Prisms' worth the reactor holds.</summary>
        public float Capacity { get; }

        public int PrismsPerCrystal { get; }

        /// <summary>How full the reactor is, 0..1: what the crystal in the well shows.</summary>
        public float Fill01 => Capacity > 0f ? Mathf.Clamp01(Charge / Capacity) : 0f;

        /// <summary>A whole crystal fits. The Ark never harvests a crystal it would waste part of.</summary>
        public bool HasRoomForCrystal => Charge + PrismsPerCrystal <= Capacity + 1e-3f;

        /// <summary>At least one prism's worth aboard.</summary>
        public bool CanRegrow => Charge >= 1f - 1e-4f;

        /// <summary>Take in one omni crystal's power. False (nothing changes) when it does not fit.</summary>
        public bool TryHarvest()
        {
            if (!HasRoomForCrystal) return false;
            Charge = Mathf.Min(Capacity, Charge + PrismsPerCrystal);
            return true;
        }

        /// <summary>Spend one prism's worth on a regrowth. False when the reactor is spent.</summary>
        public bool TrySpend()
        {
            if (!CanRegrow) return false;
            Charge = Mathf.Max(0f, Charge - 1f);
            return true;
        }

        /// <summary>
        /// The empty slot nearest <paramref name="reactor"/> (the regrowth spreads out from the reactor well), or -1 when
        /// none is empty. <paramref name="positions"/> and <paramref name="empty"/> are parallel, in one space.
        /// </summary>
        public static int NearestEmpty(IReadOnlyList<Vector3> positions, IReadOnlyList<bool> empty, Vector3 reactor)
        {
            int best = -1;
            float bestSqr = float.PositiveInfinity;
            int n = Mathf.Min(positions.Count, empty.Count);
            for (int i = 0; i < n; i++)
            {
                if (!empty[i]) continue;
                float d = (positions[i] - reactor).sqrMagnitude;
                if (d < bestSqr)
                {
                    bestSqr = d;
                    best = i;
                }
            }
            return best;
        }
    }
}
