// Builders and thieves (Docs/BUILDERS_AND_THIEVES.md, research Tools/Ecology/builders + bestiary/species/thief.py on the
// research branch): the SHARED substrate of every colony that STEALS prisms and does something with them.
//
// Pure C# over System.Numerics - no UnityEngine - so the same files compile and RUN headless
// (Tools/Build/builders_harness) and the Unity glue (BuilderColonyFauna) converts at the boundary. The round-7 swarm
// shape (SwarmSortCore): struct-of-arrays workers, a fixed tick, no GameObject per worker.
//
// A colony never creates a prism. Every brick it lays and every prism a thief hoards already existed (a pilot's trail
// prism, a skeleton, loose cell mass) with its own collider, spatial-index entry and render entity. A colony changes
// three things on an existing prism: its DOMAIN (the steal), its POSITION (only while carried) and its OWNER registry
// (BuilderRegistry). Net collider impact of the structures: 0.
using System;
using System.Collections.Generic;
using System.Numerics;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The world a colony acts on, addressed by integer mass HANDLES. In the game a handle is a slot in the glue's
    /// prism table (BuilderPrismWorld over PrismSpatialIndex); in the harness it is an index into the arena's arrays.
    /// Every mutation here is one of the platform's existing verbs - the core never invents a new one.
    /// </summary>
    public interface IBuilderWorld
    {
        /// <summary>Every LIVE prism within <paramref name="radius"/> (PrismSpatialIndex.QuerySphere). Cleared first.</summary>
        int QuerySphere(Vector3 centre, float radius, List<int> results);
        bool Alive(int h);
        Vector3 Position(int h);
        /// <summary>The prism's domain as an int (Domains); colonies compare it, never interpret it.</summary>
        int Domain(int h);
        /// <summary>Shielded OR super-shielded (Fauna.IsShieldedMass): never a target, never food.</summary>
        bool Shielded(int h);
        /// <summary>Laid by a vessel's trail (player-derived mass).</summary>
        bool IsTrail(int h);
        /// <summary>Seconds since the prism was created (prismProperties.TimeCreated).</summary>
        float Age(int h);
        float Volume(int h);
        /// <summary>
        /// World-side half of IsStealableForMe: loose mass only - not a living lifeform's tissue, not a structure ANY
        /// colony built (BuilderRegistry), not carried by anyone.
        /// </summary>
        bool Loose(int h);
        /// <summary>Prism.Steal(colonyName, domain, superSteal:false). True when the prism is now in <paramref name="domain"/>;
        /// the world remembers who it was taken from (for <see cref="GiveBack"/>).</summary>
        bool Steal(int h, int domain);
        /// <summary>A carrier moved its prism: transform write + Prism.NotifyPositionChanged (the mover contract).</summary>
        void Carry(int h, Vector3 position);
        /// <summary>A carrier let go of its prism without placing it (it died, or lost it): the prism falls loose where it
        /// is, in whatever domain it now has - nothing pops, mass conserved.</summary>
        void Drop(int h);
        /// <summary>PrismSpatialIndex.TryReserve - claim-before-place.</summary>
        bool TryReserve(Vector3 site, float clearRadius);
        /// <summary>Deposit: the FINAL pose is written now (collider, index, volume final at once) and the short move from
        /// <paramref name="from"/> is a clock-stamped flight of <paramref name="seconds"/> (animation, not live data).</summary>
        void Settle(int h, Vector3 from, Vector3 site, float seconds);
        /// <summary>Mark / unmark a prism as part of a structure (BuilderRegistry, keyed by colony and SITE) - built mass is
        /// never anyone's loot. A fortress passes its lattice site index; a hoard passes -(handle + 1).</summary>
        void SetBuilt(int h, int colony, int site, bool built);
        /// <summary>Eaten: the prism leaves the world through the food web (Prism.Consume). Returns its volume.</summary>
        float Consume(int h, Vector3 mouth);
        /// <summary>Recapture: the prism changes hands back to whoever it was stolen from.</summary>
        void GiveBack(int h);
        /// <summary>A raiding vessel takes a hoarded prism (it changes hands to the vessel).</summary>
        void Reclaim(int h, int vesselId);
    }

    /// <summary>A vessel as a colony senses it (fed by the glue each tick, nearest first is not required).</summary>
    public struct BuilderVessel
    {
        public Vector3 Pos, Vel;
        public float Radius;
        public int Id;
        public int Domain;
        /// <summary>True when contact kills a worker (every vessel in the game; the harness's scripted cutter only during
        /// its passes, as in the research).</summary>
        public bool Rams;
    }

    /// <summary>A worker / thief died this tick: <see cref="Vessel"/> is the vessel that rammed it, <see cref="StarvedBy"/>
    /// for an empty stomach, or <see cref="PlatformKill"/> when the platform killed its proxy (a weapon, a joust, a predator).</summary>
    public struct BuilderDeath
    {
        public const int StarvedBy = -1, PlatformKill = -2;
        public int Agent;
        public int Vessel;
        public Vector3 At;
        /// <summary>The member's stomach at the moment it died (volume eaten and not yet spent). Recorded here because a
        /// birth in the SAME step can re-use the slot and overwrite <c>Stomach[Agent]</c> - a ledger that read the array
        /// after the step lost that volume (QA-SWARM-ROUND11-9, the showcase cell's builder residual).</summary>
        public float Stomach;
    }

    /// <summary>xorshift128+ with a cached Box-Muller normal. Deterministic per seed; no System.Random allocation churn.</summary>
    public sealed class BuilderRng
    {
        ulong _a, _b;
        bool _has;
        float _spare;

        public BuilderRng(int seed)
        {
            ulong s = (ulong)(uint)seed * 0x9E3779B97F4A7C15UL + 0x1234567UL;
            _a = SplitMix(ref s); _b = SplitMix(ref s);
            if (_a == 0 && _b == 0) _b = 1;
        }

        static ulong SplitMix(ref ulong x)
        {
            ulong z = (x += 0x9E3779B97F4A7C15UL);
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        ulong Next()
        {
            ulong s1 = _a, s0 = _b;
            _a = s0;
            s1 ^= s1 << 23;
            _b = s1 ^ s0 ^ (s1 >> 17) ^ (s0 >> 26);
            return _b + s0;
        }

        /// <summary>[0, 1)</summary>
        public float Uniform() => (Next() >> 40) * (1f / (1UL << 24));

        public int Range(int n) => (int)((Next() >> 33) % (ulong)Math.Max(1, n));

        public float Normal()
        {
            if (_has) { _has = false; return _spare; }
            float u, v, s;
            do
            {
                u = 2f * Uniform() - 1f;
                v = 2f * Uniform() - 1f;
                s = u * u + v * v;
            } while (s >= 1f || s == 0f);
            float m = MathF.Sqrt(-2f * MathF.Log(s) / s);
            _spare = v * m; _has = true;
            return u * m;
        }

        public Vector3 Normal3(float sigma = 1f) => new(Normal() * sigma, Normal() * sigma, Normal() * sigma);

        public Vector3 OnSphere()
        {
            var d = Normal3();
            float l = d.Length();
            return l > 1e-6f ? d / l : Vector3.UnitX;
        }
    }

    /// <summary>
    /// A colony member's STOMACH (the ecology's eat/starve law, Docs/claude/ECOSYSTEM_DESIGN_PRINCIPLES.md): a worker
    /// eats loose prisms through the food web's one down-force (Consume), metabolises what it ate, and dies of
    /// starvation only when the stomach is EMPTY - never on a clock. A birth is paid out of the parent's stomach.
    /// </summary>
    public sealed class BuilderStomachParams
    {
        /// <summary>Volume a stomach holds before the member stops eating.</summary>
        public float Capacity = 40f;
        /// <summary>Fill a spawned FOUNDER starts with (the spawner's endowment, like every fauna spawn); each founder
        /// draws uniformly in [FounderFill, 1] x Capacity so a colony's hunger never arrives in one synchronised wave.</summary>
        public float FounderFill = 0.6f;
        /// <summary>Volume per second burned while active.</summary>
        public float Metabolism = 0.02f;
        /// <summary>Volume per second burned while roosting (torpor) - what carries a colony through an empty opening.</summary>
        public float Torpor = 0.02f;
        /// <summary>Below this fraction of Capacity the member eats instead of working.</summary>
        public float HungryBelow = 0.35f;
        /// <summary>Above this fraction a member may breed (colony under its cap).</summary>
        public float BirthAbove = 0.9f;
        /// <summary>Volume a birth costs the parent: the newborn's body and its starting stomach (half of it).</summary>
        public float BirthCost = 16f;
    }

    /// <summary>Shared vector helpers (research bestiary/core.py: unit, clamp_len, steer).</summary>
    /// <summary>
    /// Round 11f-2 (Docs/ECOLOGY_LOD.md §6.2): a colony COLLAPSED far from every pilot ROOSTS - its members hold still and
    /// each burns its stomach at the torpor rate, the rule the cores already apply to a roosting member. A roost never kills:
    /// the owner expands the colony before its emptiest stomach runs out, so starvation is always an individual's death
    /// (through <c>Kill(k, StarvedBy)</c> and a proxy's crystal). Shared by the three builder cores.
    /// </summary>
    public static class BuilderRoost
    {
        /// <summary>Burns <paramref name="rate"/> x <paramref name="dt"/> from every living member's stomach (never below 0,
        /// never a death). Returns the volume burned (the core adds it to its Metabolised ledger).</summary>
        public static float Burn(bool[] alive, float[] stomach, int cap, float rate, float dt, RoostBug bug = RoostBug.None)
        {
            if (bug == RoostBug.NoBurn) return 0f;
            float burned = 0f;
            for (int k = 0; k < cap; k++)
            {
                if (!alive[k]) continue;
                float b = MathF.Min(stomach[k], rate * dt);
                stomach[k] -= b; burned += b;
                if (bug == RoostBug.KillOnEmpty && stomach[k] <= 0f) alive[k] = false;
            }
            return burned;
        }

        /// <summary>Seconds until the emptiest living stomach is empty at <paramref name="rate"/> (infinity at rate 0).</summary>
        public static float SecondsLeft(bool[] alive, float[] stomach, int cap, float rate)
        {
            if (!(rate > 0f)) return float.PositiveInfinity;
            float left = float.PositiveInfinity;
            for (int k = 0; k < cap; k++) if (alive[k]) left = MathF.Min(left, stomach[k] / rate);
            return left;
        }
    }

    /// <summary>Planted bugs for the builders LOD gate's negative controls (builders_harness group lod).</summary>
    public enum RoostBug
    {
        None = 0,
        /// <summary>A roosting colony burns nothing (collapsing is immortality: breaks "one rule set").</summary>
        NoBurn = 1,
        /// <summary>A roost kills an emptied member in the macro (an imposed death with no body and no crystal).</summary>
        KillOnEmpty = 2,
    }

    public static class BuilderMath
    {
        public static Vector3 Unit(Vector3 v)
        {
            float l = v.Length();
            return l > 1e-9f ? v / l : Vector3.Zero;
        }

        public static Vector3 ClampLength(Vector3 v, float m)
        {
            float l = v.Length();
            return l > m && l > 1e-9f ? v * (m / l) : v;
        }

        /// <summary>Move <paramref name="vel"/> toward <paramref name="desired"/> with a bounded acceleration.</summary>
        public static Vector3 SteerAccel(Vector3 vel, Vector3 desired, float accel, float dt) =>
            vel + ClampLength(desired - vel, accel * dt);
    }
}
