using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Round 11b (Docs/SUBSTRATE_FAUNA.md §4): ONE SPECIES of the Living Ecology substrate, as data. The species is
    /// <see cref="Species"/> - every number <see cref="SubstrateCore"/> reads (two regimes, drives, the quorum rule and
    /// the bestiary primitives); nothing in the core branches on which species it is stepping. The rest of this asset is
    /// the glue's: how agents are seeded, drawn, made real near a vessel and fed.
    ///
    /// The shipped assets (Pack Hunter, Locust, Lurker) are written by <c>Tools/Build/author_substrate_fauna.py</c> from
    /// <c>Tools/Build/substrate_harness/game_params.json</c>, which the harness asserts is the research parameter set plus
    /// the documented game deltas (harness test F). Edit a number in the inspector to tune; re-run the author script to
    /// return to the proven set.
    /// </summary>
    [CreateAssetMenu(fileName = "SubstrateSpecies", menuName = "ScriptableObjects/Fauna/Substrate Species")]
    public class SubstrateSpeciesSO : ScriptableObject
    {
        [Header("Species (the substrate's numbers - Docs/SUBSTRATE_FAUNA.md §2)")]
        [Tooltip("Every parameter the substrate core reads for this species: the solitary and gregarious regimes (each " +
                 "steering weight is lerp(solitary, gregarious, phase)), metabolism and stomach, the quorum rule, and the " +
                 "bestiary primitives (closure quorum, posture clock, gaze, mimicry, prey). Name is what a predator's " +
                 "PreyName matches.")]
        [SerializeField] SubstrateSpeciesParams species = new();

        [Header("Seeding")]
        [Tooltip("Agents a new population is seeded with (each paid for at the species' Stock0 - a spawner is a seeder). " +
                 "0 = the species' own N0.")]
        [SerializeField, Min(0)] int seedCount;
        [Tooltip("World spread (one sigma) of the seed cloud around the anchor's spawn point.")]
        [SerializeField, Min(0f)] float seedSpread = 60f;
        [Tooltip("Seed each agent AT a living flora heart in the population's band instead of around the anchor - an " +
                 "ambusher that mimics crystals is seeded among them (research anchor=\"mass\").")]
        [SerializeField] bool seedAtFlora;

        [Header("Drawing")]
        [Tooltip("The agent's proxy prefab (a SubstrateAgentFauna: one body prism and a heart). Its body prism's mesh is " +
                 "also what the GPU draws for every agent, so an agent looks the same drawn or real.")]
        [SerializeField] SubstrateAgentFauna agentPrefab;
        [Tooltip("The instanced member shader (Assets/_Graphics/Materials/Graphs/SwarmMemberInstanced.shader) - the " +
                 "swarm's own; agents publish the same 80-byte SwarmInstance. Referenced here so a build includes it.")]
        [SerializeField] Shader memberShader;
        [Tooltip("The palette agents wear (plain / danger tier pairs of their domain, the neutral living-heart pair).")]
        [SerializeField] ThemeManagerDataContainerSO theme;
        [Tooltip("World scale of each agent's heart crystal, per element (x Charge, y Mass, z Space, w Time).")]
        [SerializeField] Vector4 heartWorldScale = new(2.298f, 1.737f, 2.298f, 1.737f);
        [Tooltip("World gap between the heart and the front of the body prism.")]
        [SerializeField, Min(0f)] float heartPrismGap = 0.6f;
        [Tooltip("A body prism is w wide, Thin*w tall and aspect*w long, with volume = the agent's stock (mass).")]
        [SerializeField, Range(0.1f, 1f)] float bodyThin = 0.6f;

        [Header("Proxies (the collider budget - Docs/SUBSTRATE_FAUNA.md §5)")]
        [Tooltip("World radius around a vessel inside which an agent is given a PROXY: a real heart and body prism with " +
                 "colliders, so rams, guns, jousts and its DANGER plate all act. Outside it an agent is data and pixels.")]
        [SerializeField, Min(0f)] float engageRadius = 160f;
        [Tooltip("Most proxies this population holds at once (nearest to a vessel first). Two colliders each - this " +
                 "number is the population's share of the cell's collider ceiling (author_swarm_fauna.py checks it).")]
        [SerializeField, Min(0)] int maxProxies = 12;
        [Tooltip("Seconds a proxy outlives its vessel leaving before it is given back to the simulation.")]
        [SerializeField, Min(0f)] float proxyLingerSeconds = 2f;
        [Tooltip("Proxy Instantiates per frame this population may make (a hit, a predation or a starvation is never " +
                 "deferred).")]
        [SerializeField, Min(1)] int maxSpawnsPerFrame = 6;
        [Tooltip("World radius of a vessel as the substrate senses it (closure, bites, gaze).")]
        [SerializeField, Min(0.5f)] float vesselRadius = 6f;

        [Header("Feeding (mass is conserved: a bite becomes body 1:1)")]
        [Tooltip("World radius around a hungry agent in which it can take a bite of a flora prism.")]
        [SerializeField, Min(0.5f)] float biteRadius = 24f;
        [Tooltip("Bites this population may take per tick (the main-thread cost that scales with appetite).")]
        [SerializeField, Min(1)] int maxBitesPerTick = 12;

        [Header("Tick")]
        [Tooltip("Agent slots the cell's substrate reserves when THIS species is the first to arrive (every population " +
                 "of every species in the cell shares one core).")]
        [SerializeField, Min(64)] int cellCapacity = 1024;
        [Tooltip("Run the cell's substrate tick on a worker thread (WebGL always runs it inline).")]
        [SerializeField] bool simulateOffMainThread = true;
        [Tooltip("Seconds an empty population lingers before its anchor is despawned (the seeder may hatch a new one).")]
        [SerializeField, Min(0f)] float extinctLingerSeconds = 8f;
        [Header("Round 11f-2 - ecology LOD (Docs/ECOLOGY_LOD.md §6.1)")]
        [Tooltip("Far from every pilot and unseen, a population FREEZES: the cell's substrate skips it in every pass " +
                 "(its agents hold still, keep their stock and index entries, so LiveVolume does not move) except " +
                 "metabolism - each agent still gets hungry at the species' rate. It thaws on the research prefetch " +
                 "radii (280 u, 450 u ahead) before a pilot can see it, when hit or hunted, or before its hungriest " +
                 "agent's reserve runs out (starvation is decided only by individuals).")]
        [SerializeField] bool macroLod = true;
        [Tooltip("Seconds of reserve the hungriest agent must have left: below it a frozen population thaws (twice it " +
                 "is needed to freeze), so an agent always starves - or eats - as an individual.")]
        [SerializeField, Min(0.5f)] float thawReserveSeconds = 5f;

        public SubstrateSpeciesParams Species => species;
        public string SpeciesName => species != null ? species.Name : "";
        public int SeedCount => seedCount > 0 ? seedCount : (species != null ? species.N0 : 0);
        public float SeedSpread => seedSpread;
        public bool SeedAtFlora => seedAtFlora;
        public SubstrateAgentFauna AgentPrefab => agentPrefab;
        public Shader MemberShader => memberShader;
        public ThemeManagerDataContainerSO Theme => theme;
        public Vector4 HeartWorldScaleByElement => heartWorldScale;
        public float HeartPrismGap => heartPrismGap;
        public float BodyThin => bodyThin;
        public float EngageRadius => engageRadius;
        public int MaxProxies => maxProxies;
        public float ProxyLingerSeconds => proxyLingerSeconds;
        public int MaxSpawnsPerFrame => maxSpawnsPerFrame;
        public float VesselRadius => vesselRadius;
        public float BiteRadius => biteRadius;
        public int MaxBitesPerTick => maxBitesPerTick;
        public int CellCapacity => cellCapacity;
        public bool SimulateOffMainThread => simulateOffMainThread;
        public float ExtinctLingerSeconds => extinctLingerSeconds;
        public bool MacroLod => macroLod;
        public float ThawReserveSeconds => thawReserveSeconds;

        /// <summary>A private copy for one population (a tuned asset never changes a live core under it).</summary>
        public SubstrateSpeciesParams ToParams() => (species ?? new SubstrateSpeciesParams()).Clone();

        /// <summary>The heart's world scale for <paramref name="e"/>.</summary>
        public float HeartWorldScale(Element e) => e switch
        {
            Element.Charge => heartWorldScale.x,
            Element.Mass => heartWorldScale.y,
            Element.Space => heartWorldScale.z,
            Element.Time => heartWorldScale.w,
            _ => heartWorldScale.y,
        };

        /// <summary>Research element index (0 Charge, 1 Mass, 2 Space, 3 Time) - the member shader's and the core's.</summary>
        public static int ToIndex(Element e) => e switch
        {
            Element.Charge => 0, Element.Mass => 1, Element.Space => 2, Element.Time => 3, _ => -1,
        };

        public static Element ToElement(int index) => index switch
        {
            0 => Element.Charge, 1 => Element.Mass, 2 => Element.Space, 3 => Element.Time, _ => Element.Mass,
        };
    }
}
