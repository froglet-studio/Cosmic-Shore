using CosmicShore.Data;
using CosmicShore.Utility;
using FMODUnity;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Every number of the Orca's crew (the 4-seat vessel: one helm, three gunner seats; Crew of Four Lab round 4,
    /// <c>Docs/Studios/CREW_OF_FOUR_LAB.md</c>). The lab is the record: each field names the lab key it came from, and
    /// <c>Tools/Build/orca_crew_lab/parity.cjs</c> holds the shipped asset to the lab's SHIPPED block. Read by
    /// <see cref="OrcaCrew"/>; see <c>Assets/_Scripts/Controller/Vessel/Crew/ORCA_CREW.md</c>.
    /// </summary>
    [CreateAssetMenu(fileName = "OrcaCrewConfig", menuName = "ScriptableObjects/Vessel Actions/Orca Crew Config")]
    public sealed class OrcaCrewConfigSO : ScriptableObject
    {
        [Header("Hull")]
        [Tooltip("lab hullRadius: the hull's contact radius (u). The turret mounts sit at multiples of it, and a missile that passes this near a rival hull bursts on it.")]
        [SerializeField, Min(1f)] float hullRadius = 9f;

        [Header("Helm: boost")]
        [Tooltip("lab boostTank: seconds of boost the tank holds. The helm boosts on the right trigger (right Shift) while the tank has any.")]
        [SerializeField, Min(0f)] float boostTank = 3f;
        [Tooltip("lab boostRefill x Time: tank seconds regained per second while not boosting. 0.25 at rest; the lab's kTime 0.6 " +
                 "at level 15 is 0.35 at level 10 (the lab's 1 + k x level / 15 is this channel's line through 1 and 1 + 2k/3).")]
        [SerializeField] ElementalFloat boostRefill = ElementalFloat.Multiplier(0.25f, 0.35f, Element.Time, 0.05f);

        [Header("Helm: shield (danger prisms round the hull, the forward cone open)")]
        [Tooltip("lab shieldBlocks: danger prisms in the shield (DangerHemisphereConfig: 12 rays x 12 blocks).")]
        [SerializeField, Min(1)] int shieldBlocks = 144;
        [Tooltip("lab shieldMinR: nearest a shield prism stands from the hull (u). DangerHemisphereConfig minRadius.")]
        [SerializeField, Min(0f)] float shieldMinRadius = 50f;
        [Tooltip("lab shieldMaxR: farthest a shield prism stands from the hull (u). DangerHemisphereConfig maxRadius.")]
        [SerializeField, Min(0f)] float shieldMaxRadius = 70f;
        [Tooltip("lab shieldOpenDeg: half-angle of the open cone ahead of the hull (deg), so the helm can see and fly out.")]
        [SerializeField, Range(0f, 90f)] float shieldOpenDeg = 45f;
        [Tooltip("lab shieldTime x Mass: seconds the shield stands. 4 at rest; the lab's kMass 0.8 is 6.13 at level 10.")]
        [SerializeField] ElementalFloat shieldSeconds = ElementalFloat.Multiplier(4f, 6.1333333f, Element.Mass, 0.5f);
        [Tooltip("lab shieldCd: seconds between shields, counted from the raise.")]
        [SerializeField, Min(0f)] float shieldCooldown = 9f;
        [Tooltip("DangerHemisphereConfig baseBlockScale: each shield prism's size (u).")]
        [SerializeField] Vector3 shieldBlockScale = new(10f, 1f, 1f);

        [Header("Gunner turrets (gyro-stabilized, on outriggers)")]
        [Tooltip("lab mountSmooth: seconds for the outrigger mounts to follow the hull's turns (where the eye sits lags; the aim never does).")]
        [SerializeField, Min(0.005f)] float mountSmooth = 0.35f;
        [Tooltip("lab horizonSmooth: seconds for a turret view's held horizon to drift back to the hull's up.")]
        [SerializeField, Min(0.05f)] float horizonSmooth = 3f;
        [Tooltip("lab turretTurn: turret slew at full stick, a person's or the AI's (deg/s).")]
        [SerializeField, Min(1f)] float turretTurn = 160f;
        [Tooltip("lab sens: a person's mouse aim (radians per pixel of mouse travel).")]
        [SerializeField, Min(0f)] float mouseAimRadiansPerPixel = 0.0035f;

        [Header("Missiles (flown by the gunner that released them)")]
        [Tooltip("lab missileSpeed: u/s at Charge 0 (x missileScale).")]
        [SerializeField, Min(1f)] float missileSpeed = 180f;
        [Tooltip("lab missileTurn: deg/s at full stick, at Charge 0 (x missileScale).")]
        [SerializeField, Min(1f)] float missileTurn = 150f;
        [Tooltip("lab kCharge: missile speed and turn both scale by this. 1 at rest; the lab's kCharge 0.6 is 1.4 at level 10.")]
        [SerializeField] ElementalFloat missileScale = ElementalFloat.Multiplier(1f, 1.4f, Element.Charge, 0.5f);
        [Tooltip("lab missileLife: a missile bursts this long after launch (s). Garrett 2026-10-20: \"explode on contact or after 5 seconds\".")]
        [SerializeField, Min(0.25f)] float missileLife = 5f;
        [Tooltip("lab launchCd: a turret's reload after its missile ends (s).")]
        [SerializeField, Min(0f)] float launchCooldown = 0.6f;
        [Tooltip("lab blastR x Space: the burst's radius (u). 30 at rest (AOEExplosion MaxScale 60 is twice it); the lab's kSpace 0.8 is 46 at level 10.")]
        [SerializeField] ElementalFloat blastRadius = ElementalFloat.Multiplier(30f, 46f, Element.Space, 5f);
        [Tooltip("lab blastTime: seconds the burst takes to reach full size (AOEExplosion's own ExplosionDuration is 3 s). What it takes from a rival hull is the explosion's own vessel effect (VesselCombatHitByMissileBlast: petals, a scored hit).")]
        [SerializeField, Min(0.05f)] float blastSeconds = 0.5f;
        [Tooltip("lab: a crystal within this of a flown missile is collected for the hull (u; the lab's 81 = 9 squared).")]
        [SerializeField, Min(0.5f)] float missileCollectRadius = 9f;
        [Tooltip("lab: a hostile prism within this of a missile's path bursts it (u; the lab's 12 = 3.46 squared).")]
        [SerializeField, Min(0.5f)] float missileContactRadius = 3.46f;

        [Header("Magazine (shared by the three gunners)")]
        [Tooltip("lab startMissiles: missiles in the magazine at spawn.")]
        [SerializeField, Min(0)] int startMissiles = 6;
        [Tooltip("lab maxMissiles: the magazine's size.")]
        [SerializeField, Min(1)] int maxMissiles = 12;
        [Tooltip("lab omniMissiles: missiles an omni crystal adds, whoever collects it (a missile or the hull).")]
        [SerializeField, Min(0)] int omniMissiles = 3;
        [Tooltip("lab aiReserve: missiles AI gunners leave in the magazine for each human gunner aboard.")]
        [SerializeField, Min(0)] int aiReserve = 1;

        [Header("AI seats")]
        [Tooltip("The AI crew's skill (lab SKILL): 0 Easy, 1 Medium, 2 Hard. Every seat no person holds is flown by it.")]
        [SerializeField, Range(0, 2)] int aiSkill = 2;
        [Tooltip("How often an AI gunner re-picks its target (s; the lab's 0.4).")]
        [SerializeField, Min(0.05f)] float aiRetargetSeconds = 0.4f;

        [Header("Cameras")]
        [Tooltip("lab turretFov: the first-person turret camera's field of view (deg).")]
        [SerializeField, Range(20f, 120f)] float turretFov = 75f;
        [Tooltip("lab missileCamBack: the camera behind a flown missile (u).")]
        [SerializeField, Min(0f)] float missileCamBack = 14f;
        [Tooltip("lab missileCamUp: and above it (u).")]
        [SerializeField, Min(0f)] float missileCamUp = 3f;

        [Header("Wiring")]
        [Tooltip("The burst: AOEExplosion.prefab, as the Wrasse's Space blast. Spares our own domain; its vessel effect drains a rival hull's petals and scores the hit.")]
        [SerializeField] AOEExplosion[] explosions;
        [Tooltip("The match's players, for the AI gunners' rival hulls and the missiles' hull contacts.")]
        [SerializeField] GameDataSO gameData;

        [Header("Sounds (ship empty: an unwired slot is a visible TODO)")]
        [SerializeField] EventReference missileLaunchSound;
        [SerializeField] EventReference missileBurstSound;
        [SerializeField] EventReference shieldRaiseSound;
        [SerializeField] EventReference omniRefillSound;
        [SerializeField] EventReference emptyMagazineSound;

        [Header("Text (player-facing; a blank field shows nothing)")]
        [Tooltip("The seat line on screen while you hold a seat. Tokens: {seat} the seat's name, {count} missiles in the magazine.")]
        [SerializeField] string seatLineTemplate = "{seat}  ·  {count} missiles  ·  1-4 to change seat";
        [Tooltip("The seats' names, helm first.")]
        [SerializeField] string[] seatNames = { "Helm", "Gunner 1", "Gunner 2", "Gunner 3" };

        public float HullRadius => hullRadius;
        public float BoostTank => boostTank;
        public float BoostRefillAt(IVesselStatus status) => boostRefill.EvaluateLive(status);
        public int ShieldBlocks => shieldBlocks;
        public float ShieldMinRadius => shieldMinRadius;
        public float ShieldMaxRadius => Mathf.Max(shieldMinRadius, shieldMaxRadius);
        public float ShieldOpenDeg => shieldOpenDeg;
        public float ShieldSecondsAt(IVesselStatus status) => shieldSeconds.EvaluateLive(status);
        public float ShieldCooldown => shieldCooldown;
        public Vector3 ShieldBlockScale => shieldBlockScale;
        public float MountSmooth => mountSmooth;
        public float HorizonSmooth => horizonSmooth;
        public float TurretTurn => turretTurn;
        public float MouseAimRadiansPerPixel => mouseAimRadiansPerPixel;
        public float MissileSpeed => missileSpeed;
        public float MissileTurn => missileTurn;
        public float MissileLife => missileLife;
        public float LaunchCooldown => launchCooldown;
        public float BlastRadiusAt(IVesselStatus status) => blastRadius.EvaluateLive(status);
        /// <summary>The burst's radius at rest (the AI's prism burst distance).</summary>
        public float BlastRadiusAtRest => blastRadius.Value;
        public float MissileScaleAt(IVesselStatus status) => missileScale.EvaluateLive(status);
        public float BlastSeconds => blastSeconds;
        public float MissileCollectRadius => missileCollectRadius;
        public float MissileContactRadius => missileContactRadius;
        public int StartMissiles => startMissiles;
        public int MaxMissiles => maxMissiles;
        public int OmniMissiles => omniMissiles;
        public int AiReserve => aiReserve;
        public int AiSkill => aiSkill;
        public float AiRetargetSeconds => aiRetargetSeconds;
        public float TurretFov => turretFov;
        public float MissileCamBack => missileCamBack;
        public float MissileCamUp => missileCamUp;
        public AOEExplosion[] Explosions => explosions;
        public GameDataSO GameData => gameData;
        public EventReference MissileLaunchSound => missileLaunchSound;
        public EventReference MissileBurstSound => missileBurstSound;
        public EventReference ShieldRaiseSound => shieldRaiseSound;
        public EventReference OmniRefillSound => omniRefillSound;
        public EventReference EmptyMagazineSound => emptyMagazineSound;
        public string SeatLineTemplate => seatLineTemplate;
        public string SeatName(int seat) => seatNames != null && seat >= 0 && seat < seatNames.Length ? seatNames[seat] : string.Empty;

        /// <summary>The core's numbers, as a plain struct.</summary>
        public OrcaCrewSettings ToSettings() => new()
        {
            HullRadius = hullRadius, MountSmooth = mountSmooth, HorizonSmooth = horizonSmooth, TurretTurnDeg = turretTurn,
            MissileSpeed = missileSpeed, MissileTurnDeg = missileTurn, MissileLife = missileLife, LaunchCooldown = launchCooldown,
            MaxMissiles = maxMissiles, OmniMissiles = omniMissiles, AiReserve = aiReserve,
            ShieldBlocks = shieldBlocks, ShieldOpenDeg = shieldOpenDeg, ShieldMinRadius = shieldMinRadius, ShieldMaxRadius = ShieldMaxRadius,
        };
    }
}
