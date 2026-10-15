using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using FMODUnity;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Every number the Shrike's executor reads (design: <c>R_VesselActions/SHRIKE.md</c>; the source is the Shrike
    /// Studio, <c>Docs/Studios/VesselStudio/shrike.html</c>, whose SHIPPED keys are these field names verbatim and whose
    /// slider labels are these tooltips). One asset, wired directly on the executor, never written at runtime.
    ///
    /// <para><b>The modes and their elements</b> (Garrett, 2026-10-15: the right trigger does the MODE's action, LT
    /// zooms). The element dials are the Notion "Class - Shrike" document's:
    /// <list type="bullet">
    /// <item><b>Space mode — Void Lance</b>: <see cref="LanceRadius"/> scales with CHARGE ("wider radius on void
    /// lance"); the Space upgrade lets it break super-shielded mass.</item>
    /// <item><b>Charge mode — Targeting Array</b> (paint; the flora's missiles fly): <see cref="LockCone"/> scales with
    /// SPACE ("larger lock-on radius").</item>
    /// <item><b>Time mode — Void Sling</b>: <see cref="SlingCooldown"/> scales with TIME.</item>
    /// <item><b>Mass — detach and leave a nest</b>: <see cref="NestBlocks"/> and <see cref="TrailWavelength"/> scale
    /// with MASS.</item>
    /// </list></para>
    ///
    /// <para><b>PvP is petals only (LOCKED, 2026-10-10).</b> The document's lance and missiles slow, steer and switch
    /// off the trail of the vessels they hit; none of that may touch another pilot's vessel. A rival hull hit is one
    /// scored combat hit that moves petals (<see cref="CombatHitDrain"/>).</para>
    /// </summary>
    [CreateAssetMenu(fileName = "ShrikeConfig", menuName = "ScriptableObjects/Vessel Actions/Shrike Config")]
    public sealed class ShrikeConfigSO : ScriptableObject
    {
        // ------------------------------------------------------------------ docking
        [Header("Docking (Vessel Config > Flight)")]
        [Tooltip("Hull contact: the radius at which a touch docks it (\"The Shrike will dock to any block it runs into\").")]
        [SerializeField, Min(0.5f)] float hullRadius = 3f;
        [Tooltip("Dock bonus: cooldowns tick this much faster while docked.")]
        [SerializeField, Min(1f)] float dockBonus = 1.6f;
        [Tooltip("Dock bonus, shielded: the faster rate docked in a nest or on super-shielded mass.")]
        [SerializeField, Min(1f)] float dockBonusShielded = 2.5f;
        [Tooltip("Game only: how far past the hull radius the contact query reaches for a block's centre (it must cover the largest block's half size).")]
        [SerializeField, Min(1f)] float dockQueryReach = 12f;
        [Tooltip("Game only (the page's own constant): seconds after an undock or a sling's start before a touch can dock again.")]
        [SerializeField, Min(0f)] float dockGraceSeconds = 0.5f;
        [Tooltip("Game only (the page's own constant): the push off a surface on undocking, u/s, decaying at 2/s.")]
        [SerializeField, Min(0f)] float undockPush = 8f;
        [Tooltip("Game only (the page's own constant): your own trail younger than this never docks you.")]
        [SerializeField, Min(0f)] float ownTrailGraceSeconds = 1f;

        // ------------------------------------------------------------------ lance
        [Header("Modes and zoom (X Space, RB Charge, B Time; LT zoom)")]
        [Tooltip("Game only: the flight view's field of view at full zoom, degrees.")]
        [SerializeField, Range(5f, 60f)] float zoomFieldOfView = 24f;
        [Tooltip("Game only: seconds the zoom takes to come fully in or out.")]
        [SerializeField, Min(0.01f)] float zoomSeconds = 0.2f;
        [Tooltip("Game only: docked, the chase camera eases back to this multiple of its authored distance (\"after " +
                 "colliding with a prism the camera should move further away\").")]
        [SerializeField, Min(1f)] float dockedCameraPullBack = 1.8f;
        [Tooltip("Game only: how fast the camera eases to and from the docked distance, 1/s.")]
        [SerializeField, Min(0.1f)] float cameraEaseRate = 4f;

        [Header("Void Lance (Space mode, RT held)")]
        [Tooltip("Lance range, u.")]
        [SerializeField, Min(10f)] float lanceRange = 2400f;
        [Tooltip("Lance cooldown, s.")]
        [SerializeField, Min(0.05f)] float lanceCooldown = 1.2f;
        [Tooltip("CHARGE: lance radius, u (lanceRadiusAt0 1.5 -> lanceRadiusAt10 5: \"wider radius on void lance\").")]
        [SerializeField] ElementalFloat lanceRadius = ElementalFloat.Multiplier(1.5f, 5f, Element.Charge, 0.5f);
        [Tooltip("Game only: the debris speed of a block the lance breaks, u/s.")]
        [SerializeField, Min(0f)] float lanceDebrisSpeed = 60f;
        [Tooltip("Game only: how long the beam is drawn, s (0 draws nothing).")]
        [SerializeField, Min(0f)] float lanceBeamSeconds = 0.3f;

        // ------------------------------------------------------------------ sling
        [Header("Void Sling (Time mode, RT)")]
        [Tooltip("Sling range, u: how far the tether reaches.")]
        [SerializeField, Min(10f)] float slingRange = 1600f;
        [Tooltip("Tether speed, u/s.")]
        [SerializeField, Min(10f)] float tetherSpeed = 2400f;
        [Tooltip("Sling seconds: the fixed time the boost takes to reach the anchor.")]
        [SerializeField, Min(0.05f)] float slingSeconds = 1f;
        [Tooltip("TIME: sling cooldown, s (slingCooldownAt0 6 -> slingCooldownAt10 2.5: \"shorter cooldown on void sling\").")]
        [SerializeField] ElementalFloat slingCooldown = ElementalFloat.Multiplier(6f, 2.5f, Element.Time, 1f);
        [Tooltip("Game only (the page's own constant): how fast the nose turns onto the sling's line, 1/s.")]
        [SerializeField, Min(0.1f)] float slingTurnRate = 6f;

        // ------------------------------------------------------------------ array
        [Header("Targeting Array (Charge mode, RT held): vessels, then fauna, then flora, then mass")]
        [Tooltip("Lock range, u.")]
        [SerializeField, Min(10f)] float lockRange = 1500f;
        [Tooltip("SPACE: lock-on cone, degrees off the nose (lockConeAt0 5 -> lockConeAt10 14: \"larger lock-on radius for targeting array\").")]
        [SerializeField] ElementalFloat lockCone = ElementalFloat.Multiplier(5f, 14f, Element.Space, 2f);
        [Tooltip("Missile speed, u/s.")]
        [SerializeField, Min(1f)] float missileSpeed = 220f;
        [Tooltip("Missile turn, degrees per second.")]
        [SerializeField, Min(0f)] float missileTurn = 140f;
        [Tooltip("Missile blast radius, u (\"destroying blocks in a medium radius\").")]
        [SerializeField, Min(0.5f)] float missileBlast = 18f;
        [Tooltip("Missile every, s: one flora's time between missiles while the target is painted.")]
        [SerializeField, Min(0.05f)] float missileEvery = 0.8f;
        [Tooltip("Missile life, s: a missile that hits nothing blows when it runs out.")]
        [SerializeField, Min(0.5f)] float missileLife = 8f;
        [Tooltip("Game only (the page's own constant): an enemy lock holds to this multiple of the cone.")]
        [SerializeField, Min(1f)] float lockKeepScale = 1.6f;
        [Tooltip("Game only (the page's own constant): the radius round the painted block whose blocks' centre is the mass lock, u.")]
        [SerializeField, Min(1f)] float massLockRadius = 35f;

        // ------------------------------------------------------------------ nest
        [Header("Nest (A, docked: detach and leave one behind) - MASS")]
        [Tooltip("MASS: nest blocks (nestBlocksAt0 12 -> nestBlocksAt10 32: \"more blocks in the nest\"), rounded half up.")]
        [SerializeField] ElementalFloat nestBlocks = ElementalFloat.Multiplier(12f, 32f, Element.Mass, 6f);
        [Tooltip("Game only: each nest block's side, u (the page's 4.5). The nest's radius grows until no two blocks touch.")]
        [SerializeField, Min(0.5f)] float nestBlockSize = 4.5f;
        [Tooltip("Game only: extra room between nest blocks past touching (1 = their bounding spheres just meet).")]
        [SerializeField, Range(1f, 2f)] float nestClearance = 1.1f;
        [Tooltip("Nest cooldown, s.")]
        [SerializeField, Min(0f)] float nestCooldown = 8f;
        [Tooltip("Game only: the missiles a left-behind nest's flora carries (\"leave behind a nest with only one missile\").")]
        [SerializeField, Min(0)] int detachNestMissiles = 1;
        [Tooltip("Game only: seconds the nest just left behind cannot catch the hull flying out of it.")]
        [SerializeField, Min(0f)] float leaveNestSeconds = 2f;
        [Tooltip("Max nests: a new one past this retires the oldest.")]
        [SerializeField, Min(1)] int maxNests = 3;

        [Header("Nest flora (a collected crystal, or a left-behind nest's crown) grows the missiles")]
        [Tooltip("Turret regen, s: the time one flora takes to grow a missile (twice as fast while docked in its nest).")]
        [SerializeField, Min(0.1f)] float turretRegen = 6f;
        [Tooltip("Game only: missiles one flora holds when fully grown.")]
        [SerializeField, Range(1, 8)] int floraMissileMax = 4;
        [Tooltip("Game only: flora alive at once; a new one past this withers the oldest.")]
        [SerializeField, Range(1, 32)] int maxFlora = 8;
        [Tooltip("Game only: a missile's length, u, on the flora and in flight (\"large missiles\").")]
        [SerializeField, Min(0.5f)] float missileLength = 4f;
        [Tooltip("Game only: the missile's trail, seconds long and u wide at the missile.")]
        [SerializeField, Min(0f)] float missileTrailSeconds = 0.9f;
        [SerializeField, Min(0f)] float missileTrailWidth = 1.8f;

        // ------------------------------------------------------------------ trail
        [Header("Trail - MASS")]
        [Tooltip("MASS: trail wavelength, u (trailWaveAt0 8 -> trailWaveAt10 4: \"shorter trail wavelength\"). The spawner's authored wavelength is the At0 value.")]
        [SerializeField] ElementalFloat trailWavelength = ElementalFloat.Multiplier(8f, 4f, Element.Mass, 2f);
        [Tooltip("Trail thin: block width cruising, u (\"thin long blocks\").")]
        [SerializeField, Min(0.05f)] float trailThin = 0.6f;
        [Tooltip("Trail thick: block width while slinging, u (\"that get thicker while boosting with the void sling\").")]
        [SerializeField, Min(0.05f)] float trailThick = 2.6f;
        [Tooltip("Trail length: each block's length, u.")]
        [SerializeField, Min(0.1f)] float trailLength = 7f;
        [Tooltip("Game only: most sling blocks laid in one frame; excess owed distance is DROPPED so a hitch never dumps a burst.")]
        [SerializeField, Range(1, 16)] int slingMaxBlocksPerFrame = 6;

        // ------------------------------------------------------------------ scoring
        [Header("Scoring (PvP is petals only)")]
        [SerializeField] CombatHitClass lanceHitClass = CombatHitClass.Bullet;
        [SerializeField] CombatHitClass missileHitClass = CombatHitClass.MissileBlast;
        [Tooltip("At most one scored hit per victim per this many seconds (the page's HIT_WINDOW).")]
        [SerializeField, Min(0f)] float hitCooldown = 0.05f;
        [SerializeField] ScriptableEventCombatHitStats onCombatHitLanded;

        // ------------------------------------------------------------------ AI
        [Header("Autopilot (input-only)")]
        [Tooltip("Rival blocks on the lance's line that make the AI hold the trigger.")]
        [SerializeField, Min(1)] int aiLanceMinTargets = 2;
        [Tooltip("Seconds the AI stays docked before it presses Mass to detach (and leave a nest).")]
        [SerializeField, Min(0f)] float aiDockSeconds = 3f;
        [Tooltip("The AI slings when its target is at least this far, u, and inside aiSlingDegrees of the nose.")]
        [SerializeField, Min(0f)] float aiSlingMinDistance = 300f;
        [SerializeField, Range(1f, 90f)] float aiSlingDegrees = 20f;

        // ------------------------------------------------------------------ audio (exposed, shipped EMPTY)
        [Header("Audio (FMOD) - every slot ships empty")]
        [SerializeField] EventReference modeEvent;
        [SerializeField] EventReference lanceEvent;
        [SerializeField] EventReference tetherEvent;
        [SerializeField] EventReference slingEvent;
        [SerializeField] EventReference dockEvent;
        [SerializeField] EventReference undockEvent;
        [SerializeField] EventReference nestEvent;
        [SerializeField] EventReference lockEvent;
        [SerializeField] EventReference missileLaunchEvent;
        [SerializeField] EventReference missileBlastEvent;
        [SerializeField] EventReference crystalEvent;

        public float ZoomFieldOfView => zoomFieldOfView;
        public float ZoomSeconds => zoomSeconds;
        public float DockedCameraPullBack => dockedCameraPullBack;
        public float CameraEaseRate => cameraEaseRate;
        public float HullRadius => hullRadius;
        public float DockBonus => dockBonus;
        public float DockBonusShielded => dockBonusShielded;
        public float DockQueryReach => dockQueryReach;
        public float DockGraceSeconds => dockGraceSeconds;
        public float UndockPush => undockPush;
        public float OwnTrailGraceSeconds => ownTrailGraceSeconds;
        public float LanceRange => lanceRange;
        public float LanceCooldown => lanceCooldown;
        public ElementalFloat LanceRadius => lanceRadius;
        public float LanceDebrisSpeed => lanceDebrisSpeed;
        public float LanceBeamSeconds => lanceBeamSeconds;
        public float SlingRange => slingRange;
        public float TetherSpeed => tetherSpeed;
        public float SlingSeconds => slingSeconds;
        public ElementalFloat SlingCooldown => slingCooldown;
        public float SlingTurnRate => slingTurnRate;
        public float LockRange => lockRange;
        public ElementalFloat LockCone => lockCone;
        public float MissileSpeed => missileSpeed;
        public float MissileTurn => missileTurn;
        public float MissileBlast => missileBlast;
        public float MissileEvery => missileEvery;
        public float MissileLife => missileLife;
        public float LockKeepScale => lockKeepScale;
        public float MassLockRadius => massLockRadius;
        public ElementalFloat NestBlocks => nestBlocks;
        public float NestBlockSize => nestBlockSize;
        public float NestClearance => nestClearance;
        public float NestCooldown => nestCooldown;
        public int DetachNestMissiles => detachNestMissiles;
        public float LeaveNestSeconds => leaveNestSeconds;
        public int MaxNests => maxNests;
        public float TurretRegen => turretRegen;
        public int FloraMissileMax => floraMissileMax;
        public int MaxFlora => maxFlora;
        public float MissileLength => missileLength;
        public float MissileTrailSeconds => missileTrailSeconds;
        public float MissileTrailWidth => missileTrailWidth;
        public ElementalFloat TrailWavelength => trailWavelength;
        public float TrailThin => trailThin;
        public float TrailThick => trailThick;
        public float TrailLength => trailLength;
        public int SlingMaxBlocksPerFrame => slingMaxBlocksPerFrame;
        public CombatHitClass LanceHitClass => lanceHitClass;
        public CombatHitClass MissileHitClass => missileHitClass;
        public float HitCooldown => hitCooldown;
        public ScriptableEventCombatHitStats OnCombatHitLanded => onCombatHitLanded;
        public int AiLanceMinTargets => aiLanceMinTargets;
        public float AiDockSeconds => aiDockSeconds;
        public float AiSlingMinDistance => aiSlingMinDistance;
        public float AiSlingDegrees => aiSlingDegrees;
        public EventReference ModeEvent => modeEvent;
        public EventReference LanceEvent => lanceEvent;
        public EventReference TetherEvent => tetherEvent;
        public EventReference SlingEvent => slingEvent;
        public EventReference DockEvent => dockEvent;
        public EventReference UndockEvent => undockEvent;
        public EventReference NestEvent => nestEvent;
        public EventReference LockEvent => lockEvent;
        public EventReference MissileLaunchEvent => missileLaunchEvent;
        public EventReference MissileBlastEvent => missileBlastEvent;
        public EventReference CrystalEvent => crystalEvent;
    }
}
