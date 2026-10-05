// The authored side of a THREAT-FLORA grove (round 11c, Docs/THREAT_FLORA.md §4): where the grove sits in its
// cell, how its prisms are shaped, and the few numbers a designer may want to turn. Every default is
// ThreatGroveDefaults' (the numbers the harness asserts), so a config created by hand matches the Swarm cell's,
// and the research parameters themselves stay in SnapTrapParams / PhysarumParams where they are quoted per field.
using CosmicShore.Data;
using UnityEngine;
using NVec = System.Numerics.Vector3;

namespace CosmicShore.Gameplay
{
    [CreateAssetMenu(fileName = "ThreatGroveConfig", menuName = "ScriptableObjects/Flora/Threat Grove Config")]
    public class ThreatGroveConfigSO : ScriptableObject
    {
        [Header("Where (a sector of the cell's shell, measured from the cell centre)")]
        [Tooltip("Inner radius of the grove's shell sector. The Swarm cell's outer swarm band ends at 1,080, so the " +
                 "grove starts past it: no swarm is penned through it and no swarm grazes it.")]
        [Min(0f)] public float InnerRadius = ThreatGroveDefaults.GroveInnerRadius;

        [Tooltip("Outer radius of the grove's shell sector. Keep it inside the membrane (1,200 in the Swarm cell).")]
        [Min(0f)] public float OuterRadius = ThreatGroveDefaults.GroveOuterRadius;

        [Tooltip("Half-angle of the sector's cone about Axis, in degrees.")]
        [Range(1f, 90f)] public float HalfAngleDegrees = ThreatGroveDefaults.GroveHalfAngleDegrees;

        [Tooltip("Direction from the cell centre to the grove (normalised at use). The colour rule holds: this says " +
                 "WHERE the grove is, never which domain it is - each plant takes the spawner's domain.")]
        public Vector3 Axis = new Vector3(1f, 0.3f, 0f);

        [Header("Snap traps")]
        [Tooltip("How many clumps the traps are planted into. A seeded trap joins the clump with the fewest traps.")]
        [Min(1)] public int SnapTrapClumps = ThreatGroveDefaults.SnapTrapClumps;

        [Tooltip("Radius of one clump around its centre (research best clump_r).")]
        [Min(1f)] public float ClumpRadius = ThreatGroveDefaults.ClumpRadius;

        [Tooltip("Degrees either side of the grove axis the outer clumps sit at.")]
        [Min(0f)] public float ClumpSpreadDegrees = ThreatGroveDefaults.ClumpSpreadDegrees;

        [Tooltip("Stalk prism leaf (x across, y thickness, z along). Its volume is what a stalk slot costs.")]
        public Vector3 StalkLeaf = new Vector3(4f, 4f, 7f);

        [Tooltip("Lobe plate leaf (x across, y the plate's thickness, z along the lobe). The lobes ARE the glow, " +
                 "so the glow radius the telegraph test asserts is measured on these plates.")]
        public Vector3 LobeLeaf = new Vector3(9f, 1.6f, 10f);

        [Tooltip("Tooth leaf. Teeth are ordinary danger prisms: an opposing-domain vessel that touches one burns petals.")]
        public Vector3 ToothLeaf = new Vector3(2.2f, 2.2f, 7f);

        [Tooltip("How far heliotropism may turn a trap from the heading it was planted with (toward the cell " +
                 "centre). The rim is 120 u deep and a trap ~85 u long: this cone is what keeps its jaws off the " +
                 "swarm band and inside the membrane (harness S9).")]
        [Range(0f, 180f)] public float HelioConeDegrees = ThreatGroveDefaults.HelioConeDegrees;

        [Tooltip("Edible prisms a trap's roots take per absorb request (a trail prism is ~3 volume, a lobe 144).")]
        [Min(1)] public int RootBitesPerAbsorb = ThreatGroveDefaults.RootBitesPerAbsorb;

        [Tooltip("Snap-trap core rate (Hz). The research ran at 10 Hz; the sweep test needs 20 so a fast vessel " +
                 "cannot skip the closing jaws between ticks.")]
        [Range(5f, 60f)] public float SnapTrapHz = 20f;

        [Tooltip("Seconds between offspring attempts while the rhizome holds a whole body and the cell cap refuses one.")]
        [Min(0.1f)] public float BudRetrySeconds = 2f;

        [Header("Physarum network")]
        [Tooltip("Tube prism leaf (x, y across, z along the cable). Its volume is what a tube costs the reserve.")]
        public Vector3 TubeLeaf = new Vector3(3.6f, 3.6f, 12f);

        [Tooltip("One beat-shell prism around a sclerotium's crystal (six of them, an octahedron).")]
        public Vector3 ShellLeaf = new Vector3(6f, 6f, 6f);

        [Tooltip("How far the beat shell sits from the crystal. The shell's six prisms ARE the beat's sting.")]
        [Min(1f)] public float ShellRadius = ThreatGroveDefaults.ShellRadius;

        [Tooltip("The planted reserve each seeded sclerotium brings to the network, in tube bodies.")]
        [Min(0)] public int PlantedTubesPerSclerotium = ThreatGroveDefaults.PlantedTubesPerSclerotium;

        [Tooltip("Hard cap on live tubes - the collider budget (Docs/THREAT_FLORA.md §5). The research grew ~1,750 " +
                 "in a 450 u ball; the rim sector stays well under this in the harness (P4).")]
        [Min(0)] public int MaxTubes = ThreatGroveDefaults.MaxTubes;

        [Tooltip("The network's sim rate (Hz). 10 is the rate the Jones rule was searched at; its per-step " +
                 "rates (deposit, diffuse, evaporate, EMA) would mean something else at another rate.")]
        [Range(5f, 20f)] public float PhysarumHz = ThreatGroveDefaults.SimHz;

        [Tooltip("Warm-up steps run per frame when the grove starts (250 in all, ~1.4 ms each in the Swarm cell grove), so the start is ~4 ms a frame for ~1.4 s rather than one hitch.")]
        [Min(1)] public int WarmupStepsPerFrame = 3;

        [Tooltip("Tube prisms laid per frame at most. The rest queue: the core's ledger already holds them.")]
        [Min(1)] public int MaxLaysPerFrame = 24;

        [Tooltip("Seconds between refreshes of the food the network smells (edible prisms in the grove).")]
        [Min(0.1f)] public float FoodRefreshSeconds = 1f;

        [Tooltip("Food prisms the network tracks at most (nearest the grove centre first).")]
        [Min(16)] public int MaxFood = 1024;

        [Header("Far cadence (round 11f-2, Docs/ECOLOGY_LOD.md §6.3)")]
        [Tooltip("Flora is never LOD'd, but with no pilot near the network runs on slowed time: Advance gets dt x this. " +
                 "Every step is the same mass-exact step at the same 10 Hz rule, there are just fewer of them a second " +
                 "(0.25 -> a quarter of the cost; the grove grows, eats and beats a quarter as fast). 1 = always full rate.")]
        [Range(0.05f, 1f)] public float FarTimeScale = 0.25f;

        [Tooltip("A pilot (any vessel, or the main camera) within the grove's reach plus this margin keeps the network " +
                 "at full rate. 400 = the ecology LOD's collapse radius.")]
        [Min(0f)] public float FarMargin = 400f;

        [Header("Look")]
        [Tooltip("The palette the lobes glow in (TryGetPrismKindColors, Danger / Plain per domain).")]
        public ThemeManagerDataContainerSO Theme;

        public NVec AxisN
        {
            get
            {
                var a = new NVec(Axis.x, Axis.y, Axis.z);
                return a.LengthSquared() < 1e-8f ? NVec.UnitX : NVec.Normalize(a);
            }
        }

        public ThreatGroveShape BuildShape(Vector3 cellCentre) =>
            ThreatGroveShape.MakeShellSector(new NVec(cellCentre.x, cellCentre.y, cellCentre.z), AxisN,
                InnerRadius, OuterRadius, HalfAngleDegrees);

        public SnapTrapParams BuildSnapTrap(Element element)
        {
            var p = new SnapTrapParams
            {
                StalkVolume = StalkLeaf.x * StalkLeaf.y * StalkLeaf.z,
                LobeVolume = LobeLeaf.x * LobeLeaf.y * LobeLeaf.z,
                ToothVolume = ToothLeaf.x * ToothLeaf.y * ToothLeaf.z,
                HelioConeDegrees = HelioConeDegrees,
            };
            return p.ForElement((int)element);
        }

        public float TubeVolume => TubeLeaf.x * TubeLeaf.y * TubeLeaf.z;
        public float ShellVolume => ThreatGroveDefaults.ShellPrisms * ShellLeaf.x * ShellLeaf.y * ShellLeaf.z;
        public float PlantedVolumePerSclerotium => PlantedTubesPerSclerotium * TubeVolume + ShellVolume;

        public PhysarumParams BuildPhysarum(Element element)
        {
            var p = new PhysarumParams
            {
                TubeVolume = TubeVolume,
                ShellVolume = ShellVolume,
                MaxTubes = MaxTubes,
                StepSeconds = 1f / Mathf.Max(1f, PhysarumHz),
            };
            return p.ForElement((int)element);
        }
    }
}
