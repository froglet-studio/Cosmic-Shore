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
        /// Damage a prism with a TRUE impact velocity, so the debris actually flies at the
        /// speed of the thing that hit it.
        ///
        /// The legacy <see cref="Damage(IVesselStatus, PrismImpactor, float, Vector3)"/> path
        /// hands over <c>velocity * inertia</c> and <see cref="Prism.Explode"/> divides by the
        /// prism's volume, so the debris speed carries a gain of <c>inertia / volume</c> - which
        /// ranges over ~100x between a thin trail prism and a fat environment one. The explosion
        /// prefab's speed clamp exists to contain that, and it sits so far below real impact
        /// speeds that every hit saturates and the magnitude reads the same no matter what hit it.
        ///
        /// Pre-multiplying by the prism's volume cancels the divide, so the debris velocity IS
        /// the impact velocity (times <paramref name="restitution"/>) for every prism size - and
        /// the accompanying <paramref name="debrisSpeedLimit"/> replaces the mismatched guard
        /// with a ceiling in the same units. Same idiom <c>Boid</c> already uses when a creature
        /// knocks its own health prism loose.
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

            var prism = prismImpactor.Prism;

            // Multiply by the EXACT value Explode divides by - the cached
            // prismProperties.volume, not the live Prism.Volume. They diverge: the cached one
            // is refreshed at specific lifecycle points and some paths floor it at 1, while the
            // live property reads the scale animator with no floor. Rhino trail prisms sit right
            // at that boundary (~0.75-2.25), so using the live value would leave a residual gain
            // on exactly the prisms this feature is about. Same-value cancellation is exact
            // whatever the cache holds.
            float volume = prism.prismProperties != null ? prism.prismProperties.volume : 0f;
            if (volume <= 0f) volume = Mathf.Max(prism.Volume, 0.0001f);

            prism.Damage(impactVelocity * (restitution * volume), status.Domain, status.PlayerName,
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