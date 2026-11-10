using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// "The Field" - Harvest's arena, the Thresher's SPACE game (<c>Assets/_Scripts/Controller/Arcade/HARVEST.md</c>).
    /// A field of SHEAVES floating in 0G round a clear centre, where the omni crystals spawn, with loose CHAFF drifting
    /// in clouds between them and solid BALES set among them.
    ///
    ///   • <b>A sheaf</b> is a <b>core</b> - <see cref="coreBlocks"/> cubes on a sphere of <see cref="coreRadius"/>,
    ///     most of its mass - with <see cref="stalkCount"/> <b>stalks</b>, each a column of <see cref="stalkPrisms"/>
    ///     thin prisms. With <see cref="wheelStalks"/> the stalks lie in ONE plane round the core, like the spokes of a
    ///     wheel (the plane is the sheaf's hashed axis turned flat), so a Thresher lapping the sheaf in that plane drags
    ///     its chain across every stalk and banks energy in the ball, then dives down the axle - where there is no
    ///     stalk - and spends the bank in the core as one blast. Measured offline with the shipped chain solver
    ///     (<c>Tools/Build/harvest_stalk_lab/</c>, Docs/MassAndSpaceGames/THRESHER_SPACE.md §10): stalks in every
    ///     direction feed the chain 0-11 cuts a lap; a wheel feeds it 38-92.
    ///   • <b>Chaff</b> - <see cref="chaffClouds"/> clouds of loose straws (one or two <see cref="Segment"/> prisms
    ///     each, always neutral) between the sheaves: fuel wherever a pilot flies, so the bank fills on the way.
    ///   • <b>Bales</b> - <see cref="baleCount"/> solid cubes of <see cref="baleSize"/>³ blocks: many prisms in one
    ///     place, the best thing to spend a full bank on.
    ///   • <b>Colour is the target choice</b> (Rampage's rule): a sheaf or bale in your domain's colour scores nothing
    ///     for you, a neutral one (<see cref="Domains.Blue"/>) scores for everyone. <see cref="colourPattern"/> says which.
    ///   • <b>The centre is clear</b> (<see cref="centreClearingRadius"/>): nothing reaches it, so the omni crystals
    ///     the cell's CrystalManager spawns there (its <c>noNucleusSpawnRadius</c>) never sit inside anything.
    ///
    /// <para><b>0G.</b> Nothing has an up: each sheaf and bale is turned by a hashed axis.</para>
    ///
    /// <para><b>Mirrored exactly by <c>Tools/Build/harvest_budget.py</c></b>, which the generator imports for the
    /// census, the volume and the PhaseThresholds. Every draw is <see cref="CellEnvironmentSpawnableBase.Hash01"/> of
    /// an integer key, every per-unit count is a field or a const, and the only count-affecting rejections are
    /// deterministic geometry (a piece that enters the clearing, leaves the field or comes too close to another
    /// re-draws, at most <see cref="Tries"/> times, then is skipped). The generator fails if a sheaf or a bale is
    /// skipped; a straw that finds no room in its cloud is simply not laid, and the mirror counts it the same way.</para>
    ///
    /// <para><b>Flush, never interpenetrating</b> (Docs/ECOSYSTEM.md §62): a stalk's root sits a whole block outside
    /// the core sphere, stalk and straw prisms are laid a hair under their length (<see cref="Flush"/>), straws keep
    /// <see cref="StrawGap"/> between their axes, bale blocks sit on a pitch wider than a block, and clouds and bales
    /// keep clear of every sheaf's reach.</para>
    ///
    /// <para><b>No intensity read.</b> The layout reads only the serialized fields below, one prefab per intensity,
    /// so each one bakes (<c>Tools/Build/bake_prism_layouts.py</c>). Authored by
    /// <c>Tools/Build/author_harvest_assets.py</c>.</para>
    /// </summary>
    public class SpawnableHarvest : CellEnvironmentSpawnableBase
    {
        [Header("The field")]
        [Tooltip("Radius (u) of the ball of space the field fills, round the arena centre. Everything lies inside it.")]
        [SerializeField, Min(100f)] float fieldRadius = 880f;
        [Tooltip("Radius (u) of the clear ball at the centre that nothing enters: the omni crystals spawn in it.")]
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
        [Tooltip("Stalks standing out of a core.")]
        [SerializeField, Min(0)] int stalkCount = 24;
        [Tooltip("Prisms in one stalk, root to tip (each Segment long).")]
        [SerializeField, Min(1)] int stalkPrisms = 6;
        [Tooltip("On: the stalks lie in ONE plane round the core, evenly spaced, like a wheel's spokes (the plane " +
                 "across the sheaf's hashed axis). Off: they stand out in every direction (a Fibonacci sphere).")]
        [SerializeField] bool wheelStalks = true;
        [Tooltip("Every Nth stalk of a sheaf (stalk N-1, 2N-1, ...) is DANGER from root to tip. 0 = none.")]
        [SerializeField, Min(0)] int dangerStalkEveryNth = 0;

        [Header("Chaff (loose straws between the sheaves, always neutral)")]
        [Tooltip("Clouds of loose straws. 0 = none.")]
        [SerializeField, Min(0)] int chaffClouds = 8;
        [Tooltip("Radius (u) of one cloud.")]
        [SerializeField, Min(20f)] float chaffCloudRadius = 110f;
        [Tooltip("Straws tried in each cloud (one that finds no room after its tries is not laid).")]
        [SerializeField, Min(0)] int strawsPerCloud = 24;

        [Header("Bales (solid stacks, the best thing to spend a full bank on)")]
        [Tooltip("Bales in the field (a bale that cannot find room is skipped; the generator fails then). 0 = none.")]
        [SerializeField, Min(0)] int baleCount = 4;
        [Tooltip("Blocks along one edge of a bale: a bale is size x size x size blocks.")]
        [SerializeField, Range(2, 5)] int baleSize = 3;

        [Header("Colour")]
        [Tooltip("0 = every sheaf and bale neutral (scores for everyone). 1 = one in four wears a domain colour " +
                 "(Jade, Ruby, Gold in turn), the rest neutral. 2 = Jade, Ruby, Gold, neutral in turn.")]
        [SerializeField, Range(0, 2)] int colourPattern = 1;

        /// <summary>A core or bale block's edge (u).</summary>
        public const float Block = 6f;
        /// <summary>Centre to centre (u) of neighbouring bale blocks: a block and a hair of air.</summary>
        public const float BalePitch = 6.5f;
        /// <summary>A stalk or straw prism's length and square section (u).</summary>
        public const float Segment = 20f, Section = 3f;
        /// <summary>Air (u) between the stalk tips of two neighbouring sheaves, at the least.</summary>
        public const float StalkAir = 20f;
        /// <summary>Air (u) a cloud or a bale keeps from any sheaf's reach, from each other and from the clearing.</summary>
        public const float PieceAir = 24f;
        /// <summary>Least distance (u) between two straw axes in a cloud: a section and a half of air.</summary>
        public const float StrawGap = 7.5f;
        /// <summary>Draws a piece gets before its slot is given up.</summary>
        public const int Tries = 48;
        /// <summary>How close to exact contact a stalk or straw prism is laid along its axis.</summary>
        public const float Flush = 0.995f;
        /// <summary>Key offsets into <see cref="CellEnvironmentSpawnableBase.Hash01"/>, one stream per family, so
        /// changing one count never moves another family's pieces.</summary>
        public const int SheafKeyBase = 3_000_000, CloudKeyBase = 4_000_000, StrawKeyBase = 5_000_000,
                         BaleKeyBase = 6_000_000;

        /// <summary>The colours <see cref="colourPattern"/> 1 and 2 hand out, in turn.</summary>
        static readonly Domains[] Playable = { Domains.Jade, Domains.Ruby, Domains.Gold };

        protected override int DefaultSeed => 8888;
        protected override int LayCapacity => 8192;

        protected override int BuildParameterHash() =>
            System.HashCode.Combine(
                System.HashCode.Combine(fieldRadius, centreClearingRadius, sheafCount, coreGap),
                System.HashCode.Combine(coreRadius, coreBlocks, shieldedCores, stalkCount, stalkPrisms, wheelStalks),
                System.HashCode.Combine(dangerStalkEveryNth, colourPattern, chaffClouds, chaffCloudRadius, strawsPerCloud),
                System.HashCode.Combine(baleCount, baleSize, 2));

        int LayoutSeed => seed != 0 ? seed : DefaultSeed;

        /// <summary>The radius (u) a stalk's root sits at: a whole block outside the core sphere.</summary>
        public float StalkRoot => coreRadius + Block;
        /// <summary>A sheaf's reach (u) from its centre to a stalk tip.</summary>
        public float Reach => StalkRoot + stalkPrisms * Segment;
        /// <summary>The least distance (u) between two sheaf centres.</summary>
        public float SheafSpacing => Mathf.Max(2f * Reach + StalkAir, 2f * coreRadius + coreGap);
        /// <summary>A bale's reach (u) from its centre: the half-diagonal of its cube of blocks.</summary>
        public float BaleReach => (0.5f * (baleSize - 1) * BalePitch + 0.5f * Block) * 1.7320508f;
        /// <summary>True when the stalks lie in one plane (see <see cref="wheelStalks"/>).</summary>
        public bool WheelStalks => wheelStalks;

        /// <summary>One sheaf, in this structure's local space. <see cref="Axis"/> is its wheel's axle (with
        /// <see cref="WheelStalks"/> the stalks lie in the plane across it).</summary>
        public readonly struct Sheaf
        {
            public readonly Vector3 Centre, Axis;
            public readonly float CoreRadius, Reach;
            public readonly Domains Domain;
            public Sheaf(Vector3 centre, Vector3 axis, float coreRadius, float reach, Domains domain)
            {
                Centre = centre; Axis = axis; CoreRadius = coreRadius; Reach = reach; Domain = domain;
            }
        }

        readonly List<Sheaf> _sheaves = new();
        readonly List<Vector3> _clouds = new();
        readonly List<Vector3> _bales = new();
        readonly List<Vector3> _baleAxes = new();
        readonly List<Vector3> _strawA = new(), _strawB = new();

        protected override void BuildEnvironment()
        {
            PlaceSheaves(_sheaves);
            PlaceClouds(_sheaves, _clouds);
            PlaceBales(_sheaves, _clouds, _bales, _baleAxes);
            for (int i = 0; i < _sheaves.Count; i++) LaySheaf(_sheaves[i]);
            for (int c = 0; c < _clouds.Count; c++) LayCloud(c, _clouds[c]);
            for (int b = 0; b < _bales.Count; b++) LayBale(_bales[b], _baleAxes[b], BaleDomainFor(colourPattern, b));
        }

        /// <summary>
        /// Every sheaf in this structure's local space (the arena centre is its origin), in slot order. Read by the
        /// AI to choose a sheaf to work. Pure: it reads only the serialized fields, so a baked layout answers it too.
        /// </summary>
        public void CollectSheaves(List<Sheaf> into)
        {
            var sheaves = new List<Sheaf>();
            PlaceSheaves(sheaves);
            into.AddRange(sheaves);
        }

        /// <summary>
        /// Points in this structure's local space where something may float clear of every sheaf, cloud and bale:
        /// <paramref name="count"/> draws of the stream <paramref name="stream"/>, each at least
        /// <paramref name="clearance"/> from every piece. The controller floats a team's Space crystals at them (I3).
        /// Pure and deterministic, so every peer answers the same points.
        /// </summary>
        public void CollectOpenPoints(int stream, int count, float clearance, List<Vector3> into)
        {
            var sheaves = new List<Sheaf>();
            var clouds = new List<Vector3>();
            var bales = new List<Vector3>();
            var axes = new List<Vector3>();
            PlaceSheaves(sheaves);
            PlaceClouds(sheaves, clouds);
            PlaceBales(sheaves, clouds, bales, axes);
            int start = into.Count;
            for (int slot = 0; slot < count; slot++)
            {
                for (int t = 0; t < Tries; t++)
                {
                    var p = InBall(Key(7_000_000 + stream * 10_000, slot, t, 0), fieldRadius - clearance);
                    if (p.magnitude < centreClearingRadius + clearance) continue;
                    if (!ClearOf(p, clearance, sheaves, clouds, bales)) continue;
                    bool apart = true;
                    for (int i = start; i < into.Count; i++)
                        if ((into[i] - p).magnitude < 2f * clearance) { apart = false; break; }
                    if (!apart) continue;
                    into.Add(p);
                    break;
                }
            }
        }

        bool ClearOf(Vector3 p, float r, List<Sheaf> sheaves, List<Vector3> clouds, List<Vector3> bales)
        {
            for (int i = 0; i < sheaves.Count; i++)
                if ((sheaves[i].Centre - p).magnitude < sheaves[i].Reach + r + PieceAir) return false;
            for (int i = 0; i < clouds.Count; i++)
                if ((clouds[i] - p).magnitude < chaffCloudRadius + r + PieceAir) return false;
            for (int i = 0; i < bales.Count; i++)
                if ((bales[i] - p).magnitude < BaleReach + r + PieceAir) return false;
            return true;
        }

        // ── Draws ──

        int Key(int family, int slot, int attempt, int draw) =>
            unchecked(LayoutSeed * 7919 + family + (slot * Tries + attempt) * 4 + draw);

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

        /// <summary>The domain piece <paramref name="slot"/> wears under <see cref="colourPattern"/>.</summary>
        public static Domains DomainFor(int pattern, int slot) => pattern switch
        {
            1 => slot % 4 == 0 ? Playable[(slot / 4) % 3] : Domains.Blue,
            2 => slot % 4 == 3 ? Domains.Blue : Playable[slot % 4],
            _ => Domains.Blue,
        };

        /// <summary>The domain bale <paramref name="slot"/> wears: neutral unless every piece is coloured in turn
        /// (pattern 2), so a handful of bales never hands one team more of its own colour than another.</summary>
        public static Domains BaleDomainFor(int pattern, int slot) => pattern == 2 ? DomainFor(2, slot) : Domains.Blue;

        // ── Room ──

        void PlaceSheaves(List<Sheaf> sheaves)
        {
            sheaves.Clear();
            float reach = Reach, spacing = SheafSpacing;
            float minCentre = centreClearingRadius + reach;
            for (int slot = 0; slot < sheafCount; slot++)
            {
                for (int t = 0; t < Tries; t++)
                {
                    var centre = InBall(Key(SheafKeyBase, slot, t, 0), fieldRadius - reach);
                    if (centre.magnitude < minCentre) continue;
                    bool room = true;
                    for (int i = 0; i < sheaves.Count; i++)
                        if ((sheaves[i].Centre - centre).magnitude < spacing) { room = false; break; }
                    if (!room) continue;
                    sheaves.Add(new Sheaf(centre, UnitFrom(Key(SheafKeyBase, slot, t, 1)), coreRadius, reach,
                                          DomainFor(colourPattern, slot)));
                    break;
                }
            }
        }

        void PlaceClouds(List<Sheaf> sheaves, List<Vector3> clouds)
        {
            clouds.Clear();
            float r = chaffCloudRadius;
            for (int slot = 0; slot < chaffClouds; slot++)
            {
                for (int t = 0; t < Tries; t++)
                {
                    var c = InBall(Key(CloudKeyBase, slot, t, 0), fieldRadius - r);
                    if (c.magnitude < centreClearingRadius + r + PieceAir) continue;
                    if (!ClearOf(c, r, sheaves, clouds, EmptyList)) continue;
                    clouds.Add(c);
                    break;
                }
            }
        }

        void PlaceBales(List<Sheaf> sheaves, List<Vector3> clouds, List<Vector3> bales, List<Vector3> axes)
        {
            bales.Clear();
            axes.Clear();
            float r = BaleReach;
            for (int slot = 0; slot < baleCount; slot++)
            {
                for (int t = 0; t < Tries; t++)
                {
                    var c = InBall(Key(BaleKeyBase, slot, t, 0), fieldRadius - r);
                    if (c.magnitude < centreClearingRadius + r + PieceAir) continue;
                    if (!ClearOf(c, r, sheaves, clouds, bales)) continue;
                    bales.Add(c);
                    axes.Add(UnitFrom(Key(BaleKeyBase, slot, t, 1)));
                    break;
                }
            }
        }

        static readonly List<Vector3> EmptyList = new();

        // ── The pieces ──

        static void Frame(Vector3 axis, out Vector3 x, out Vector3 y, out Vector3 z)
        {
            x = axis;
            var helper = Mathf.Abs(axis.z) < 0.9f ? Vector3.forward : Vector3.right;
            y = Vector3.Cross(axis, helper).normalized;
            z = Vector3.Cross(axis, y);
        }

        void LaySheaf(Sheaf sheaf)
        {
            Frame(sheaf.Axis, out var x, out var y, out var z);
            var block = Vector3.one * Block;
            var coreKind = shieldedCores ? PrismKind.Shielded : PrismKind.Plain;
            for (int k = 0; k < coreBlocks; k++)
            {
                var d = InFrame(Fibonacci(k, coreBlocks), x, y, z);
                Emit(sheaf.Centre + d * coreRadius, Facing(d), block, sheaf.Domain, coreKind);
            }

            var size = new Vector3(Section, Section, Segment * Flush);
            float root = StalkRoot;
            for (int k = 0; k < stalkCount; k++)
            {
                Vector3 d;
                if (wheelStalks)
                {
                    float a = 2f * Mathf.PI * k / stalkCount;
                    d = y * Mathf.Cos(a) + z * Mathf.Sin(a);
                }
                else d = InFrame(Fibonacci(k, stalkCount), x, y, z);
                var kind = dangerStalkEveryNth > 0 && k % dangerStalkEveryNth == dangerStalkEveryNth - 1
                    ? PrismKind.Danger : PrismKind.Plain;
                var rot = Facing(d);
                for (int j = 0; j < stalkPrisms; j++)
                    Emit(sheaf.Centre + d * (root + Segment * (j + 0.5f)), rot, size, sheaf.Domain, kind);
            }
        }

        void LayCloud(int cloud, Vector3 centre)
        {
            _strawA.Clear();
            _strawB.Clear();
            var size = new Vector3(Section, Section, Segment * Flush);
            float g2 = StrawGap * StrawGap;
            for (int s = 0; s < strawsPerCloud; s++)
            {
                int slot = cloud * strawsPerCloud + s;
                for (int t = 0; t < Tries; t++)
                {
                    int prisms = Hash01(Key(StrawKeyBase, slot, t, 2)) < 0.5f ? 1 : 2;
                    float half = prisms * Segment * 0.5f;
                    var mid = centre + InBall(Key(StrawKeyBase, slot, t, 0), chaffCloudRadius - half);
                    var axis = UnitFrom(Key(StrawKeyBase, slot, t, 1));
                    Vector3 a = mid - axis * half, b = mid + axis * half;
                    bool room = true;
                    for (int i = 0; i < _strawA.Count; i++)
                        if (SpawnableJackstraw.SegmentSegmentSqr(a, b, _strawA[i], _strawB[i]) < g2) { room = false; break; }
                    if (!room) continue;
                    _strawA.Add(a);
                    _strawB.Add(b);
                    var rot = Facing(axis);
                    for (int j = 0; j < prisms; j++)
                        Emit(a + axis * (Segment * (j + 0.5f)), rot, size, Domains.Blue, PrismKind.Plain);
                    break;
                }
            }
        }

        void LayBale(Vector3 centre, Vector3 axis, Domains domain)
        {
            Frame(axis, out var x, out var y, out var z);
            var rot = Quaternion.LookRotation(z, y);
            var block = Vector3.one * Block;
            float o = 0.5f * (baleSize - 1);
            for (int i = 0; i < baleSize; i++)
            for (int j = 0; j < baleSize; j++)
            for (int k = 0; k < baleSize; k++)
                Emit(centre + (x * (i - o) + y * (j - o) + z * (k - o)) * BalePitch, rot, block, domain);
        }

        static Vector3 InFrame(Vector3 f, Vector3 x, Vector3 y, Vector3 z) => x * f.x + y * f.y + z * f.z;

        static Quaternion Facing(Vector3 axis) =>
            Quaternion.LookRotation(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right);
    }
}
