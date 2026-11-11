using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// "The Field" - Harvest's arena, the Thresher's SPACE game (<c>Assets/_Scripts/Controller/Arcade/HARVEST.md</c>).
    /// A field of SHEAVES floating in 0G round a clear centre, where the omni crystals spawn.
    ///
    ///   • <b>A sheaf</b> is a <b>core</b> - <see cref="coreBlocks"/> cubes on a sphere of <see cref="coreRadius"/>,
    ///     most of its mass - with <see cref="stalkCount"/> <b>stalks</b> standing out of it in every direction, each a
    ///     column of <see cref="stalkPrisms"/> thin prisms. The Thresher's chain cuts stalks (every cut banks energy in
    ///     the ball); the ball's next hit on a rival prism spends the bank as one blast. So a sheaf is the whole loop:
    ///     mow the rim with the chain, then whip the ball into the heart (THRESHER.md §2-2a).
    ///   • <b>Colour is the target choice</b> (Rampage's rule): a sheaf in your domain's colour scores nothing for you,
    ///     a neutral one (<see cref="Domains.Blue"/>) scores for everyone. <see cref="colourPattern"/> says which.
    ///   • <b>The centre is clear</b> (<see cref="centreClearingRadius"/>): no stalk reaches it, so the omni crystals
    ///     the cell's CrystalManager spawns there (its <c>noNucleusSpawnRadius</c>) never sit inside a sheaf.
    ///
    /// <para><b>0G.</b> Nothing has an up: each sheaf's frame is turned by a hashed axis, its stalks and blocks are a
    /// Fibonacci sphere in that frame.</para>
    ///
    /// <para><b>Mirrored exactly by <c>Tools/Build/harvest_budget.py</c></b>, which the generator imports for the
    /// census, the volume and the PhaseThresholds. Every draw is <see cref="CellEnvironmentSpawnableBase.Hash01"/> of
    /// an integer key, every per-unit count is a field or a const, and the only count-affecting rejection is
    /// deterministic geometry: a sheaf whose reach enters the centre clearing, leaves the field, or comes closer to
    /// another than <see cref="SheafSpacing"/> re-draws, at most <see cref="SheafTries"/> times, then is skipped (the
    /// generator fails if any is).</para>
    ///
    /// <para><b>Flush, never interpenetrating</b> (Docs/ECOSYSTEM.md §62): a stalk's root sits a whole block outside
    /// the core sphere, a stalk's prisms are laid a hair under their length (<see cref="Flush"/>), and the generator
    /// asserts the core blocks and the stalk roots keep their spacing.</para>
    ///
    /// <para><b>No intensity read.</b> The layout reads only the serialized fields below, one prefab per intensity,
    /// so each one bakes (<c>Tools/Build/bake_prism_layouts.py</c>). Authored by
    /// <c>Tools/Build/author_harvest_assets.py</c>.</para>
    /// </summary>
    public class SpawnableHarvest : CellEnvironmentSpawnableBase
    {
        [Header("The field")]
        [Tooltip("Radius (u) of the ball of space the sheaves fill, round the arena centre. Every stalk tip lies inside it.")]
        [SerializeField, Min(100f)] float fieldRadius = 880f;
        [Tooltip("Radius (u) of the clear ball at the centre that no stalk enters: the omni crystals spawn in it.")]
        [SerializeField, Min(0f)] float centreClearingRadius = 200f;
        [Tooltip("Sheaves in the field (a sheaf that cannot find room after its tries is skipped; the generator fails then).")]
        [SerializeField, Min(0)] int sheafCount = 12;
        [Tooltip("Air (u) between two sheaves' CORES, surface to surface, at the least. Larger than the Thresher's " +
                 "largest blast radius, so one blast never reaches two cores.")]
        [SerializeField, Min(0f)] float coreGap = 180f;

        [Header("A sheaf")]
        [Tooltip("Radius (u) of the sphere the core's blocks sit on.")]
        [SerializeField, Min(10f)] float coreRadius = 58f;
        [Tooltip("Cubes in a core, on a Fibonacci sphere.")]
        [SerializeField, Min(1)] int coreBlocks = 140;
        [Tooltip("Lay every core block SHIELDED: the first hit pops its shield, the next destroys it.")]
        [SerializeField] bool shieldedCores = true;
        [Tooltip("Stalks standing out of a core, on a Fibonacci sphere.")]
        [SerializeField, Min(0)] int stalkCount = 24;
        [Tooltip("Prisms in one stalk, root to tip (each Segment long).")]
        [SerializeField, Min(1)] int stalkPrisms = 6;
        [Tooltip("Every Nth stalk of a sheaf (stalk N-1, 2N-1, ...) is DANGER from root to tip. 0 = none.")]
        [SerializeField, Min(0)] int dangerStalkEveryNth = 0;

        [Header("Colour")]
        [Tooltip("0 = every sheaf neutral (scores for everyone). 1 = one sheaf in four wears a domain colour (Jade, " +
                 "Ruby, Gold in turn), the rest neutral. 2 = Jade, Ruby, Gold, neutral in turn.")]
        [SerializeField, Range(0, 2)] int colourPattern = 1;

        /// <summary>A core block's edge (u).</summary>
        public const float Block = 6f;
        /// <summary>A stalk prism's length and square section (u).</summary>
        public const float Segment = 20f, Section = 3f;
        /// <summary>Air (u) between the stalk tips of two neighbouring sheaves, at the least.</summary>
        public const float StalkAir = 20f;
        /// <summary>Draws a sheaf gets before its slot is given up.</summary>
        public const int SheafTries = 48;
        /// <summary>How close to exact contact a stalk prism is laid along its stalk.</summary>
        public const float Flush = 0.995f;
        /// <summary>Key offset into <see cref="CellEnvironmentSpawnableBase.Hash01"/> for the sheaves.</summary>
        public const int SheafKeyBase = 3_000_000;

        /// <summary>The colours <see cref="colourPattern"/> 1 and 2 hand out, in turn.</summary>
        static readonly Domains[] Playable = { Domains.Jade, Domains.Ruby, Domains.Gold };

        protected override int DefaultSeed => 8888;
        protected override int LayCapacity => 4096;

        protected override int BuildParameterHash() =>
            System.HashCode.Combine(
                System.HashCode.Combine(fieldRadius, centreClearingRadius, sheafCount, coreGap),
                System.HashCode.Combine(coreRadius, coreBlocks, shieldedCores, stalkCount, stalkPrisms),
                System.HashCode.Combine(dangerStalkEveryNth, colourPattern, 1));

        int LayoutSeed => seed != 0 ? seed : DefaultSeed;

        /// <summary>The radius (u) a stalk's root sits at: a whole block outside the core sphere.</summary>
        public float StalkRoot => coreRadius + Block;
        /// <summary>A sheaf's reach (u) from its centre to a stalk tip.</summary>
        public float Reach => StalkRoot + stalkPrisms * Segment;
        /// <summary>The least distance (u) between two sheaf centres.</summary>
        public float SheafSpacing => Mathf.Max(2f * Reach + StalkAir, 2f * coreRadius + coreGap);

        /// <summary>One sheaf, in this structure's local space.</summary>
        public readonly struct Sheaf
        {
            public readonly Vector3 Centre;
            public readonly float CoreRadius, Reach;
            public readonly Domains Domain;
            public Sheaf(Vector3 centre, float coreRadius, float reach, Domains domain)
            {
                Centre = centre; CoreRadius = coreRadius; Reach = reach; Domain = domain;
            }
        }

        readonly List<Sheaf> _placed = new();
        readonly List<Vector3> _axes = new();

        protected override void BuildEnvironment()
        {
            Place(_placed, _axes);
            for (int i = 0; i < _placed.Count; i++) LaySheaf(_placed[i], _axes[i]);
        }

        /// <summary>
        /// Every sheaf in this structure's local space (the arena centre is its origin), in slot order. Read by the
        /// AI to choose a sheaf to work. Pure: it reads only the serialized fields, so a baked layout answers it too.
        /// </summary>
        public void CollectSheaves(List<Sheaf> into)
        {
            var axes = new List<Vector3>();
            var sheaves = new List<Sheaf>();
            Place(sheaves, axes);
            into.AddRange(sheaves);
        }

        // ── Draws ──

        int Key(int slot, int attempt, int draw) =>
            unchecked(LayoutSeed * 7919 + SheafKeyBase + (slot * SheafTries + attempt) * 4 + draw);

        /// <summary>A direction uniform on the sphere from two hashes: no axis is up.</summary>
        static Vector3 UnitFrom(int key)
        {
            float u = 2f * Hash01(key) - 1f;
            float phi = 2f * Mathf.PI * Hash01(unchecked(key + 7919));
            float s = Mathf.Sqrt(Mathf.Max(0f, 1f - u * u));
            return new Vector3(s * Mathf.Cos(phi), s * Mathf.Sin(phi), u);
        }

        /// <summary>A point uniform in a ball of radius <paramref name="r"/> (cube root, so the ball fills evenly).</summary>
        static Vector3 InBall(int key, float r) =>
            UnitFrom(key) * (r * Mathf.Pow(Hash01(unchecked(key + 104729)), 1f / 3f));

        /// <summary>Point <paramref name="k"/> of <paramref name="n"/> on the unit Fibonacci sphere.</summary>
        public static Vector3 Fibonacci(int k, int n)
        {
            float y = 1f - 2f * (k + 0.5f) / n;
            float s = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            float a = GoldenAngle * k;
            return new Vector3(Mathf.Cos(a) * s, y, Mathf.Sin(a) * s);
        }

        /// <summary>The domain sheaf <paramref name="slot"/> wears under <see cref="colourPattern"/>.</summary>
        public static Domains DomainFor(int pattern, int slot) => pattern switch
        {
            1 => slot % 4 == 0 ? Playable[(slot / 4) % 3] : Domains.Blue,
            2 => slot % 4 == 3 ? Domains.Blue : Playable[slot % 4],
            _ => Domains.Blue,
        };

        // ── Room ──

        void Place(List<Sheaf> sheaves, List<Vector3> axes)
        {
            sheaves.Clear();
            axes.Clear();
            float reach = Reach, spacing = SheafSpacing;
            float minCentre = centreClearingRadius + reach;
            for (int slot = 0; slot < sheafCount; slot++)
            {
                for (int t = 0; t < SheafTries; t++)
                {
                    var centre = InBall(Key(slot, t, 0), fieldRadius - reach);
                    if (centre.magnitude < minCentre) continue;
                    bool room = true;
                    for (int i = 0; i < sheaves.Count; i++)
                        if ((sheaves[i].Centre - centre).magnitude < spacing) { room = false; break; }
                    if (!room) continue;
                    sheaves.Add(new Sheaf(centre, coreRadius, reach, DomainFor(colourPattern, slot)));
                    axes.Add(UnitFrom(Key(slot, t, 1)));
                    break;
                }
            }
        }

        // ── A sheaf ──

        void LaySheaf(Sheaf sheaf, Vector3 axis)
        {
            // The sheaf's own frame, turned by its hashed axis (Jackstraw's jack frame).
            var helper = Mathf.Abs(axis.z) < 0.9f ? Vector3.forward : Vector3.right;
            var y = Vector3.Cross(axis, helper).normalized;
            var z = Vector3.Cross(axis, y);

            var block = Vector3.one * Block;
            var coreKind = shieldedCores ? PrismKind.Shielded : PrismKind.Plain;
            for (int k = 0; k < coreBlocks; k++)
            {
                var d = InFrame(Fibonacci(k, coreBlocks), axis, y, z);
                Emit(sheaf.Centre + d * coreRadius, Facing(d), block, sheaf.Domain, coreKind);
            }

            var size = new Vector3(Section, Section, Segment * Flush);
            float root = StalkRoot;
            for (int k = 0; k < stalkCount; k++)
            {
                var d = InFrame(Fibonacci(k, stalkCount), axis, y, z);
                var kind = dangerStalkEveryNth > 0 && k % dangerStalkEveryNth == dangerStalkEveryNth - 1
                    ? PrismKind.Danger : PrismKind.Plain;
                var rot = Facing(d);
                for (int j = 0; j < stalkPrisms; j++)
                    Emit(sheaf.Centre + d * (root + Segment * (j + 0.5f)), rot, size, sheaf.Domain, kind);
            }
        }

        static Vector3 InFrame(Vector3 f, Vector3 x, Vector3 y, Vector3 z) => x * f.x + y * f.y + z * f.z;

        static Quaternion Facing(Vector3 axis) =>
            Quaternion.LookRotation(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right);
    }
}
