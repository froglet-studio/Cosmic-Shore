using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using FMODUnity;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Every number the Brittle Star's executor reads (design: <c>R_VesselActions/BRITTLE_STAR.md</c>; the class
    /// document is the Notion "Class - Brittle Star" page). One asset, wired directly on the executor (vessel rule 20),
    /// shared by every Brittle Star and never written at runtime (rule 1).
    ///
    /// <para><b>One element per ability</b> (the four-icon row, charge → mass → space → time):
    /// <list type="bullet">
    /// <item><b>Charge — Ring Detonation</b> (Rolling Thunder: "larger projectile size and larger AOE on support ring
    /// detonation"): the ring explosion's thickness (<see cref="BlastScale"/>) and <see cref="ProjectileScaleMultiplier"/>.</item>
    /// <item><b>Mass — Creation Mode</b> (Fabricator: "increased block size in creation mode"): a trail behind each of
    /// the ring's five corners, <see cref="CreationBlockScale"/>.</item>
    /// <item><b>Space — Destruction Mode &amp; Shooting</b> (Hunter: "increased shot range"):
    /// <see cref="RangeMultiplier"/>. The document's "nudge intensity" is retired with knockback itself — see below.</item>
    /// <item><b>Time — Speed Mode</b> (Zephyr: "increased movement speed bonus in speed mode"):
    /// <see cref="SpeedModeMultiplier"/>.</item>
    /// </list></para>
    ///
    /// <para><b>PvP is petals only (LOCKED, 2026-10-10).</b> The class document predates that rule and asks for shots
    /// that knock enemies back, cut their momentum and disable their trail. None of that may touch another pilot's
    /// vessel any more: every hit on an enemy hull is a SCORED combat hit that moves petals, through the fleet's
    /// reporters (the bullet's <c>VesselCombatHitByBullet</c>) and, for the ring explosion,
    /// <see cref="CombatHitDrain.TryAdmit"/> directly. Destruction Mode's "disable mass production" is
    /// carried by what it can still do: five unbroken streams take petals five times as fast.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "BrittleStarConfig", menuName = "ScriptableObjects/Vessel Actions/Brittle Star Config")]
    public sealed class BrittleStarConfigSO : ScriptableObject
    {
        // ------------------------------------------------------------------ modes
        [Header("Modes")]
        [Tooltip("Seconds a mode takes to come fully on or off (thrusters, trail, the HUD's switch).")]
        [SerializeField, Min(0.01f)] float modeBlendSeconds = 0.35f;
        [Tooltip("TIME: Speed Mode's multiplier on the hull's cruise speed (x6 at rest - Garrett, 2026-10-14). The Zephyr's element.")]
        [SerializeField] ElementalFloat speedModeMultiplier = ElementalFloat.Multiplier(6f, 8f, Element.Time, 4f);
        [Tooltip("1 = Speed Mode stretches the base trail's spacing by exactly the speed it adds, so the hull lays the " +
                 "same prisms per second whatever its speed (the document: 'so as not to increase production output').")]
        [SerializeField, Range(0f, 1f)] float speedModeSpacingFollow = 1f;

        [Header("Creation Mode (a trail behind each ring corner)")]
        [Tooltip("Each corner trail's block size (x across, y thick, z along the path), at Mass 0.")]
        [SerializeField] Vector3 creationBlockSize = new(2.5f, 2.5f, 4.5f);
        [Tooltip("MASS: the corner trails' block size multiplier. The Fabricator's element.")]
        [SerializeField] ElementalFloat creationBlockScale = ElementalFloat.Multiplier(1f, 1.8f, Element.Mass, 0.6f);
        [Tooltip("World units between blocks along each corner's trail (the base trail's wavelength).")]
        [SerializeField, Min(0.5f)] float creationSpacing = 6f;
        [Tooltip("How far out from the hull's axis the corner trails are laid, as a multiple of the ring's radius (1 = on the corners).")]
        [SerializeField, Min(0.2f)] float creationRadiusScale = 1f;
        [Tooltip("How far behind the ring the corner trails are laid, world units (the base trail's offset).")]
        [SerializeField, Min(0f)] float creationOffset = 2f;
        [Tooltip("Most blocks one corner may lay in a frame; excess owed distance is DROPPED so a hitch never dumps a burst.")]
        [SerializeField, Range(1, 8)] int creationMaxPerFrame = 3;
        [Tooltip("Creation Mode's multiplier on cruise speed: the SLOWEST of the modes (Garrett, 2026-10-14). Building " +
                 "is slow work; the corner trails keep their spacing, so production per second falls with it.")]
        [SerializeField, Range(0.1f, 1f)] float creationSpeedMultiplier = 0.5f;
        [Tooltip("Seconds a freshly laid block's collider stays off, so the hull clears it.")]
        [SerializeField, Min(0f)] float creationWaitTime = 0.6f;

        // ------------------------------------------------------------------ shooting
        [Header("Shooting (RT: one turret, or the cockpit gun)")]
        [Tooltip("Rounds per second from EACH stream.")]
        [SerializeField, Min(0.5f)] float fireRate = 12f;
        [Tooltip("Muzzle speed, u/s. Range = speed × 2T/π (Projectile's ease-out, vessel rule 34).")]
        [SerializeField, Min(10f)] float projectileSpeed = 700f;
        [Tooltip("Flight time at Space 0, seconds.")]
        [SerializeField, Min(0.05f)] float projectileTime = 0.3f;
        [Tooltip("SPACE: multiplier on the flight time (and so the range). The Hunter's element.")]
        [SerializeField] ElementalFloat rangeMultiplier = ElementalFloat.Multiplier(1f, 1.6f, Element.Space, 0.7f);
        [Tooltip("Round size multiplier on the projectile prefab's own size, at Charge 0.")]
        [SerializeField, Min(0.1f)] float projectileScale = 1.6f;
        [Tooltip("CHARGE: the rounds' size multiplier. Rolling Thunder's element.")]
        [SerializeField] ElementalFloat projectileScaleMultiplier = ElementalFloat.Multiplier(1f, 1.8f, Element.Charge, 0.6f);
        [Header("Destruction Mode (the five arms)")]
        [Tooltip("Rounds per second from EACH of the five turrets while Destruction Mode is on. It fires on its own, with " +
                 "no spread, at the Sparrow's full-auto cadence: a stream this steady reads as five ARMS - the brittle star's.")]
        [SerializeField, Min(1f)] float destructionFireRate = 90f;
        [Tooltip("The arms' muzzle speed, u/s (the Sparrow's full-auto: 1350). With the tracer's own length, this and the " +
                 "fire rate decide whether the stream reads as solid: spacing = speed / rate (15 u at 1350 / 90).")]
        [SerializeField, Min(10f)] float destructionProjectileSpeed = 1350f;
        [Tooltip("The arms' round size, as a multiple of the round prefab's own (the Sparrow's tracer at 1).")]
        [SerializeField, Min(0.1f)] float destructionProjectileScale = 1f;
        [Tooltip("With the right stick IN (centred), the five arms angle inward to meet the nose axis this far ahead, " +
                 "world units - the vessel as a crosshair.")]
        [SerializeField, Min(1f)] float armFocusDistance = 120f;
        [Tooltip("With the right stick pushed OUT to the rim, each arm splays this many degrees outward from the nose. " +
                 "Between in and out the arms move linearly (BrittleStarRules.ArmDirection).")]
        [SerializeField, Range(0f, 60f)] float armSplayDegrees = 25f;
        [Tooltip("Widest cone the right stick (or a touch drag) opens, in degrees off each muzzle's axis.")]
        [SerializeField, Range(0f, 45f)] float maxSpreadDegrees = 16f;
        [Tooltip("Right-stick deflection below this aims nowhere: normal fire comes from the cockpit gun.")]
        [SerializeField, Range(0f, 0.9f)] float aimDeadzone = 0.25f;
        [Tooltip("Degrees per second the support ring turns to bring the aimed turret round (the 'firing rotation').")]
        [SerializeField, Min(0f)] float ringTurnRate = 540f;

        // ------------------------------------------------------------------ ring detonation
        [Header("Ring Detonation")]
        [Tooltip("Launch speed of the ejected ring, u/s, on top of the hull's own velocity.")]
        [SerializeField, Min(1f)] float ringLaunchSpeed = 240f;
        [Tooltip("Seconds the ring flies before it detonates on its own.")]
        [SerializeField, Min(0.05f)] float ringFuseSeconds = 1.1f;
        [Tooltip("The ring detonates on touching any prism or enemy hull within this distance of its centre line, " +
                 "as a multiple of the ring's radius.")]
        [SerializeField, Min(0.1f)] float ringContactRadiusScale = 1f;
        [Tooltip("Spin of the flying ring about its axis, degrees per second.")]
        [SerializeField] float ringFlightSpin = 900f;
        [Tooltip("The RING EXPLOSION (Garrett, 2026-10-14): a flat disk the diameter of the cell's NUCLEUS and as thick as " +
                 "the ring, centred where the ring blows and lying in its plane. It expands from the ring's own radius out to the " +
                 "nucleus's over this many seconds, destroying every prism it passes (any domain - friendly fire) and " +
                 "stripping petals from every rival hull it passes.")]
        [SerializeField, Min(0.05f)] float ringBlastSeconds = 0.7f;
        [Tooltip("The disk's radius when the ring blows outside any cell with a nucleus, world units.")]
        [SerializeField, Min(1f)] float ringBlastFallbackRadius = 200f;
        [Tooltip("CHARGE: multiplier on the disk's THICKNESS (x1 = exactly the ring's own). Rolling Thunder's element: " +
                 "'larger AOE on support ring detonation'.")]
        [SerializeField] ElementalFloat blastScale = ElementalFloat.Multiplier(1f, 4f, Element.Charge, 1f);
        [Tooltip("Width of the expanding band drawn at the disk's edge, world units (the explosion's look; it does not " +
                 "change what the disk reaches).")]
        [SerializeField, Min(0.1f)] float ringBlastBandWidth = 6f;
        [Tooltip("Seconds the cockpit takes to build a new ring once it has the resources.")]
        [SerializeField, Min(0.05f)] float rebuildSeconds = 0.5f;
        [Tooltip("Seconds of OVERDRIVE (all three modes at once) a crystal buys when a charge is already stored.")]
        [SerializeField, Min(0.5f)] float overdriveSeconds = 6f;

        [Header("Scored hits (PvP is petals only)")]
        [Tooltip("The ring explosion's hit on an enemy hull: its class (and so its price in petals).")]
        [SerializeField] CombatHitClass ringBlastHitClass = CombatHitClass.MissileBlast;
        [Tooltip("Seconds before the same victim can be scored by this pilot's ring explosion again.")]
        [SerializeField, Min(0f)] float ringBlastHitCooldown = 0.5f;
        [Tooltip("Shared combat-hit channel (StatsManager scores from it).")]
        [SerializeField] ScriptableEventCombatHitStats onCombatHitLanded;

        // ------------------------------------------------------------------ autopilot
        [Header("Autopilot (input-only: the AI presses the same replicated controls a human does)")]
        [Tooltip("Seconds between the AI's mode decisions.")]
        [SerializeField, Min(0.2f)] float aiModeIntervalSeconds = 3f;
        [Tooltip("Rival prisms on the line ahead that make the AI hold the trigger.")]
        [SerializeField, Min(1)] int aiFireMinTargets = 2;
        [Tooltip("Rival prisms on the line ahead that make the AI switch to Destruction Mode.")]
        [SerializeField, Min(1)] int aiDestructionMinTargets = 8;
        [Tooltip("Rival prisms near the line ahead that make the AI throw its ring.")]
        [SerializeField, Min(1)] int aiDetonateMinTargets = 18;
        [Tooltip("Degrees off the nose beyond which the AI's target is far enough round to want Speed Mode.")]
        [SerializeField, Range(0f, 180f)] float aiSpeedModeDegrees = 50f;
        [Tooltip("Shortest time the AI holds or releases the trigger.")]
        [SerializeField, Min(0.05f)] float aiTriggerMinSeconds = 0.4f;

        // ------------------------------------------------------------------ audio (exposed, shipped EMPTY)
        [Header("Audio (FMOD — every slot ships empty until audio authors it)")]
        [SerializeField] EventReference speedModeEvent;
        [SerializeField] EventReference creationModeEvent;
        [SerializeField] EventReference destructionModeEvent;
        [SerializeField] EventReference modeOffEvent;
        [SerializeField] EventReference fireEvent;
        [SerializeField] EventReference ringLaunchEvent;
        [SerializeField] EventReference ringBlastEvent;
        [SerializeField] EventReference ringRebuildEvent;
        [SerializeField] EventReference chargeStoredEvent;
        [SerializeField] EventReference overdriveEvent;

        // ------------------------------------------------------------------ accessors
        public float ModeBlendSeconds => modeBlendSeconds;
        public ElementalFloat SpeedModeMultiplier => speedModeMultiplier;
        public float SpeedModeSpacingFollow => speedModeSpacingFollow;
        public Vector3 CreationBlockSize => creationBlockSize;
        public ElementalFloat CreationBlockScale => creationBlockScale;
        public float CreationSpacing => creationSpacing;
        public float CreationRadiusScale => creationRadiusScale;
        public int CreationMaxPerFrame => creationMaxPerFrame;
        public float CreationWaitTime => creationWaitTime;
        public float FireRate => fireRate;
        public float ProjectileSpeed => projectileSpeed;
        public float ProjectileTime => projectileTime;
        public ElementalFloat RangeMultiplier => rangeMultiplier;
        public float ProjectileScale => projectileScale;
        public ElementalFloat ProjectileScaleMultiplier => projectileScaleMultiplier;
        public float DestructionProjectileScale => destructionProjectileScale;
        public float DestructionFireRate => destructionFireRate;
        public float DestructionProjectileSpeed => destructionProjectileSpeed;
        public float CreationOffset => creationOffset;
        public float CreationSpeedMultiplier => creationSpeedMultiplier;
        public float ArmFocusDistance => armFocusDistance;
        public float ArmSplayDegrees => armSplayDegrees;
        public float MaxSpreadDegrees => maxSpreadDegrees;
        public float AimDeadzone => aimDeadzone;
        public float RingTurnRate => ringTurnRate;
        public float RingLaunchSpeed => ringLaunchSpeed;
        public float RingFuseSeconds => ringFuseSeconds;
        public float RingContactRadiusScale => ringContactRadiusScale;
        public float RingFlightSpin => ringFlightSpin;
        public float RingBlastSeconds => ringBlastSeconds;
        public float RingBlastFallbackRadius => ringBlastFallbackRadius;
        public ElementalFloat BlastScale => blastScale;
        public float RingBlastBandWidth => ringBlastBandWidth;
        public float RebuildSeconds => rebuildSeconds;
        public float OverdriveSeconds => overdriveSeconds;
        public CombatHitClass RingBlastHitClass => ringBlastHitClass;
        public float RingBlastHitCooldown => ringBlastHitCooldown;
        public ScriptableEventCombatHitStats OnCombatHitLanded => onCombatHitLanded;
        public float AiModeIntervalSeconds => aiModeIntervalSeconds;
        public int AiFireMinTargets => aiFireMinTargets;
        public int AiDestructionMinTargets => aiDestructionMinTargets;
        public int AiDetonateMinTargets => aiDetonateMinTargets;
        public float AiSpeedModeDegrees => aiSpeedModeDegrees;
        public float AiTriggerMinSeconds => aiTriggerMinSeconds;
        public EventReference SpeedModeEvent => speedModeEvent;
        public EventReference CreationModeEvent => creationModeEvent;
        public EventReference DestructionModeEvent => destructionModeEvent;
        public EventReference ModeOffEvent => modeOffEvent;
        public EventReference FireEvent => fireEvent;
        public EventReference RingLaunchEvent => ringLaunchEvent;
        public EventReference RingBlastEvent => ringBlastEvent;
        public EventReference RingRebuildEvent => ringRebuildEvent;
        public EventReference ChargeStoredEvent => chargeStoredEvent;
        public EventReference OverdriveEvent => overdriveEvent;
    }
}
