// The GAME's numbers for the threat-flora grove (round 11c, Docs/THREAT_FLORA.md §4-5): where the Swarm cell's
// grove sits, and the research parameters re-expressed in game volumes. Plain C# (no UnityEngine) so the
// harness asserts the SAME numbers the ScriptableObject defaults to, and Tools/Build/author_threat_flora.py
// --check reads them from here rather than keeping a second copy.
using System.Numerics;

namespace CosmicShore.Gameplay
{
    public static class ThreatGroveDefaults
    {
        // ── where: a sector of the Swarm cell's RIM shell, outside every swarm band ─────────────────────────
        // The cell's bands (Tools/Build/author_swarm_fauna.py REGIONS): inner 470-620, middle 690-840, outer
        // 910-1080; membrane 1200, nucleus 392. The grove takes the rim between the outer band and the membrane,
        // so no swarm is penned through it and no swarm grazes it.
        public const float GroveInnerRadius = 1095f;
        public const float GroveOuterRadius = 1192f;
        public const float GroveHalfAngleDegrees = 18f;
        public static readonly Vector3 GroveAxis = Vector3.Normalize(new Vector3(1f, 0.3f, 0f));

        public static ThreatGroveShape SwarmCellGrove(Vector3 cellCentre) =>
            ThreatGroveShape.MakeShellSector(cellCentre, GroveAxis, GroveInnerRadius, GroveOuterRadius, GroveHalfAngleDegrees);

        // ── the snap-trap clumps ─────────────────────────────────────────────────────────────────────────────
        public const int SnapTrapClumps = 3;
        public const int SnapTrapSeedFloor = 9;     // three per clump
        public const int SnapTrapCap = 15;          // five per clump at most (collider budget, §5)
        public const float ClumpRadius = 57.0011f;  // research best: clump_r
        public const float ClumpSpreadDegrees = 9f; // the clumps sit this far either side of the grove axis
        // A trap ROOTS this far inside the grove's outer radius (ThreatGrove.RootAtRim), planted facing the cell
        // centre, and heliotropism may turn it at most HelioConeDegrees from that. Its whole body - every slot at
        // every gape, at any heading in the cone - then stays between the outer swarm band and the membrane (the
        // harness asserts it, S9). The rim gap is 120 u and a trap is ~85 u long, so the cone is what makes it fit.
        public const float TrapRootDepthMin = 15f;
        public const float TrapRootDepthMax = 25f;
        public const float HelioConeDegrees = 60f;
        public const float OuterSwarmBandEdge = 1080f;  // author_swarm_fauna.py REGIONS: the outer band's top
        public const float MembraneRadius = 1200f;      // author_swarm_fauna.py MEMBRANE_RADIUS
        public const int SnapTrapElement = 4;           // Time: the anchor's geometry (Space x1.35 would not fit the rim)
        public const int PhysarumElement = 3;           // Space: a longer sensor reach and step - a wider-roaming net

        // prism leaves (GAME units: x across, y the plate's thickness / normal, z along). Volumes = products.
        public static readonly Vector3 StalkLeaf = new Vector3(4f, 4f, 7f);
        public static readonly Vector3 LobeLeaf = new Vector3(9f, 1.6f, 10f);
        public static readonly Vector3 ToothLeaf = new Vector3(2.2f, 2.2f, 7f);
        public const int RootBitesPerAbsorb = 4;    // GAME: a trail prism is ~3 volume, a lobe plate 144 (§2.4)

        public static SnapTrapParams SnapTrap()
        {
            var p = new SnapTrapParams
            {
                StalkVolume = StalkLeaf.X * StalkLeaf.Y * StalkLeaf.Z,
                LobeVolume = LobeLeaf.X * LobeLeaf.Y * LobeLeaf.Z,
                ToothVolume = ToothLeaf.X * ToothLeaf.Y * ToothLeaf.Z,
                HelioConeDegrees = HelioConeDegrees,
            };
            return p;
        }

        // ── the physarum network ─────────────────────────────────────────────────────────────────────────────
        public const int Sclerotia = 5;             // research DEFAULTS n_hearts: the seed floor
        public const int SclerotiumCap = 8;         // hearts bud from the reserve up to this (collider budget, §5)
        public static readonly Vector3 TubeLeaf = new Vector3(3.6f, 3.6f, 12f);
        public static readonly Vector3 ShellLeaf = new Vector3(6f, 6f, 6f);
        public const int ShellPrisms = 6;           // the sclerotium's beat shell: an octahedron of prisms
        public const float ShellRadius = 14f;      // GAME: the sting's reach (the research's guard sphere was 50 u, §3.5)
        public const int PlantedTubesPerSclerotium = 25;
        public const int MaxTubes = 400;            // collider-budget cap (§5); the research grew ~1,750 in a 450 u ball
        public const float SimHz = 10f;             // the rate the Jones rule was scored at (research dt = 0.1)

        public static float TubeVolume => TubeLeaf.X * TubeLeaf.Y * TubeLeaf.Z;
        public static float ShellVolume => ShellPrisms * ShellLeaf.X * ShellLeaf.Y * ShellLeaf.Z;
        public static float PlantedVolumePerSclerotium => PlantedTubesPerSclerotium * TubeVolume + ShellVolume;

        public static PhysarumParams Physarum()
        {
            var p = new PhysarumParams
            {
                TubeVolume = TubeVolume,
                ShellVolume = ShellVolume,
                PlantVolume = Sclerotia * PlantedVolumePerSclerotium,
                MaxTubes = MaxTubes,
                StepSeconds = 1f / SimHz,
            };
            return p;
        }
    }
}
