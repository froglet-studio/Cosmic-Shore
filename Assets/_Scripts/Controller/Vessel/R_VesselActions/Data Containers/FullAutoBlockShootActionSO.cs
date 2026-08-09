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
        [SerializeField] private FlightVisualization flightVisualization = FlightVisualization.TranslateAndGrow;

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
        public Vector3 BlockScale => blockScale;
        public Vector3 RotationOffsetEuler => rotationOffsetEuler;
        public PrismType PrismType => prismType;

        /// <summary>True when the bullet action this stance mirrors is wired.</summary>
        public bool HasBulletAction => bulletAction;

        /// <summary>Volleys per second — the bullets' rate, verbatim.</summary>
        public float FireRate => bulletAction ? bulletAction.FiringRate : 0f;

        /// <summary>Seconds of flight before the prism anchors — the bullets' lifetime, verbatim.</summary>
        public float FlightTime => bulletAction ? bulletAction.ProjectileTime : 0f;

        /// <summary>Muzzle speed for this shot — the bullets' live SPACE-scaled speed, verbatim.</summary>
        public float ResolveSpeed(IVesselStatus status)
            => bulletAction ? bulletAction.ResolveSpeed(status) : 0f;

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
        {
            if (!bulletAction)
            {
                CSDebug.LogError(
                    $"[{name}] No bulletAction assigned — the Turret Stance takes its fire rate, " +
                    "speed and flight time from the vessel's Full Auto action. Wire it on the asset.");
                return;
            }

            execs?.Get<FullAutoBlockShootActionExecutor>()?.Begin(this);
        }

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vesselStatus)
            => execs?.Get<FullAutoBlockShootActionExecutor>()?.End();
    }
}
