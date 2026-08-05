using CosmicShore.Utility;

﻿using UnityEngine;

namespace CosmicShore.Gameplay
{
    public static class PrismEffectHelper
    {
        /// Override if you want a different damage formula.
        public static void Damage(IVesselStatus status, PrismImpactor prismImpactor, float inertia, Vector3 course, float speed)
        {
            // Default: Course * Speed * inertia
            if (status.Player == null)
            {
                CSDebug.LogError("No player found to deal damage to prism!");
                return;
            }
            
            var damage= course * speed * inertia;
            prismImpactor.Prism.Damage(damage, status.Domain, status.PlayerName);
        }
        
        public static void Damage(IVesselStatus status, PrismImpactor prismImpactor, float inertia, Vector3 Velocity)
        {
            // Default: Course * Speed * inertia
            if (status.Player == null)
            {
                CSDebug.LogError("No player found to deal damage to prism!");
                return;
            }
            
            var damage= Velocity * inertia;
            prismImpactor.Prism.Damage(damage, status.Domain, status.PlayerName);
        }

        /// <summary>
        /// Damage a prism with a TRUE impact velocity, so the debris flies at the speed of the
        /// thing that hit it - the same speed for every prism size.
        ///
        /// The legacy <see cref="Damage(IVesselStatus, PrismImpactor, float, Vector3)"/> path
        /// hands over <c>velocity * inertia</c>, which the explosion clamp then flattens: the
        /// gain sits so far above real impact speeds that every hit saturates and the magnitude
        /// reads the same no matter what caused it.
        ///
        /// Here the vector IS the debris velocity. <see cref="Prism.Explode"/> passes it through
        /// untouched (the supplied <paramref name="debrisSpeedLimit"/> is what marks it), and
        /// that limit replaces the mismatched prefab clamp with a ceiling in the same units.
        ///
        /// Do NOT pre-multiply by the prism's volume hoping to cancel Explode's divide. That
        /// divide is a no-op: SetupDestruction stands the scale animator down before reading the
        /// volume, GetCurrentVolume() reports 0 once disabled, and the Max(_, 1) floor therefore
        /// pins prismProperties.volume to exactly 1 for every prism at the moment of the divide.
        /// A pre-multiply survives as a straight volume multiplier - which damps small prisms
        /// (a Rhino trail prism is ~0.75) and slams large ones into the ceiling.
        /// </summary>
        /// <param name="restitution">Debris speed as a multiple of impact speed. 1 = the struck prism leaves at the speed of the striker.</param>
        /// <param name="debrisSpeedLimit">Ceiling in real speed units; 0 falls back to the explosion prefab's clamp (which will flatten it).</param>
        public static void DamageProportional(IVesselStatus status, PrismImpactor prismImpactor, Vector3 impactVelocity,
                                              float restitution, float debrisSpeedLimit)
        {
            if (status.Player == null)
            {
                CSDebug.LogError("No player found to deal damage to prism!");
                return;
            }

            prismImpactor.Prism.Damage(impactVelocity * restitution, status.Domain, status.PlayerName,
                                       debrisSpeedLimit: debrisSpeedLimit);
        }

        public static void Steal(PrismImpactor impactee, IVesselStatus status)
        {
            impactee.Prism.Steal(status.PlayerName, status.Domain);
        }

        /// <summary>
        /// The velocity a skimmer hands a prism it just destroyed: the vessel's own velocity
        /// plus the skimmer's velocity RELATIVE to the vessel at the point that actually made
        /// contact. On a swinging skimmer (the Rhino's sword) a hit near the hilt is barely
        /// more than the ship's course while a tip strike carries the full lever-arm speed of
        /// the swipe, along the swing tangent - see <see cref="SkimmerSwingKinematics"/>.
        ///
        /// Skimmers with no swing model (every rigidly-mounted sphere) have no relative
        /// motion to add, so this collapses to the vessel's own <c>Course * Speed</c>.
        /// </summary>
        /// <param name="swingScale">Designer dial on how much of the relative swing reaches the prism. 1 = the physical model.</param>
        /// <param name="maxSpeed">Ceiling on the resulting speed; 0 leaves it unclamped.</param>
        public static Vector3 ContactVelocity(SkimmerImpactor impactor, IVesselStatus status, Vector3 prismPosition,
                                              float swingScale, float maxSpeed)
        {
            var swing = impactor != null && impactor.Skimmer != null ? impactor.Skimmer.SwingKinematics : null;
            if (swing == null || !swing.IsReady)
                return status.Course * status.Speed;

            Vector3 contact = swing.ClosestBladePoint(prismPosition);
            Vector3 velocity = swing.VesselVelocity + swingScale * swing.RelativeVelocityAt(contact);
            return maxSpeed > 0f ? Vector3.ClampMagnitude(velocity, maxSpeed) : velocity;
        }
    }
}