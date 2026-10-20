using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Every number the Krill reads (design: <c>R_VesselActions/KRILL.md</c>). The first block is the Krill Lab's
    /// <c>SHIPPED</c> (<c>Docs/Studios/KrillLab.html</c>), field for field under the lab's own names so
    /// <c>Tools/Build/studio_to_unity.py</c> can clone them; the second block is the game's own contact and
    /// presentation numbers, each copied from the lab's model where the lab has one (named in its tooltip).
    /// Seeded once by <c>Tools/Build/author_krill_assets.py</c>; tune in the lab, never here by hand.
    /// </summary>
    [CreateAssetMenu(fileName = "KrillConfig", menuName = "ScriptableObjects/Vessel Actions/Krill Config")]
    public sealed class KrillConfigSO : ScriptableObject
    {
        [Header("Cards")]
        [Tooltip("The 80 cards (generated from cards.json). Hands are dealt from its wired cards.")]
        [SerializeField] KrillCardDeckSO deck;

        [Header("Phases (the Krill Lab's SHIPPED)")]
        [Tooltip("Omnicrystal phase timer (s). A hand is dealt when this phase ends, crystal or not.")]
        [SerializeField, Min(1f)] float tOmni = 30f;
        [Tooltip("Elemental crystals phase timer (s).")]
        [SerializeField, Min(1f)] float tElem = 25f;
        [Tooltip("Opposing vessels phase timer (s).")]
        [SerializeField, Min(1f)] float tVessel = 25f;
        [Tooltip("Opposing mass phase timer (s).")]
        [SerializeField, Min(1f)] float tMass = 25f;
        [Tooltip("Y: elemental crystals collected in the phase that end it before its timer.")]
        [SerializeField, Min(1)] int goalY = 5;
        [Tooltip("X: enemy prisms destroyed or stolen in the phase that end it before its timer.")]
        [SerializeField, Min(1)] int goalX = 30;

        [Header("Swarm (the Krill Lab's SHIPPED)")]
        [Tooltip("Members the swarm starts with.")]
        [SerializeField, Min(1)] int startSize = 24;
        [Tooltip("Most members the swarm can hold before Big Family.")]
        [SerializeField, Min(1)] int cap = 40;
        [Tooltip("The protected minimum: at or below this many members nothing can harm the swarm.")]
        [SerializeField, Min(0)] int minSize = 6;
        [Tooltip("Member cruise speed before Time cards (u/s).")]
        [SerializeField, Min(1f)] float cruise = 120f;
        [Tooltip("How fast a member can swing round before Agility (deg/s).")]
        [SerializeField, Min(1f)] float turn = 70f;

        [Header("Element levels (the Krill Lab's SHIPPED): a drawn card of the element arrives at this level")]
        [SerializeField, Range(1, 15)] int lvMass = 1;
        [SerializeField, Range(1, 15)] int lvTime = 1;
        [SerializeField, Range(1, 15)] int lvSpace = 1;
        [SerializeField, Range(1, 15)] int lvCharge = 1;

        [Header("View (the Krill Lab's SHIPPED)")]
        [Tooltip("Half-thickness of the viewing plane's sharp slice (u): the Wrasse's 2.5D depth look (PrismDepthPlane).")]
        [SerializeField, Min(1f)] float slab = 260f;
        [Tooltip("World units across the short side of the screen. The camera asset's followOffset is seeded from it.")]
        [SerializeField, Min(100f)] float viewSpan = 1400f;

        [Header("Contacts (the lab's model constants)")]
        [Tooltip("Contact checks per second. The lab steps every frame; 10 Hz keeps 40 members x the world cheap.")]
        [SerializeField, Min(1f)] float contactHz = 10f;
        [Tooltip("A member this close to an elemental crystal collects it (the lab's 28 u).")]
        [SerializeField, Min(0.1f)] float crystalReach = 28f;
        [Tooltip("A member this close to the omnicrystal collects it (the lab's 20 u).")]
        [SerializeField, Min(0.1f)] float omniReach = 20f;
        [Tooltip("A member's bite reach on a prism before Wide Bite (the lab's 25 u).")]
        [SerializeField, Min(0.1f)] float prismReach = 25f;
        [Tooltip("A bare member's chance per second to break a plain enemy prism it touches (the lab's 0.5).")]
        [SerializeField, Min(0f)] float baseBite = 0.5f;
        [Tooltip("A member this close to a rival vessel nips it (the lab's 16 u).")]
        [SerializeField, Min(0.1f)] float rivalReach = 16f;
        [Tooltip("A bare member's nip, in petals per second (the lab's 0.05).")]
        [SerializeField, Min(0f)] float nipRate = 0.05f;
        [Tooltip("Petals of nips and zaps that make one scored hit (the lab's debuff every 1/3 petal).")]
        [SerializeField, Min(0.01f)] float petalsPerHit = 1f / 3f;
        [Tooltip("The combat-hit latch between two scored hits on the same rival (s). PvP is petals only: every hit is scored.")]
        [SerializeField, Min(0f)] float hitCooldown = 0.5f;
        [Tooltip("The cell radius used when the scene has no Cell (the lab's R).")]
        [SerializeField, Min(10f)] float fallbackCellRadius = 900f;

        [Header("Presentation (game only)")]
        [Tooltip("Each member's model scale (Krill.fbx is about 4.5 u long).")]
        [SerializeField, Min(0.01f)] float memberScale = 3f;
        [Tooltip("How fast the pilot turns the viewing plane at full stick (deg/s). It stops when the stick is let go.")]
        [SerializeField, Min(1f)] float viewTurnRate = 90f;
        [Tooltip("How fast the view's centre eases onto the swarm (1/s).")]
        [SerializeField, Min(0.01f)] float followRate = 3f;

        public KrillCardDeckSO Deck => deck;
        public float TOmni => tOmni;
        public float TElem => tElem;
        public float TVessel => tVessel;
        public float TMass => tMass;
        public int GoalY => goalY;
        public int GoalX => goalX;
        public int StartSize => startSize;
        public int Cap => cap;
        public int MinSize => minSize;
        public float Cruise => cruise;
        public float Turn => turn;
        public float Slab => slab;
        public float ViewSpan => viewSpan;
        public float ContactHz => contactHz;
        public float CrystalReach => crystalReach;
        public float OmniReach => omniReach;
        public float PrismReach => prismReach;
        public float BaseBite => baseBite;
        public float RivalReach => rivalReach;
        public float NipRate => nipRate;
        public float PetalsPerHit => petalsPerHit;
        public float HitCooldown => hitCooldown;
        public float FallbackCellRadius => fallbackCellRadius;
        public float MemberScale => memberScale;
        public float ViewTurnRate => viewTurnRate;
        public float FollowRate => followRate;

        /// <summary>The level a drawn card of <paramref name="element"/> arrives at.</summary>
        public int LevelFor(Data.Element element) => element switch
        {
            Data.Element.Mass => lvMass,
            Data.Element.Time => lvTime,
            Data.Element.Space => lvSpace,
            Data.Element.Charge => lvCharge,
            _ => 1,
        };

        /// <summary>The phase timer of <paramref name="phase"/>, before Hurry Up.</summary>
        public float PhaseTimer(KrillPhase phase) => phase switch
        {
            KrillPhase.Omnicrystal => tOmni,
            KrillPhase.ElementalCrystals => tElem,
            KrillPhase.OpposingVessels => tVessel,
            _ => tMass,
        };

        /// <summary>For tests: a config at the field initializers (the lab's SHIPPED) reading <paramref name="cards"/>.</summary>
        public static KrillConfigSO CreateForTests(KrillCardDeckSO cards)
        {
            var config = CreateInstance<KrillConfigSO>();
            config.deck = cards;
            return config;
        }
    }
}
