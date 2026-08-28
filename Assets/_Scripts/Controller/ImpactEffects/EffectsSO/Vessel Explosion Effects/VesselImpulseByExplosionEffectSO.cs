using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Applies a physical impulse to vessels caught in an explosion — the Grizzly's
    /// signature: the strongest knock-back in the game, deliberately rare (shared
    /// only with Dolphin crystals per the class doc). The shooter riding its own
    /// blast IS the Grizzly's primary movement tool (Ziggs-style self-propulsion),
    /// so the explosion must be initialized with AffectSelfOverride = true.
    ///
    /// Space-5 "Safe Detonation": allies stop being impulsed/damaged, but the
    /// SHOOTER is still hit — friendly-fire-off must never break self-launch.
    ///
    /// A per-(explosion, vessel) latch prevents multi-collider hulls (Squirrel,
    /// Manta) from receiving the impulse once per collider. Deliberately NOT
    /// VesselCombatHitLatch — consuming that would eat the scoreboard's admits.
    /// </summary>
    [CreateAssetMenu(
        fileName = "VesselImpulseByExplosionEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Explosion/VesselImpulseByExplosionEffectSO")]
    public class VesselImpulseByExplosionEffectSO : VesselExplosionEffectSO
    {
        [Header("Impulse")]
        [SerializeField, Tooltip("Multiplier on the explosion's authored impulse for OTHER vessels.")]
        float knockbackMultiplier = 1f;
        [SerializeField, Tooltip("Multiplier on the authored impulse when the shooter hits itself (self-launch).")]
        float selfLaunchMultiplier = 1.25f;
        [SerializeField, Tooltip("Seconds the velocity modifier persists (cosine ease-out).")]
        float impulseDuration = 1f;
        [SerializeField, Tooltip("A dug-in Grizzly is blasted out of turret stance by its own explosion.")]
        bool selfLaunchUnplants = true;

        // (explosion instanceID, vessel instanceID) admitted this blast — pruned lazily.
        static readonly HashSet<long> _admitted = new();
        static int _sincePrune;

        public override void Execute(VesselImpactor impactor, ExplosionImpactor impactee)
        {
            var explosion = impactee ? impactee.Explosion : null;
            var victim = impactor ? impactor.Vessel : null;
            var victimStatus = victim?.VesselStatus;
            if (explosion == null || victimStatus?.VesselTransformer == null)
                return;

            bool isSelf = impactee.SourceVessel != null && ReferenceEquals(impactee.SourceVessel, victim);

            // Space-5 ally sparing: same-domain vessels are skipped — except the shooter.
            if (!isSelf && victimStatus.Domain == explosion.Domain)
            {
                var shooterStatus = impactee.SourceVessel?.VesselStatus;
                bool safeDetonation = shooterStatus?.ElementalAbilityHandler != null &&
                                      shooterStatus.ElementalAbilityHandler.IsUpgradeActive(Element.Space);
                if (safeDetonation)
                    return;
            }

            // One VesselImpactor is shared by all of a hull's colliders, so its instance
            // id is a stable per-vessel key even on multi-collider ships.
            if (!Admit(explosion.GetInstanceID(), impactor.GetInstanceID()))
                return;

            Vector3 direction;
            if (isSelf)
            {
                // SELF-LAUNCH steers by the NOSE, not by the blast geometry. Riding your
                // own explosion is the Grizzly's movement tool, and a radial push sent the
                // pilot wherever they happened to be standing relative to the detonation -
                // which is unaimable. Facing is the one direction the player controls, so
                // the bomb becomes a thruster they point. Other vessels keep the radial
                // knock-back below, which is what a blast should do to a bystander.
                var nose = victimStatus.Transform != null
                    ? victimStatus.Transform.forward
                    : explosion.transform.forward;
                direction = nose.sqrMagnitude < 0.0001f ? explosion.transform.forward : nose.normalized;
            }
            else
            {
                var victimPos = impactor.Transform.position;
                var radial = (victimPos - explosion.transform.position);
                direction = radial.sqrMagnitude < 0.0001f ? explosion.transform.forward : radial.normalized;
            }

            float multiplier = isSelf ? selfLaunchMultiplier : knockbackMultiplier;
            var impulse = explosion.Impulse.Along(direction) * multiplier;

            if (isSelf && selfLaunchUnplants && victimStatus.IsTranslationRestricted &&
                victim is VesselController controller)
            {
                // Route through the controller so the netvar stays in sync (the restore
                // branch's stuck-turret bug came from bypassing this).
                controller.SetTranslationRestricted(false);
            }

            victimStatus.VesselTransformer.ModifyVelocity(impulse, impulseDuration);
        }

        static bool Admit(int explosionId, int vesselId)
        {
            long key = ((long)explosionId << 32) ^ (uint)vesselId;
            if (_admitted.Contains(key))
                return false;

            if (++_sincePrune > 256) { _admitted.Clear(); _sincePrune = 0; }
            _admitted.Add(key);
            return true;
        }
    }
}
