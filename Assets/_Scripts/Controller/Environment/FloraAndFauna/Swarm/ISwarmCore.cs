// What the swarm's Unity glue (SwarmFauna) needs from a SIMULATION CORE. Four cores implement it:
//   SwarmFieldCore - the research's `field` model (designed attractor fields, greedy slot assignment);
//   SwarmGridCore  - the research's `hgrid2` model (a coarse class-deficit grid + a fine per-class
//                    morphogen; nothing assigns a tadpole a place);
//   SwarmSortCore  - the research's `sort` model (emergent cell sorting: positional-information
//                    wells, fate commitment, differential adhesion, a composition homeostat, molting);
//   SwarmEvoFateCore - the research's `evofate` model (the evolved G2 network moves every tadpole; a
//                    designed fate pull sorts them; sort's composition).
// A swarm picks one through SwarmFaunaConfigSO.Model, so a cell can host all four side by side
// (Docs/SWARM_FAUNA.md §8, §9, §11). Like the cores, this file is free of UnityEngine: it compiles and runs
// headless in Tools/Build/swarm_core_harness.
using System;
using System.Collections.Generic;
using System.Numerics;

namespace CosmicShore.Gameplay
{
    /// <summary>Which simulation drives a swarm. Explicit values: the enum is serialized.</summary>
    public enum SwarmModel
    {
        /// <summary>Designed attractor fields + boids (research `field`): crisp, every tadpole owns a slot.</summary>
        Field = 0,
        /// <summary>The grid morphogen (research `hgrid2`): every tadpole reads only fields at its own
        /// position - a coarse class deficit and a fine per-class morphogen. Looser, more organic.</summary>
        Grid = 1,
        /// <summary>Emergent cell sorting (research `sort`): each tadpole commits to one positional-
        /// information well of its type and climbs it; unlike types repel harder than like types; a
        /// surplus member MOLTS into a deficit element. Lossless by design - nothing dies on a clock.</summary>
        Sort = 2,
        /// <summary>The EVOLVED rule given a fate (research `evofate`): a learned network (the trained G2 rule
        /// + the evo genome) moves every tadpole, and a small designed pull - only outside a dead zone, only on
        /// the steps the network fires - steers each toward the one positional-information well it committed
        /// to. Sort's composition (homeostat + molting). The most "alive" texture of the four, and the dearest.</summary>
        EvoFate = 3,
    }

    /// <summary>The per-swarm simulation the glue drives. Arrays are struct-of-arrays over the core's
    /// fixed capacity, indexed by member slot; a member is live while <see cref="Alive"/> is true.</summary>
    public interface ISwarmCore
    {
        int Cap { get; }
        Vector3[] Pos { get; }
        Vector3[] Vel { get; }
        Vector3[] Facing { get; }
        int[] Elem { get; }
        bool[] Alive { get; }
        float[] Startle { get; }
        /// <summary>Molt progress (0 = not molting). Always 0 in a core that never molts.</summary>
        float[] Molt { get; }
        int[] MoltTo { get; }
        /// <summary>Eaten volume banked per research element (0 Charge .. 3 Time) - what pays for eggs.</summary>
        float[] Stomach { get; }
        /// <summary>Round 8 (Docs/SWARM_FAUNA.md §16.4): the same banked volume split by the DOMAIN slot (0..2) of the
        /// mass that was eaten, laid out [element * 3 + slot]. Only a core that funds eggs by food domain spends it.</summary>
        float[] StomachDom { get; }
        /// <summary>Each member's domain SLOT (0..2; 0 = the swarm's controlling domain). All 0 in a one-colour swarm.</summary>
        int[] Dom { get; }
        List<SwarmEvent> Events { get; }

        int Clock { get; }
        int PlanIx { get; }
        SwarmPlanData Plan { get; }
        int AliveCount { get; }
        Vector3 Anchor { get; }
        Vector3 BX { get; }
        Vector3 BY { get; }
        Vector3 BZ { get; }
        Vector3 SwimTarget { get; set; }

        void Seed(int planElement, int count, Vector3 anchor, Vector3 heading);
        void Step(ReadOnlySpan<SwarmPredator> preds);
        void Kill(int i);
        int EffectiveElement(int i);
        int[] Counts(bool eff);

        /// <summary>
        /// The prism member <paramref name="i"/> should wear as element <paramref name="element"/>:
        /// half-extents [long, wide, thin] in sim units and its Charge tier (0 plain, 1 danger, 2 shield).
        /// False when the core has no specific look for it (the glue then uses a typical prism).
        /// </summary>
        bool TryGetLook(int i, int element, out Vector3 half, out int tier);

        /// <summary>
        /// Starvation has come (the HOST decided: the swarm went unfed for StarvationSeconds) - which
        /// member does it shed? -1 when there is nobody to shed. A core never kills on its own clock.
        /// </summary>
        int StarvationVictim();
    }

    /// <summary>
    /// Rules every core shares, written once: the vessel reaction (SwarmFieldCore's predator layer,
    /// reused by the grid and sort cores) and funded laying (an egg costs eaten volume). The field core
    /// still inlines its own copy of both - it was first, and its behaviour is pinned by its tests.
    /// </summary>
    public static class SwarmCoreShared
    {
        /// <summary>
        /// The distinct buckets of the 27 cells around (cx, cy, cz) in a spatial hash of <paramref name="mod"/>
        /// buckets (the cores' shared formula). Two neighbouring cells can hash to ONE bucket (measured: 0.5% of
        /// query cells near the origin), and a loop over the 27 cells then visits that bucket twice and counts its
        /// members twice - a neighbour's weight doubled in perception, separation or collision. Returns the count
        /// written to <paramref name="buckets"/> (at least 27 long).
        /// </summary>
        public static int NeighbourBuckets(int cx, int cy, int cz, int mod, int[] buckets)
        {
            int n = 0;
            for (int dx = -1; dx <= 1; dx++) for (int dy = -1; dy <= 1; dy++) for (int dz = -1; dz <= 1; dz++)
            {
                int g = (int)(((uint)((cx + dx) * 73856093) ^ (uint)((cy + dy) * 19349663) ^ (uint)((cz + dz) * 83492791)) % (uint)mod);
                bool seen = false;
                for (int k = 0; k < n && !seen; k++) seen = buckets[k] == g;
                if (!seen) buckets[n++] = g;
            }
            return n;
        }

        /// <summary>
        /// One vessel's pull on a member at <paramref name="x"/> of a given element: a startle from a
        /// vessel ahead or near (raises <paramref name="st"/>), the per-element flee (lateral out of its
        /// path, a little radial, a swirl), and - for an element that mobs - an orbit around a LOITERING
        /// ship instead of fleeing it.
        /// </summary>
        public static Vector3 FleeFrom(in SwarmPredator pr, Vector3 x, float fleeK, float mobK, float senseK,
                                       float lookahead, float mobSpeed, float fleeSwirl, ref float st)
        {
            var rel = x - pr.C; float dd = rel.Length(), sense = senseK * pr.R, spd = MathF.Max(pr.V.Length(), 1e-6f);
            var pvn = pr.V / spd; float along = Vector3.Dot(rel, pvn); var lat = rel - along * pvn; float dl = lat.Length();
            var latn = lat / MathF.Max(dl, 1e-3f);
            float ahead = along > -pr.R ? Math.Clamp(1 - along / (lookahead * spd + pr.R), 0, 1) : 0;
            float w = MathF.Max(Math.Clamp(1 - dl / sense, 0, 1) * ahead, Math.Clamp(1 - dd / sense, 0, 1));
            st = MathF.Max(st, Math.Clamp(1.4f * w, 0, 1));
            var radial = rel / MathF.Max(dd, 1e-3f);
            float fk = fleeK;
            var flee = Vector3.Zero;
            if (spd < mobSpeed && mobK > 0f)
            {
                float wMob = Math.Clamp(1 - dd / (2.5f * sense), 0, 1) * mobK;
                var tang = Vector3.Cross(Vector3.UnitY, radial); float tl = tang.Length();
                tang = tl > 1e-3f ? tang / tl : Vector3.UnitX;
                flee += wMob * (0.4f * (1.4f * pr.R - dd) * radial + 1.5f * tang);
                fk *= 1 - mobK;
            }
            flee += w * fk * (0.75f * latn + 0.25f * radial + fleeSwirl * 0.5f * Vector3.Cross(pvn, latn));
            return flee;
        }

        /// <summary>
        /// Pays for one egg of element <paramref name="e"/> out of <paramref name="stomach"/>: its own
        /// element's reserve at <paramref name="cost"/>, else the other reserves at
        /// <paramref name="crossCost"/> times that (largest first). Spends nothing when it fails.
        /// </summary>
        public static bool TryFund(float[] stomach, int e, float cost, float crossCost)
        {
            if (stomach[e] >= cost) { stomach[e] -= cost; return true; }
            float cross = cost * crossCost, others = 0;
            for (int o = 0; o < 4; o++) if (o != e) others += stomach[o];
            if (others < cross) return false;
            while (cross > 1e-6f)
            {
                int best = -1; for (int o = 0; o < 4; o++) if (o != e && (best < 0 || stomach[o] > stomach[best])) best = o;
                float take = MathF.Min(stomach[best], cross); stomach[best] -= take; cross -= take;
                if (take <= 0f) break;
            }
            return true;
        }
    }
}
