using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Gibbon's flight: an ordinary TWO-STICK flyer that a pair of tethers can bend.
    ///
    /// It deliberately inherits <see cref="VesselTransformer"/> rather than overriding
    /// <c>MoveShip</c>. The fleet already has four transformers each carrying its own move step,
    /// which is why a fix written into one of them reaches only the vessels running that class —
    /// the previous attempt at this vessel added a fifth, 1,726 lines long, and with it a fifth
    /// copy of grip, publishing, the modifier channels and the integration. This class is three
    /// overrides and no move step, so the danger-prism slow, knockback, the speed tunnel, external
    /// Course writes and every future fleet-wide change reach it for free.
    ///
    /// Steering is untouched: both sticks pitch/yaw/roll exactly as they do on a Squirrel or a
    /// Manta, and the hull's own lateral axis is what aims the beams — so ROLL is the aim control,
    /// and a full 3D aiming system costs no extra input.
    /// </summary>
    public class GibbonVesselTransformer : VesselTransformer
    {
        [Header("Gibbon")]
        [SerializeField] GibbonTetherExecutor arms;
        [SerializeField] GibbonTetherConfigSO config;

        /// <summary>
        /// Both triggers are spent on the arms, so this vessel structurally cannot drift — an
        /// analog squeeze here means "charge a beam", and it must never also arm a drift tier.
        /// Stated in CODE rather than left to the prefab bool, because a serialized false is one
        /// inspector click away from silently turning the drift back on underneath the mechanic.
        /// </summary>
        protected override bool AnalogTriggerDrift => false;

        /// <summary>
        /// While a line is taut, momentum is only weakly pulled back onto the nose — which is what
        /// lets the swing BE a swing. At the fleet default (1, an outright snap) the tether force
        /// would be erased by grip as fast as it was applied and the vessel would simply fly where
        /// it pointed with a slight wobble.
        ///
        /// It is deliberately not zero. A small convergence keeps the two sticks CONNECTED mid-arc
        /// — you can lean into or out of a swing by pointing — which is the difference between
        /// riding the tether and watching it.
        /// </summary>
        protected override float NoseConvergence(float dt)
        {
            if (arms == null || config == null || !arms.AnyTaut) return base.NoseConvergence(dt);
            return 1f - Mathf.Exp(-Mathf.Max(0f, config.TetheredNoseConvergence) * dt);
        }

        /// <summary>The tether force, and the frame's winch step with it — resolved inside the
        /// move step, in the same frame and the same order as thrust.</summary>
        protected override Vector3 ComputeExternalAcceleration(Vector3 velocity, float dt)
            => arms != null ? arms.Solve(transform.position, velocity, dt) : Vector3.zero;

        /// <summary>
        /// Terminal velocity. Replaces the base drift-overshoot ceiling, which is dead here
        /// anyway (no drift, so <c>DriftBlend01</c> is always 0 and the base returns its input
        /// unchanged) — so this is a replacement in name only and costs no drift behaviour.
        ///
        /// It BLEEDS rather than clamps, and never below the cap, so a pilot who arrives fast from
        /// a boost or a blast keeps that speed and simply sheds it over a second or two. Note the
        /// winch already refuses to add speed near the cap; this exists for everything that is not
        /// the winch.
        /// </summary>
        protected override float ShapeSpeed(float speedNow, float speedBeforeThrust, float dt)
        {
            if (config == null || speedNow <= config.SpeedCap) return speedNow;
            return Mathf.Max(config.SpeedCap, speedNow - config.OverspeedDrag * dt);
        }
    }
}
