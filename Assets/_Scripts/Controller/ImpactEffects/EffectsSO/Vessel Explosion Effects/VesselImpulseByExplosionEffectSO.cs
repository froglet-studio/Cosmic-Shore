using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// SELF-LAUNCH ONLY: the Grizzly riding its own blast, its primary movement tool
    /// (Ziggs-style self-propulsion), so the explosion must be initialized with
    /// AffectSelfOverride = true.
    ///
    /// <para><b>It no longer touches any other vessel</b> (Garrett, 2026-10-10: "knockback and
    /// shrink should no longer be an effect that vessels can do to each other ... no other pvp
    /// in the game"). The only thing one pilot may do to another is take their petals, as a
    /// scored hit. This used to shove every vessel in the blast radially, allies included below
    /// the Space-5 "Safe Detonation" gate, which is gone with it.</para>
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

            // Only the shooter is moved. A blast never moves another pilot's vessel.
            if (impactee.SourceVessel == null || !ReferenceEquals(impactee.SourceVessel, victim))
                return;

            // One VesselImpactor is shared by all of a hull's colliders, so its instance
            // id is a stable per-vessel key even on multi-collider ships.
            if (!Admit(explosion.GetInstanceID(), impactor.GetInstanceID()))
                return;

            // SELF-LAUNCH steers by the NOSE, not by the blast geometry. Riding your own
            // explosion is the Grizzly's movement tool, and a radial push sent the pilot wherever
            // they happened to be standing relative to the detonation - which is unaimable.
            // Facing is the one direction the player controls, so the bomb becomes a thruster
            // they point.
            var nose = victimStatus.Transform != null
                ? victimStatus.Transform.forward
                : explosion.transform.forward;
            Vector3 direction = nose.sqrMagnitude < 0.0001f ? explosion.transform.forward : nose.normalized;
            var impulse = explosion.Impulse.Along(direction) * selfLaunchMultiplier;

            if (selfLaunchUnplants && victimStatus.IsTranslationRestricted &&
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
