using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using FMODUnity;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Every number of the Orca's crew (the 4-seat vessel: one helm, three gunner seats; Crew of Four Lab round 5,
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

        [Header("Helm: shield (prisms round the hull, the forward cone open: carried super-shielded, then dropped as danger)")]
        [Tooltip("lab shieldBlocks: danger prisms in the shield (DangerHemisphereConfig: 12 rays x 12 blocks).")]
        [SerializeField, Min(1)] int shieldBlocks = 144;
        [Tooltip("lab shieldMinR: nearest a shield prism stands from the hull (u). DangerHemisphereConfig minRadius.")]
        [SerializeField, Min(0f)] float shieldMinRadius = 50f;
        [Tooltip("lab shieldMaxR: farthest a shield prism stands from the hull (u). DangerHemisphereConfig maxRadius.")]
        [SerializeField, Min(0f)] float shieldMaxRadius = 70f;
        [Tooltip("lab shieldOpenDeg: half-angle of the open cone ahead of the hull (deg), so the helm can see and fly out.")]
        [SerializeField, Range(0f, 90f)] float shieldOpenDeg = 45f;
        [Tooltip("lab shieldTime x Mass: seconds the shield rides with the hull. 4 at rest; the lab's kMass 0.8 is 6.13 at level 10. Then it drops.")]
        [SerializeField] ElementalFloat shieldSeconds = ElementalFloat.Multiplier(4f, 6.1333333f, Element.Mass, 0.5f);
        [Tooltip("lab shieldCd: seconds between shields, counted from the raise.")]
        [SerializeField, Min(0f)] float shieldCooldown = 9f;
        [Tooltip("DangerHemisphereConfig baseBlockScale: each shield prism's size (u).")]
        [SerializeField] Vector3 shieldBlockScale = new(10f, 1f, 1f);
        [Tooltip("lab shieldSuper (round 5, Garrett 2026-10-21): on, the prisms ride with the hull as SUPER-SHIELDED prisms (a wall nothing but a sniper breaks), then each turns into a danger prism where it is and stays. Off: round 4's carried danger prisms that vanish.")]
        [SerializeField] bool shieldSuper = true;
        [Tooltip("lab shieldLinger: seconds the dropped danger prisms stay where they fell (0 = until something breaks them).")]
        [SerializeField, Min(0f)] float shieldLinger = 20f;

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

        [Header("Magazines (one per gunner seat: \"not a shared pool\", Garrett 2026-10-21)")]
        [Tooltip("lab startMissiles: each gunner seat's missiles at spawn.")]
        [SerializeField, Min(0)] int startMissiles = 2;
        [Tooltip("lab maxMissiles: each seat's magazine size.")]
        [SerializeField, Min(1)] int maxMissiles = 4;
        [Tooltip("lab omniMissiles: missiles an omni crystal adds to EVERY gunner seat, whoever collects it (a missile or the hull).")]
        [SerializeField, Min(0)] int omniMissiles = 1;

        [Header("Guns (every gunner seat has one, each different: Garrett 2026-10-21)")]
        [Tooltip("lab gunsOn: off = missiles only (round 4).")]
        [SerializeField] bool gunsOn = true;

        [Header("Gunner 1: the sniper (the Serpent's sniper shot, SniperShotAction.asset)")]
        [Tooltip("lab sniperCd: seconds between shots (the Serpent's 12).")]
        [SerializeField, Min(0.1f)] float sniperCooldown = 12f;
        [Tooltip("lab sniperRange: the cone's length (u).")]
        [SerializeField, Min(1f)] float sniperRange = 3000f;
        [Tooltip("lab sniperCone: the cone's half-angle (deg). Every hostile prism inside dies, super-shielded ones included; the pierce is unlimited.")]
        [SerializeField, Range(0.05f, 10f)] float sniperConeDeg = 1.5f;
        [Tooltip("lab sniperMinR: the cone is never thinner than this (u).")]
        [SerializeField, Min(0f)] float sniperMinPathRadius = 6f;
        [Tooltip("lab sniperPetals / 40: each rival hull in the cone loses this much of every element, ejected as crystals (0.1 = one petal each; a scored hit).")]
        [SerializeField, Min(0f)] float sniperStripPerElement = 0.1f;
        [Tooltip("SniperShotAction vesselEjectSpeed: how fast the stripped crystals leave the victim (u/s).")]
        [SerializeField, Min(0f)] float sniperEjectSpeed = 45f;
        [Tooltip("SniperShotAction sameVictimCooldownSeconds.")]
        [SerializeField, Min(0f)] float sniperSameVictimCooldown = 0.5f;
        [Tooltip("SniperShotAction debrisSpeed / debrisSpeedLimit (u/s).")]
        [SerializeField, Min(0f)] float sniperDebrisSpeed = 90f;
        [SerializeField, Min(0f)] float sniperDebrisSpeedLimit = 120f;
        [Tooltip("SniperShotAction beamStartWidth, beamSeconds, impactFlareRadius, impactFlareSeconds: the tracer (SniperBeam).")]
        [SerializeField, Min(0f)] float sniperBeamStartWidth = 1.5f;
        [SerializeField, Min(0f)] float sniperBeamSeconds = 0.35f;
        [SerializeField, Min(0f)] float sniperFlareRadius = 18f;
        [SerializeField, Min(0f)] float sniperFlareSeconds = 0.3f;

        [Header("Gunner 2: the fast gun (the Brittle Star's rounds, BrittleStarConfig)")]
        [Tooltip("lab rapidRate: rounds per second while held (the Brittle Star's gun fires 90; the lab's 30).")]
        [SerializeField, Min(0.1f)] float fastGunRate = 30f;
        [Tooltip("lab rapidSpeed: round speed (u/s), plus the hull's velocity (the Brittle Star's 135).")]
        [SerializeField, Min(1f)] float fastGunRoundSpeed = 135f;
        [Tooltip("lab rapidLife: round flight time (s; the Brittle Star's 4.5). A round slows as it flies, reaching speed x 2T / pi.")]
        [SerializeField, Min(0.05f)] float fastGunRoundSeconds = 4.5f;
        [Tooltip("BrittleStarConfig projectileScale and roundGrowthFactor: the round's size and how much it grows in flight.")]
        [SerializeField, Min(0.01f)] float fastGunRoundScale = 1f;
        [SerializeField, Min(1f)] float fastGunRoundGrowth = 3f;
        [Tooltip("lab rapidHeatTime: seconds of fire before it overheats.")]
        [SerializeField, Min(0.05f)] float fastGunHeatSeconds = 6f;
        [Tooltip("lab rapidCoolTime: seconds it is locked out once overheated (it fires again only when cool).")]
        [SerializeField, Min(0.05f)] float fastGunCoolSeconds = 5f;

        [Header("Gunner 3: the bomb (the Grizzly's trigger bomb, without its self-launch)")]
        [Tooltip("lab bombSpeed: u/s, plus the hull's velocity (GrizzlyTriggerBombConfig's 90).")]
        [SerializeField, Min(1f)] float bombSpeed = 90f;
        [Tooltip("lab bombLife: the fuse (s). It bursts on a rival hull or a hostile prism, on a second press, or at the fuse. It never knocks our own hull about.")]
        [SerializeField, Min(0.1f)] float bombFuse = 4f;
        [Tooltip("lab bombBlastR x Space: the blast radius (u). 50 at rest (the Grizzly's mid-charge MaxScale 100); the lab's kSpace 0.8 is 76.7 at level 10.")]
        [SerializeField] ElementalFloat bombBlastRadius = ElementalFloat.Multiplier(50f, 76.666667f, Element.Space, 10f);
        [Tooltip("lab bombCd: seconds between bombs, from the throw.")]
        [SerializeField, Min(0f)] float bombCooldown = 2.5f;
        [Tooltip("The bomb's projectile scale (GrizzlyTriggerBombConfig 2.5-5 by charge; the middle).")]
        [SerializeField, Min(0.1f)] float bombScale = 3.75f;
        [Tooltip("The bomb's colour (the Grizzly's danger red).")]
        [SerializeField] Color bombColor = new(1f, 0.072f, 0.105f, 1f);

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
        [Tooltip("The bomb's blast: AOEGrizzlyExplosion.prefab (the Grizzly's), called with AffectSelfOverride off and never the Grizzly's self-launch.")]
        [SerializeField] AOEExplosion[] bombExplosions;
        [Tooltip("The match's players, for the AI gunners' rival hulls and the missiles' hull contacts; its theme colours the sniper's tracer.")]
        [SerializeField] GameDataSO gameData;
        [Tooltip("The scoreboard's channel: a sniper strip is a scored hit (the Serpent's).")]
        [SerializeField] ScriptableEventCombatHitStats onCombatHitLanded;

        [Header("Sounds (ship empty: an unwired slot is a visible TODO)")]
        [SerializeField] EventReference missileLaunchSound;
        [SerializeField] EventReference missileBurstSound;
        [SerializeField] EventReference shieldRaiseSound;
        [SerializeField] EventReference omniRefillSound;
        [SerializeField] EventReference emptyMagazineSound;
        [SerializeField] EventReference sniperShotSound;
        [SerializeField] EventReference bombThrowSound;
        [SerializeField] EventReference bombBurstSound;

        [Header("Text (player-facing; a blank field shows nothing)")]
        [Tooltip("The seat line on screen while you hold a seat. Tokens: {seat} the seat's name, {gun} its gun's name and state, {count} its missiles.")]
        [SerializeField] string seatLineTemplate = "{seat}  ·  {gun}  ·  {count} missiles  ·  1-4 to change seat";
        [Tooltip("The seats' names, helm first.")]
        [SerializeField] string[] seatNames = { "Helm", "Gunner 1", "Gunner 2", "Gunner 3" };
        [Tooltip("The guns' names, by seat (the helm's is unused).")]
        [SerializeField] string[] gunNames = { "", "Sniper", "Fast gun", "Bomb" };
        [Tooltip("{gun} for a gun: {name} and {state}.")]
        [SerializeField] string gunLineTemplate = "{name} {state}";
        [Tooltip("A gun's states: ready, cooling down ({seconds}), overheated, and a bomb in flight.")]
        [SerializeField] string gunReadyText = "ready";
        [SerializeField] string gunCooldownTemplate = "{seconds}s";
        [SerializeField] string gunHotText = "overheated";
        [SerializeField] string bombOutText = "bomb out";

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
        public bool ShieldSuper => shieldSuper;
        public float ShieldLinger => shieldLinger;
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
        public bool GunsOn => gunsOn;
        public float SniperCooldown => sniperCooldown;
        public float SniperRange => sniperRange;
        public float SniperConeDeg => sniperConeDeg;
        public float SniperMinPathRadius => sniperMinPathRadius;
        public float SniperStripPerElement => sniperStripPerElement;
        public float SniperEjectSpeed => sniperEjectSpeed;
        public float SniperSameVictimCooldown => sniperSameVictimCooldown;
        public float SniperDebrisSpeed => sniperDebrisSpeed;
        public float SniperDebrisSpeedLimit => sniperDebrisSpeedLimit;
        public float SniperBeamStartWidth => sniperBeamStartWidth;
        public float SniperBeamSeconds => sniperBeamSeconds;
        public float SniperFlareRadius => sniperFlareRadius;
        public float SniperFlareSeconds => sniperFlareSeconds;
        public float FastGunRate => fastGunRate;
        public float FastGunRoundSpeed => fastGunRoundSpeed;
        public float FastGunRoundSeconds => fastGunRoundSeconds;
        public float FastGunRoundScale => fastGunRoundScale;
        public float FastGunRoundGrowth => fastGunRoundGrowth;
        public float FastGunHeatSeconds => fastGunHeatSeconds;
        public float FastGunCoolSeconds => fastGunCoolSeconds;
        public float BombSpeed => bombSpeed;
        public float BombFuse => bombFuse;
        public float BombBlastRadiusAt(IVesselStatus status) => bombBlastRadius.EvaluateLive(status);
        public float BombCooldown => bombCooldown;
        public float BombScale => bombScale;
        public Color BombColor => bombColor;
        public AOEExplosion[] BombExplosions => bombExplosions;
        public ScriptableEventCombatHitStats OnCombatHitLanded => onCombatHitLanded;
        public EventReference SniperShotSound => sniperShotSound;
        public EventReference BombThrowSound => bombThrowSound;
        public EventReference BombBurstSound => bombBurstSound;
        public string GunName(int seat) => gunNames != null && seat >= 0 && seat < gunNames.Length ? gunNames[seat] : string.Empty;
        public string GunLineTemplate => gunLineTemplate;
        public string GunReadyText => gunReadyText;
        public string GunCooldownTemplate => gunCooldownTemplate;
        public string GunHotText => gunHotText;
        public string BombOutText => bombOutText;
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
            MaxMissiles = maxMissiles, OmniMissiles = omniMissiles,
            ShieldBlocks = shieldBlocks, ShieldOpenDeg = shieldOpenDeg, ShieldMinRadius = shieldMinRadius, ShieldMaxRadius = ShieldMaxRadius,
        };
    }
}
