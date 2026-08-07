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
