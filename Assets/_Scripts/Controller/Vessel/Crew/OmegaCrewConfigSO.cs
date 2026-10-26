using CosmicShore.Core;
using FMODUnity;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Every number of an Omega crew (the combiner crew vessels: Warden, Bulwark, Tempest). The Vessel Studio's Omega
    /// Crews lab (<c>Docs/Studios/VesselStudio/omega.html</c>) is the record: each field under "The crew's lab numbers"
    /// carries the page's label as its tooltip and its SHIPPED value as its default (/artifact-to-unity); the members,
    /// the crew weapons and the omega flight live in <see cref="OmegaMath"/>'s tables, which are the page's. Read by
    /// <see cref="OmegaCrew"/>; design and port notes: <c>Assets/_Scripts/Controller/Vessel/Crew/OMEGA_CREW.md</c>.
    /// </summary>
    [CreateAssetMenu(fileName = "OmegaCrewConfig", menuName = "ScriptableObjects/Vessel Actions/Omega Crew Config")]
    public sealed class OmegaCrewConfigSO : ScriptableObject
    {
        [Header("Crew")]
        [Tooltip("Which of the three crews this hull is: Warden (Stoat, Thresher, Butterfly: the Ripple whip), Bulwark (Rhino, Grizzly, Scarab: the Siege cannon), Tempest (Squirrel, Sparrow, Manta: the Pulsefire battery). The members, their moves and the omega flight are OmegaMath's tables (the page's).")]
        [SerializeField] OmegaMath.Crew crew = OmegaMath.Crew.Warden;

        [Header("The crew's lab numbers (omega.html SPEC: every one a proposal)")]
        [Tooltip("Combine time: Seconds the members take to dock into the omega form once gathered")]
        [SerializeField, Min(0.1f)] float combineSeconds = 1.6f;
        [Tooltip("Gather range: Every member must be within this many u of the one holding LT for the combine to start")]
        [SerializeField, Min(1f)] float gatherRange = 140f;
        [Tooltip("Meter per crystal: How much of the omega meter a collected crystal adds")]
        [SerializeField, Range(0f, 1f)] float meterPerCrystal = 0.22f;
        [Tooltip("Meter per skim: How much a skim adds (any member, any prism). Not wired yet: the hull's skimmer raises no per-skim hook here (OMEGA_CREW.md section 6).")]
        [SerializeField, Range(0f, 0.1f)] float meterPerSkim = 0.004f;
        [Tooltip("Apart regen: Meter gained per second while the crew flies apart (the recovery after a finisher)")]
        [SerializeField, Range(0f, 0.2f)] float regen = 0.02f;
        [Tooltip("Omega drain: Meter lost per second while combined")]
        [SerializeField, Range(0f, 0.3f)] float drain = 0.05f;
        [Tooltip("Combined crystal pay: How much of a crystal's meter the omega form earns (apart earns it all)")]
        [SerializeField, Range(0f, 1f)] float combinedCrystal = 0.5f;
        [Tooltip("Shield drain: Extra meter lost per second while Core holds the shield")]
        [SerializeField, Range(0f, 0.3f)] float shieldDrain = 0.06f;
        [Tooltip("Combine at: The meter a combine needs (and the AI crew waits for)")]
        [SerializeField, Range(0f, 1f)] float combineAt = 0.3f;
        [Tooltip("Finisher at: The meter Core needs to fire the finisher")]
        [SerializeField, Range(0f, 1f)] float finisherAt = 0.9f;
        [Tooltip("Finisher reach: How far ahead the finisher's cone destroys everything (u)")]
        [SerializeField, Min(10f)] float finisherCone = 180f;
        [Tooltip("Finisher cost: Meter the finisher spends; the crew splits as it fires")]
        [SerializeField, Range(0f, 1f)] float finisherCost = 0.7f;
        [Tooltip("Split time: Seconds Helm holds LT to split on purpose")]
        [SerializeField, Min(0.1f)] float splitSeconds = 0.5f;
        [Tooltip("Omega contact plough (the page's omegaContactPlough): on, the omega form destroys the mass it touches instead of taking a hull contact")]
        [SerializeField] bool omegaContactPlough = true;
        [Tooltip("The meter the crew starts with (the page starts every run at 0.5).")]
        [SerializeField, Range(0f, 1f)] float startMeter = 0.5f;

        [Header("Game-side (not on the page; OMEGA_CREW.md section 3)")]
        [Tooltip("The omega form's contact radius for the plough (u): the page's omega reach of 7 u round the body.")]
        [SerializeField, Min(1f)] float ploughRadius = 7f;
        [Tooltip("A cannon shot (a member's or the Siege cannon) bursts this far along its aim (u). The page flies the shell at its speed until it meets mass; here it bursts at this range at once.")]
        [SerializeField, Min(10f)] float cannonRange = 140f;
        [Tooltip("A boost-kind move with no page boost of its own (the Butterfly's gate, the Manta's tunnel) is this boost for one second here.")]
        [SerializeField, Min(1f)] float foldBoost = 1.5f;
        [Tooltip("The march formation (Yash, 2026-10-26): how far the attacker rides to the mobility vessel's right and the defender to its left (u).")]
        [SerializeField, Min(5f)] float formationSpread = 18f;
        [Tooltip("The march formation: how far behind the mobility vessel the attacker and the defender ride (u).")]
        [SerializeField, Min(0f)] float formationBack = 10f;
        [Tooltip("The defence seat's shield: every hostile prism within this of the formation's centre dies while LT is held (u).")]
        [SerializeField, Min(5f)] float shieldRadius = 40f;
        [Tooltip("The defence seat's ward burst (RT): a one-shot sweep half again as wide as the shield; this much meter it costs.")]
        [SerializeField, Range(0f, 1f)] float wardCost = 0.2f;
        [Tooltip("Seconds between ward bursts.")]
        [SerializeField, Min(0f)] float wardCooldown = 3f;
        [Tooltip("The Arms seat's mouse aim (radians per pixel of mouse travel).")]
        [SerializeField, Min(0f)] float mouseAimRadiansPerPixel = 0.0035f;
        [Tooltip("How far ahead an AI Arms seat looks for mass to aim at (u).")]
        [SerializeField, Min(10f)] float aiAimRange = 120f;
        [Tooltip("Hostile prisms about to meet the formation that make an AI defender raise the shield.")]
        [SerializeField, Min(1)] int aiShieldMass = 3;
        [Tooltip("Hostile prisms about to meet the formation that make an AI defender fire the ward burst.")]
        [SerializeField, Min(1)] int aiFinisherMass = 6;
        [Tooltip("Speed a smashed prism's debris leaves at (u/s), and its cap (the Orca's sniper numbers).")]
        [SerializeField, Min(0f)] float debrisSpeed = 90f;
        [SerializeField, Min(0f)] float debrisSpeedLimit = 120f;

        [Header("Explosions")]
        [Tooltip("The ward burst's blast (AOEExplosion prefabs), spawned at the formation's centre and scaled to its sweep.")]
        [SerializeField] AOEExplosion[] finisherExplosions;
        [Tooltip("A cannon shot's burst (a member's or the Siege cannon), scaled to its blast radius.")]
        [SerializeField] AOEExplosion[] cannonExplosions;

        [Header("Sounds (FMOD; every slot ships empty: an unwired slot is a visible TODO)")]
        [SerializeField] EventReference combineSound;
        [SerializeField] EventReference splitSound;
        [SerializeField] EventReference finisherSound;
        [SerializeField] EventReference weaponSound;
        [SerializeField] EventReference moveSound;

        public OmegaMath.Crew Crew => crew;
        public OmegaMath.CrewDef Def => OmegaMath.CrewOf(crew);

        /// <summary>The lab numbers as one block, for <see cref="OmegaMath"/>.</summary>
        public OmegaMath.Params Params => new()
        {
            CombineSeconds = combineSeconds, GatherRange = gatherRange, MeterPerCrystal = meterPerCrystal, MeterPerSkim = meterPerSkim,
            Regen = regen, Drain = drain, CombinedCrystal = combinedCrystal, ShieldDrain = shieldDrain, CombineAt = combineAt,
            FinisherAt = finisherAt, FinisherCone = finisherCone, FinisherCost = finisherCost, SplitSeconds = splitSeconds,
            OmegaContactPlough = omegaContactPlough,
        };

        public float StartMeter => startMeter;
        public float PloughRadius => ploughRadius;
        public float CannonRange => cannonRange;
        public float FoldBoost => foldBoost;
        public float FormationSpread => formationSpread;
        public float FormationBack => formationBack;
        public float ShieldRadius => shieldRadius;
        public float WardCost => wardCost;
        public float WardCooldown => wardCooldown;
        public float MouseAimRadiansPerPixel => mouseAimRadiansPerPixel;
        public float AiAimRange => aiAimRange;
        public int AiShieldMass => aiShieldMass;
        public int AiFinisherMass => aiFinisherMass;
        public float DebrisSpeed => debrisSpeed;
        public float DebrisSpeedLimit => debrisSpeedLimit;
        public AOEExplosion[] FinisherExplosions => finisherExplosions;
        public AOEExplosion[] CannonExplosions => cannonExplosions;
        public EventReference CombineSound => combineSound;
        public EventReference SplitSound => splitSound;
        public EventReference FinisherSound => finisherSound;
        public EventReference WeaponSound => weaponSound;
        public EventReference MoveSound => moveSound;
    }
}
