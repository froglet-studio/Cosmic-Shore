// FullAutoBlockShootActionSO.cs
using UnityEngine;
using CosmicShore.Gameplay;
using CosmicShore.Data;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Turret Stance (Sparrow, MASS): while stopped the guns fire PRISMS instead of bullets.
    ///
    /// A turret prism is a bullet in every respect that is authored — same fire rate, same
    /// muzzle speed, same eased flight path, same impact effects — with exactly two
    /// differences:
    ///   1. it ALWAYS pierces (a bullet only pierces at SPACE 5), so it keeps destroying
    ///      everything it crosses for the whole length of its flight; and
    ///   2. at the end of that flight it stops and stays — it becomes permanent world mass.
    ///
    /// That parity is structural, not copied: the cadence, speed and flight time are read
    /// off <see cref="bulletAction"/> (the same asset the flying-mode cannons fire from), so
    /// retuning the guns retunes the turret and the two can never drift apart. Only the
    /// things that are genuinely turret-specific — the shape of the prism it leaves and
    /// which pool it comes from — are authored here.
    /// </summary>
    [CreateAssetMenu(
        fileName = "FullAutoBlockShootAction",
        menuName = "ScriptableObjects/Vessel Actions/Full Auto Block Shoot")]
    public class FullAutoBlockShootActionSO : ShipActionSO
    {
        /// <summary>
        /// How the flying prism is DRAWN. Both ride the GPU clock; both use the same
        /// carried-projectile gameplay (cadence, speed, pierce, impact). Live-tunable:
        /// the executor reads this per volley, so flipping it in the inspector during
        /// play mode switches the next shot.
        /// </summary>
        public enum FlightVisualization
        {
            /// <summary>The prism itself flies: it scales up and translates out of the
            /// gun into place (PrismFlightClock vertex offset + the standard grow
            /// bloom). Mass is final at the destination from the moment of firing.</summary>
            TranslateAndGrow = 0,

            /// <summary>The fauna suction shader in reverse: the prism's faces stream
            /// out of the MOVING shot point into the final shape at the anchor
            /// (PrismImplosion.StartGrow with the carried projectile as the moving
            /// emitter). The real prism is created when the shot lands, so mass
            /// becomes tangible at arrival.</summary>
            ReverseSuction = 1,
        }

        [Header("Flight Visualization")]
        [Tooltip("How the flying prism is drawn. TranslateAndGrow: the prism scales and " +
                 "translates out of the gun into place. ReverseSuction: the suction shader " +
                 "in reverse - faces stream from the moving shot point into the anchored " +
                 "shape. Read per volley, so it can be flipped live in play mode.")]
        [SerializeField] private FlightVisualization flightVisualization = FlightVisualization.ReverseSuction;

        [Tooltip("ReverseSuction only: the assembly animation runs this many times longer " +
                 "than the shot's flight. The shot still lands (and its mass is created) on " +
                 "the bullet clock; the faces keep streaming into place after it, and the " +
                 "real prism reveals as the stream completes.")]
        [SerializeField, Min(1f)] private float suctionDurationMultiplier = 5f;

        /// <summary>The state a fired prism is born in — playtest dial.</summary>
        public enum FiredPrismState
        {
            /// <summary>Ordinary prism, always — no shield at any level.</summary>
            Plain = 0,
            /// <summary>Every prism arrives SHIELDED (one-hit ablative octahedron),
            /// regardless of level — a playtest override. Fauna still eat shielded
            /// mass via devastate, so the food-web sink survives.</summary>
            Shielded = 1,
            /// <summary>Every prism arrives DANGEROUS. Locked law: danger bites everyone
            /// including the shooter, and suppresses shields (mutual exclusion).</summary>
            Danger = 2,
            /// <summary>THE DESIGN: regular prisms below MASS 5; at MASS 5+ they arrive
            /// shielded. Shielding is the MASS level-5 upgrade — MASS owns the SUBSTANCE of
            /// what you fire (prism length, round growth, and now armour), while SPACE 5
            /// owns REACH: pierce, on both fire modes. Briefly lived at SPACE 5 (round 4)
            /// and was moved back by design sign-off in round 3 of the gun-feel pass.</summary>
            ShieldedAtMass5 = 3,
        }

        [Tooltip("The state fired prisms are born in. ShieldedAtMass5 is the design: regular " +
                 "prisms below MASS 5, shielded at 5+ - MASS owns the substance of what you " +
                 "fire, SPACE 5 owns pierce on both fire modes. Plain/Shielded/Danger are " +
                 "unconditional playtest overrides; Danger bites everyone, shooter included, " +
                 "per locked law.")]
        [SerializeField] private FiredPrismState firedPrismState = FiredPrismState.ShieldedAtMass5;

        [Header("Shot Collision")]
        [Tooltip("World-space DIAMETER of the shot's spherical hit volume, matched to the " +
                 "BULLETS' hit sphere so a prism shot hits whatever a bullet would have hit. " +
                 "The bullet's collider is sized to its own VISIBLE radius +10% - the tracer " +
                 "mesh is a unit sphere at scale (1.5, 1.5, 20), so its cross-section radius " +
                 "is 0.75 and its hit sphere is 0.825 world radius = 1.65 diameter. Keep the " +
                 "two in step: if SparrowProjectile.prefab's SphereCollider changes, change " +
                 "this. The carried collider is a unit sphere (radius 0.5), so the world " +
                 "diameter IS this number.")]
        [SerializeField, Min(0.1f)] private float collisionDiameter = 1.65f;

        [Tooltip("World-space hit diameter for a SHIELDED shot - deliberately larger than the " +
                 "base diameter (x1.5): the armored octahedron reads bigger, so it should hit " +
                 "bigger.")]
        [SerializeField, Min(0.1f)] private float shieldedCollisionDiameter = 2.475f;

        [Tooltip("Fired prisms are FULL SIZE from their first visible frame - no grow-in " +
                 "bloom. The flight out of the gun is itself the continuity transition, so " +
                 "the bloom on top of it only made the shot harder to see.")]
        [SerializeField] private bool spawnFullSize = true;

        [Tooltip("A fired prism keeps its projectile immunity for this long AFTER placement " +
                 "(flight end). During the flight it is immune anyway - it is parked live at " +
                 "the anchor from fire time. Without the settle window, the hit sphere is wide " +
                 "enough that even a full-speed spin's next shots destroy the previous prism, " +
                 "so only one prism ever survives. Immunity is vs ALL projectiles (window is " +
                 "sub-second); after it closes the prism is ordinary friendly-fire mass.")]
        [SerializeField, Min(0f)] private float placementImmunitySeconds = 0.2f;

        [Header("Cadence & Motion")]
        [Tooltip("The vessel's bullet action. Fire rate, muzzle speed and flight time are " +
                 "ADOPTED from it so turret prisms fly exactly like the bullets they replace. " +
                 "Required — the turret does not author its own cadence.")]
        [SerializeField] private FullAutoActionSO bulletAction;

        [Header("Prism Appearance")]
        [SerializeField] private Vector3 blockScale = new(0.8f, 0.5f, 5f);
        [SerializeField] private Vector3 rotationOffsetEuler = Vector3.zero;

        [Header("Pooling")]
        [SerializeField] private PrismType prismType = PrismType.Sparrow;

        public FlightVisualization Visualization => flightVisualization;
        public float SuctionDurationMultiplier => Mathf.Max(1f, suctionDurationMultiplier);
        public FiredPrismState FiredState => firedPrismState;
        public bool SpawnFullSize => spawnFullSize;
        public float CollisionDiameter => collisionDiameter;
        public float ShieldedCollisionDiameter => Mathf.Max(shieldedCollisionDiameter, collisionDiameter);
        public float PlacementImmunitySeconds => placementImmunitySeconds;
        public Vector3 BlockScale => blockScale;
        public Vector3 RotationOffsetEuler => rotationOffsetEuler;
        public PrismType PrismType => prismType;

        /// <summary>True when the bullet action this stance mirrors is wired.</summary>
        public bool HasBulletAction => bulletAction;

        /// <summary>Volleys per second — the bullets' rate, verbatim.</summary>
        public float FireRate => bulletAction ? bulletAction.FiringRate : 0f;

        /// <summary>Seconds of flight before the prism anchors — the bullets' lifetime, verbatim.</summary>
        public float FlightTime => bulletAction ? bulletAction.ProjectileTime : 0f;

        /// <summary>In-flight growth for this shot — the bullets' live MASS-scaled factor,
        /// verbatim. The carried hit sphere swells exactly as a bullet's does, which also
        /// brings it much closer to the size of the prism the player watches flying.</summary>
        public float ResolveGrowthFactor(IVesselStatus status)
            => bulletAction ? bulletAction.ResolveGrowthFactor(status) : 1f;

        /// <summary>Muzzle speed for this shot — the bullets' live SPACE-scaled speed, verbatim.</summary>
        public float ResolveSpeed(IVesselStatus status)
            => bulletAction ? bulletAction.ResolveSpeed(status) : 0f;

        /// <summary>
        /// The accuracy-decay cone — the bullets' profile, verbatim. A turret shot IS a bullet,
        /// so it walks off aim on a held trigger exactly like one; the prism simply stays where
        /// the deflected bullet would have died. Authoring a second cone here is the same class
        /// of drift the shared cadence closed.
        /// </summary>
        public GunSpreadProfile Spread => bulletAction ? bulletAction.Spread : null;

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
        {
            if (!bulletAction)
            {
                CSDebug.LogError(
                    $"[{name}] No bulletAction assigned — the Turret Stance takes its fire rate, " +
                    "speed, flight time and spread from the vessel's Full Auto action. Wire it on " +
                    "the asset.");
                return;
            }

            if (!execs) return;
            execs.Get<FullAutoBlockShootActionExecutor>()?.Begin(this, execs.Get<GunSprayAccuracy>());
        }

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<FullAutoBlockShootActionExecutor>()?.End();
    }
}
