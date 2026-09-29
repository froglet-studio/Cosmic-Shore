using CosmicShore.Gameplay;
using UnityEngine;
using CosmicShore.Data;
using CosmicShore.Utility;
namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A skyburst missile that lands a DIRECT hit on a vessel detonates there. That is the
    /// whole of it: the round stops, the warhead and blast prefabs spawn at the contact point,
    /// and the blast's own containers decide what reaches the pilot (a combat-hit report and an
    /// elemental transfer — see <c>Docs/ELEMENTAL_ECONOMY.md</c>).
    ///
    /// <para><b>It used to SPIN the victim as well, and that is removed (Sep 2026).</b>
    /// <b>A VESSEL MAY NOT MOVE AN OPPOSING VESSEL</b> — being shoved and re-aimed by somebody
    /// else's weapon is not fun to receive, and it is the one kind of hit a pilot cannot answer
    /// with flying. What a weapon may take from another pilot is their ELEMENTAL CRYSTALS
    /// (stolen on a contact verb, ejected as free-for-all crystals on a ranged one), which is a
    /// loss they can chase down and win back. This class was named for the spin; it is renamed
    /// for what it does, because a name that outlives its behaviour is read as the behaviour.</para>
    ///
    /// <para>The detonation could not simply be unwired with the spin: this was the ONLY effect
    /// on the round's <c>projectileShipEffects</c> that detonated it on a vessel, so deleting the
    /// asset would have made a centre-punch — the dearest hit in the game at 30 points — pass
    /// through a pilot without going off.</para>
    /// </summary>
    [CreateAssetMenu(
        fileName = "VesselDetonateSkyBurstProjectileEffect",
        menuName = "ScriptableObjects/Impact Effects/Vessel - Projectile/VesselDetonateSkyBurstProjectileEffectSO")]
    public class VesselDetonateSkyBurstProjectileEffectSO : VesselProjectileEffectSO
    {
        [Header("Detonation")]
        [SerializeField] private bool detonateOnHit = true;
        [SerializeField] private ProjectileDetonatorSO detonator;
        [SerializeField] private AOEExplosion[] aoePrefabs;
        [SerializeField] private float minExplosionScale = 0.75f;
        [SerializeField] private float maxExplosionScale = 2.0f;
        [SerializeField]
        private float explodeDelay = 0.15f;
        [SerializeField] private float returnDelay = 0.25f;

        public override void Execute(VesselImpactor impactor, ProjectileImpactor impactee)
        {
            if (impactor?.Vessel == null || impactee?.Projectile == null) return;
            if (!detonateOnHit || !detonator) return;

            var proj = impactee.Projectile;
            detonator.Detonate(new ProjectileDetonatorSO.Request
            {
                Projectile          = proj,
                Position            = impactee.transform.position,
                Rotation            = impactee.transform.rotation,
                FaceExitVelocity    = false,
                MinScale            = minExplosionScale,
                MaxScale            = maxExplosionScale,
                ExplodeDelaySeconds = Mathf.Max(0f, explodeDelay),
                ReturnDelay         = Mathf.Max(0f, returnDelay),
                StopAtImpact        = true,
                DisableColliderNow  = true,
                Prefabs             = aoePrefabs,
                Anonymous           = false,
                OverrideMaterial    = proj.VesselStatus.AOEExplosionMaterial,
                DIContainer         = impactee.DIContainer
            });
        }
    }
}
