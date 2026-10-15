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
    /// <para><b>One element per ability</b> (the four-icon row, charge → mass → space → time), the Notion
    /// "Class - Shrike" document's mapping:
    /// <list type="bullet">
    /// <item><b>Charge — Void Lance</b> ("wider radius on void lance"): <see cref="LanceRadius"/>.</item>
    /// <item><b>Mass — Nest</b> ("more blocks in the nest", "shorter trail wavelength"): <see cref="NestBlocks"/>,
    /// <see cref="TrailWavelength"/>.</item>
    /// <item><b>Space — Targeting Array</b> ("larger lock-on radius for targeting array"): <see cref="LockCone"/>.</item>
    /// <item><b>Time — Void Sling</b> ("shorter cooldown on void sling"): <see cref="SlingCooldown"/>.</item>
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
        [Header("Void Lance (RT, held) - CHARGE")]
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
        [Header("Void Sling (B) - TIME")]
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
        [Header("Targeting Array (LT, held) - SPACE")]
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
        [Tooltip("Missile every, s: one nest's time between missiles.")]
        [SerializeField, Min(0.05f)] float missileEvery = 0.8f;
        [Tooltip("Missile life, s: a missile that hits nothing blows when it runs out.")]
        [SerializeField, Min(0.5f)] float missileLife = 8f;
        [Tooltip("Game only (the page's own constant): an enemy lock holds to this multiple of the cone.")]
        [SerializeField, Min(1f)] float lockKeepScale = 1.6f;
        [Tooltip("Game only (the page's own constant): the radius round the painted block whose blocks' centre is the mass lock, u.")]
        [SerializeField, Min(1f)] float massLockRadius = 35f;

        // ------------------------------------------------------------------ nest
        [Header("Nest (A) - MASS")]
        [Tooltip("MASS: nest blocks (nestBlocksAt0 12 -> nestBlocksAt10 32: \"more blocks in the nest\"), rounded half up.")]
        [SerializeField] ElementalFloat nestBlocks = ElementalFloat.Multiplier(12f, 32f, Element.Mass, 6f);
        [Tooltip("Nest cooldown, s.")]
        [SerializeField, Min(0f)] float nestCooldown = 8f;
        [Tooltip("Nest health: the turret's health blocks when a nest is built (each missile costs one; it never fires its last).")]
        [SerializeField, Min(1)] int nestHealth = 4;
        [Tooltip("Nest health max.")]
        [SerializeField, Min(1)] int nestHealthMax = 12;
        [Tooltip("Turret regen, s per health block (twice as fast while docked in it).")]
        [SerializeField, Min(0.1f)] float turretRegen = 6f;
        [Tooltip("Crystal health: health blocks a crystal gives each of your nests.")]
        [SerializeField, Min(0)] int crystalHealth = 3;
        [Tooltip("Max nests: a new one past this retires the oldest.")]
        [SerializeField, Min(1)] int maxNests = 3;

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
        [Tooltip("Seconds the AI stays docked before it presses to undock.")]
        [SerializeField, Min(0f)] float aiDockSeconds = 3f;
        [Tooltip("Seconds between nests the AI builds when it has none.")]
        [SerializeField, Min(1f)] float aiNestEvery = 25f;
        [Tooltip("The AI slings when its target is at least this far, u, and inside aiSlingDegrees of the nose.")]
        [SerializeField, Min(0f)] float aiSlingMinDistance = 300f;
        [SerializeField, Range(1f, 90f)] float aiSlingDegrees = 20f;

        // ------------------------------------------------------------------ audio (exposed, shipped EMPTY)
        [Header("Audio (FMOD) - every slot ships empty")]
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
        public float NestCooldown => nestCooldown;
        public int NestHealth => nestHealth;
        public int NestHealthMax => nestHealthMax;
        public float TurretRegen => turretRegen;
        public int CrystalHealth => crystalHealth;
        public int MaxNests => maxNests;
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
        public float AiNestEvery => aiNestEvery;
        public float AiSlingMinDistance => aiSlingMinDistance;
        public float AiSlingDegrees => aiSlingDegrees;
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
