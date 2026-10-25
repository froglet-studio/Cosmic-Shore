using CosmicShore.Data;
using UnityEngine;
using SVector3 = System.Numerics.Vector3;

namespace CosmicShore.Gameplay
{
    /// <summary>Which stealing species a <see cref="BuilderColonyFauna"/> anchor runs.</summary>
    public enum BuilderSpecies
    {
        /// <summary>A colony that walls its core in with stolen prisms and mends every cut (the nest is its peaceful phase).</summary>
        Fortress = 0,
        /// <summary>Magpies that nest on a plant, tail ships and hoard their warm wake.</summary>
        Thieves = 1,
        /// <summary>Hearts that steal prisms - mostly a ship's trail - and WEAR them as a moving body that grows, fuses,
        /// rears and lunges (Docs/BUILDERS_AND_THIEVES.md §10).</summary>
        Wearers = 2,
    }

    /// <summary>
    /// Every number a builder colony or a thief nest runs on (Docs/BUILDERS_AND_THIEVES.md). Defaults quote the research
    /// (Tools/Ecology/builders, bestiary/species/thief.py); the GAME additions say so. Where it lives and its element are
    /// the species config's (FaunaConfigurationSO band + Element), its colour is the cell's controlling domain at spawn.
    /// Authored by Tools/Build/author_builders.py; hand edits are drift.
    /// </summary>
    [CreateAssetMenu(fileName = "BuilderColonyConfig", menuName = "ScriptableObjects/Fauna/Builder Colony Config")]
    public class BuilderColonyConfigSO : ScriptableObject
    {
        [Header("Species")]
        [Tooltip("FORTRESS: workers steal loose mass and wall their core in; a cut wall knits shut (alarm + gap rules). " +
                 "THIEVES: a nest on a plant whose magpies tail a ship and snatch its warm wake to a hoard. " +
                 "WEARERS: hearts that wear what they steal as a moving body, and hunt with it once it is big.")]
        public BuilderSpecies Species = BuilderSpecies.Fortress;

        [Header("Members (drawn on the GPU, real only near a vessel)")]
        [Tooltip("The proxy a member becomes within EngageRadius of a vessel: the swarm's member prefab (a heart + one body " +
                 "prism) - one creature look for every GPU-drawn population. Its heart drops as the member's crystal.")]
        public SwarmTadpoleFauna MemberPrefab;
        [Tooltip("SwarmMemberInstanced - the swarm's GPU member draw, shared (one buffer, one draw per mesh).")]
        public Shader MemberShader;
        [Tooltip("The palette every member is drawn in (plain / danger tier pairs of the colony's domain).")]
        public ThemeManagerDataContainerSO Theme;
        [Tooltip("Draw living members from one GPU buffer. Off (or a device without vertex-stage structured buffers): " +
                 "every member is a proxy GameObject.")]
        public bool DrawMembersOnGpu = true;
        [Tooltip("World radius around a vessel inside which a member becomes a real proxy (heart + body prism colliders).")]
        public float EngageRadius = 140f;
        [Tooltip("Most proxies this colony holds at once (nearest first).")]
        public int MaxProxies = 24;
        [Tooltip("Seconds a proxy lingers after its vessel left range.")]
        public float ProxyLingerSeconds = 2f;
        [Tooltip("Simulation steps per second (the research ran at 10 Hz).")]
        public float TickHz = 10f;
        [Tooltip("A member's body prism local scale (x wide, y thin, z long).")]
        public Vector3 BodyScale = new(1.4f, 0.9f, 2.6f);
        [Tooltip("A member's heart world scale per element (x Charge, y Mass, z Space, w Time) - the crystal it drops.")]
        public Vector4 HeartWorldScale = new(2.298f, 1.737f, 2.298f, 1.737f);
        [Tooltip("Gap between the heart and the body prism behind it.")]
        public float HeartPrismGap = 0.6f;
        [Tooltip("Seconds a newborn takes to bloom in from nothing (continuity of existence).")]
        public float BirthBloomSeconds = 0.8f;
        [Tooltip("A vessel's hull radius for contact (ram / knock-down).")]
        public float VesselRadius = 9f;
        [Tooltip("Seconds a deposited prism takes to settle from its carrier onto its site (a clock-stamped flight).")]
        public float SettleSeconds = 0.35f;
        [Header("Round 11f-2 - ecology LOD (Docs/ECOLOGY_LOD.md §6.2)")]
        [Tooltip("Far from every pilot and unseen, the colony COLLAPSES: its members hold still where they are drawn " +
                 "(index entries and structure untouched, so LiveVolume does not move) and ROOST - each stomach burns at " +
                 "the species' torpor once a second, the rate a roosting member already burns. It collapses only with " +
                 "nothing carried, claimed or settling and no creature hunting, and expands on the research prefetch " +
                 "radii, when hit, or before its emptiest stomach runs out (a death is always an individual's).")]
        public bool MacroLod = true;
        [Tooltip("Seconds of torpor the emptiest stomach must have left: below it a collapsed colony expands (twice it is " +
                 "needed to collapse).")]
        [Min(1f)] public float ThawReserveSeconds = 20f;
        [Header("Round 11-10 - extinction (Docs/SWARM_FAUNA.md §27)")]
        [Tooltip("Seconds a colony with no living member, no proxy and nothing dying lingers before its anchor leaves, so " +
                 "the cell's seeder can hatch a fresh colony (extinction recovery, the seeder's sanctioned job - the swarm " +
                 "and the substrate anchors already leave this way). The structure stays: walls, hoard and lair are released " +
                 "as loose prisms (nothing pops).")]
        [Min(0f)] public float ExtinctLingerSeconds = 8f;

        [Header("Fortress - colony")]
        [Tooltip("Workers the colony is founded with (research 48; 24-48 recommended).")]
        public int Founders = 48;
        [Tooltip("Most workers alive at once - a backstop, not the dynamics (births stop at it; nothing is culled).")]
        public int MaxWorkers = 48;
        public float WorkerSpeed = 70f;
        [Tooltip("Foraging reach (PrismSpatialIndex.QuerySphere radius).")]
        public float Sense = 220f;
        [Tooltip("1 in N workers re-forages per step.")]
        public int ForageFraction = 4;
        [Tooltip("Construction lattice spacing (world units).")]
        public float LatticeSpacing = 8f;
        [Tooltip("Lattice half-extent in sites (33^3 at 16).")]
        public int LatticeHalf = 16;

        [Header("Fortress - the local rule")]
        [Tooltip("Template shell radius Rc: Q(r) = exp(-((r - Rc) / w)^2). Fortress 40, nest 36 (PORT.md suggests 44 / w 30).")]
        public float ShellRadius = 40f;
        [Tooltip("Template width w.")]
        public float ShellWidth = 7f;
        public float CementK = 0.6f;
        [Tooltip("Deposit probability factor on a site that touches nothing (a new pillar).")]
        public float Nucleate = 0.02f;
        [Tooltip("Which mending cues are on: alarm (laden workers climb a breach's pheromone) and gap (surrounded holes fill first).")]
        public BuilderMendRule Mend = BuilderMendRule.Both;
        public float GapGain = 2.5f;
        public float AlarmGain = 1f;
        [Tooltip("Scar tissue: where the wall is cut it re-grows thicker (0 = off; the research ran 3).")]
        public float ScarGain = 3f;

        [Header("Fortress - defence")]
        [Tooltip("A vessel inside this radius of the core raises the colony's alarm level (0.6/s).")]
        public float AlarmRadius = 110f;
        [Tooltip("At this alarm level the guard screen turns into a strike (the telegraph is the screen).")]
        public float StrikeAt = 0.6f;
        [Tooltip("Defender caste fraction: only this share of idle workers ever answers the alarm. 0.3 keeps repair at " +
                 "full speed (research t50 5.6 s vs 17.8 s with every worker defending); the design dial between " +
                 "'mends' and 'stings'.")]
        [Range(0f, 1f)] public float DefendCaste = 0.3f;

        [Header("Fortress - stomach (GAME)")]
        public float WorkerStomach = 40f;
        public float WorkerMetabolism = 0.02f;
        [Range(0f, 1f)] public float WorkerHungryBelow = 0.35f;
        [Range(0f, 1f)] public float WorkerBirthAbove = 0.9f;
        public float WorkerBirthCost = 16f;
        [Tooltip("Below this fraction of the stomach a worker is DESPERATE and its colony's OWN colour becomes food. " +
                 "Another domain's mass is always food (on the platform diet) and wins whenever both are in reach; own " +
                 "colour is the starvation fallback - the cell's Frenzy shape on a stomach. Keep it below WorkerHungryBelow " +
                 "(it is clamped there) so a fed worker never takes its own colour (Docs/BUILDERS_AND_THIEVES.md §2.1).")]
        [Range(0f, 1f)] public float WorkerOwnDomainBelow = 0.25f;

        [Header("Thieves")]
        [Tooltip("Thieves the nest is FOUNDED with (GAME: the research seeded 18 at once and starved in the opening).")]
        public int ThiefFounders = 6;
        [Tooltip("Most thieves alive at once (research 18) - a backstop.")]
        public int MaxThieves = 18;
        public float ThiefSpeed = 150f;
        [Tooltip("A laden thief's speed - HALF its free speed is what makes turning back the counterplay.")]
        public float LadenSpeed = 75f;
        [Tooltip("Only trail laid within this many seconds is wanted (the warm wake).")]
        public float WarmSeconds = 1.5f;
        [Tooltip("A ship within this range is tailed like a trawler.")]
        public float SpotRange = 700f;
        [Tooltip("How far a free thief searches for warm wake.")]
        public float ScoutRange = 400f;
        [Tooltip("GAME: a nest only follows ships this close to its plant (the living cell's leash).")]
        public float Territory = 900f;
        [Tooltip("A free thief that a ship points at inside this range veers off (bold, not brave).")]
        public float TimidRange = 120f;
        public float ThiefStomach = 20f;
        public float ThiefMetabolism = 0.02f;
        [Tooltip("GAME: the roost's torpor - an empty opening costs a founded nest almost nothing.")]
        public float ThiefTorpor = 0.004f;
        [Range(0f, 1f)] public float ThiefHungryBelow = 0.4f;
        [Range(0f, 1f)] public float ThiefBirthAbove = 0.9f;
        public float ThiefBirthCost = 8f;
        [Tooltip("Below this fraction of the stomach a thief is DESPERATE and its colony's OWN colour becomes food. " +
                 "Another domain's mass is always food (on the platform diet) and wins whenever both are in reach; own " +
                 "colour is the starvation fallback - the cell's Frenzy shape on a stomach. Keep it below ThiefHungryBelow " +
                 "(it is clamped there) so a fed thief never takes its own colour (Docs/BUILDERS_AND_THIEVES.md §2.1).")]
        [Range(0f, 1f)] public float ThiefOwnDomainBelow = 0.25f;

        [Header("Wearers (Docs/BUILDERS_AND_THIEVES.md §10)")]
        [Tooltip("Hearts the colony is founded with (GAME: the research seeded 40; one creature's worth for the demo cell).")]
        public int WearerFounders = 16;
        [Tooltip("Most hearts alive at once - a backstop (births stop at it; nothing is culled).")]
        public int MaxWearerHearts = 24;
        [Tooltip("A bare heart's speed; a body slows it: speed * (1 + n / HuntAt)^-0.15.")]
        public float WearerSpeed = 95f;
        [Tooltip("Body prisms at which a skulking thief turns hunter (research V_hunt 600 volume / ~10 per worn prism).")]
        public int WearHuntAt = 60;
        [Tooltip("A body over this many prisms sheds its outermost into a static lair and a newborn heart (research v3).")]
        public int WearBodyCap = 150;
        [Tooltip("Worn prisms across the whole colony - the moving-prism budget; thieves stop stealing at it.")]
        public int WornCap = 300;
        [Tooltip("How far a heart looks for loose mass to steal.")]
        public float WearSense = 180f;
        [Tooltip("A skulking heart never steals closer than this to a ship.")]
        public float WearKeepOff = 90f;
        [Tooltip("How far a heart SEES a ship (the thief nest's SpotRange). A ship beyond it is not followed or hunted, so a " +
                 "colony with nobody in sight can roost (round 11-10; before, hearts saw the nearest ship anywhere).")]
        public float WearSight = 700f;
        [Tooltip("The rear: seconds the body contracts before it lunges (the telegraph).")]
        public float WearWindup = 1f;
        [Tooltip("Lunge speed factor (x 1.8 of the body's speed).")]
        public float WearLunge = 2.2f;
        [Tooltip("A hunter rears when its target is inside its radius + this.")]
        public float WearRearAt = 140f;
        [Tooltip("Contact attachment temperature: a stolen prism sticks near where it touched (0 = an isotropic blob).")]
        public float WearContact = 0.5f;
        [Tooltip("Losing this share of the body inside HurtWindow seconds makes it moult (GAME).")]
        public float WearHurtFraction = 0.1667f;
        [Tooltip("The hurt moult sheds this share of what is left BACK to whoever it was stolen from.")]
        public float WearHurtShed = 0.3f;
        public float WearHurtWindow = 2f;
        public float WearerStomach = 30f;
        public float WearerMetabolism = 0.01f;
        [Range(0f, 1f)] public float WearerHungryBelow = 0.3f;
        public float WearerBirthCost = 10f;
        [Tooltip("Below this fraction of the stomach a heart is DESPERATE and its colony's OWN colour becomes food. " +
                 "Another domain's mass is always food (on the platform diet) and wins whenever both are in reach; own " +
                 "colour is the starvation fallback - the cell's Frenzy shape on a stomach. Keep it below WearerHungryBelow " +
                 "(it is clamped there) so a fed heart never takes its own colour (Docs/BUILDERS_AND_THIEVES.md §2.1).")]
        [Range(0f, 1f)] public float WearerOwnDomainBelow = 0.25f;

        /// <summary>The wearer core's parameters.</summary>
        public WearerParams ToWearerParams(Vector3 cellCentre, float membrane) => new()
        {
            Founders = WearerFounders, MaxHearts = MaxWearerHearts, Speed = WearerSpeed, HuntAt = WearHuntAt,
            BodyCap = WearBodyCap, WornCap = WornCap, Sense = WearSense, KeepOff = WearKeepOff, Sight = WearSight,
            Windup = WearWindup, Lunge = WearLunge, RearAt = WearRearAt, Contact = WearContact,
            HurtFraction = WearHurtFraction, HurtShed = WearHurtShed, HurtWindow = WearHurtWindow,
            Containment = membrane * 0.95f, CellCentre = new SVector3(cellCentre.x, cellCentre.y, cellCentre.z),
            Stomach = new BuilderStomachParams
            {
                Capacity = WearerStomach, FounderFill = 0.6f, Metabolism = WearerMetabolism, Torpor = WearerMetabolism,
                HungryBelow = WearerHungryBelow, BirthAbove = 0.9f, BirthCost = WearerBirthCost, OwnDomainBelow = WearerOwnDomainBelow,
            },
        };

        /// <summary>The colony core's parameters, in world units around <paramref name="cellCentre"/>.</summary>
        public BuilderColonyParams ToColonyParams(Vector3 cellCentre, float membrane, float bandInner, float bandOuter) => new()
        {
            Founders = Founders, MaxWorkers = MaxWorkers, Speed = WorkerSpeed, Sense = Sense, ForageFraction = ForageFraction,
            Spacing = LatticeSpacing, LatticeHalf = LatticeHalf,
            Rc = ShellRadius, W = ShellWidth, KCement = CementK, Nucleate = Nucleate,
            Mend = Mend, GapGain = GapGain, AlarmGain = AlarmGain, ScarGain = ScarGain,
            AlarmRadius = AlarmRadius, StrikeAt = StrikeAt, DefendCaste = DefendCaste,
            SettleSeconds = SettleSeconds,
            Containment = membrane * 0.95f, CellCentre = new SVector3(cellCentre.x, cellCentre.y, cellCentre.z),
            BandInner = bandInner, BandOuter = bandOuter,
            Stomach = new BuilderStomachParams
            {
                Capacity = WorkerStomach, Metabolism = WorkerMetabolism, Torpor = WorkerMetabolism,
                HungryBelow = WorkerHungryBelow, BirthAbove = WorkerBirthAbove, BirthCost = WorkerBirthCost,
                OwnDomainBelow = WorkerOwnDomainBelow,
            },
        };

        /// <summary>The thief core's parameters.</summary>
        public ThiefParams ToThiefParams(Vector3 cellCentre, float membrane) => new()
        {
            Founders = ThiefFounders, MaxThieves = MaxThieves, FreeSpeed = ThiefSpeed, LadenSpeed = LadenSpeed,
            Warm = WarmSeconds, Spot = SpotRange, Scout = ScoutRange, Territory = Territory, TimidRange = TimidRange,
            SettleSeconds = SettleSeconds,
            Containment = membrane * 0.92f, MembraneRadius = membrane,
            CellCentre = new SVector3(cellCentre.x, cellCentre.y, cellCentre.z),
            Stomach = new BuilderStomachParams
            {
                Capacity = ThiefStomach, Metabolism = ThiefMetabolism, Torpor = ThiefTorpor,
                HungryBelow = ThiefHungryBelow, BirthAbove = ThiefBirthAbove, BirthCost = ThiefBirthCost,
                OwnDomainBelow = ThiefOwnDomainBelow,
            },
        };

        /// <summary>A member's heart world scale for <paramref name="e"/>.</summary>
        public float HeartScale(Element e) => SwarmFaunaConfigSO.Of(HeartWorldScale, e);
    }
}
