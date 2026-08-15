using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A gun that rides on a PROJECTILE rather than on a vessel, and fires a whole volley from
    /// one no-argument call. This is the engine of the Urchin's chain reaction: a spike carries
    /// one of these, and <c>ProjectileChainFirePrismEffectSO</c> pulls the trigger when the
    /// spike lands, so the corpse of every converted prism sprays the next generation of spikes.
    ///
    /// Two things about it are load-bearing and were both wrong in the 2023 original:
    ///
    /// 1. **It fires from its OWN transform, not <c>transform.parent</c>.** A spike detaches to
    ///    world space the instant it launches (<see cref="Projectile.LaunchProjectile"/>), so by
    ///    the time it lands its parent is null and the old
    ///    <c>FireGun(transform.parent, …)</c> dereferenced null inside <c>FireSingle</c>.
    ///
    /// 2. **The volley's energy comes from the HOST projectile's remaining depth**, not from
    ///    this component's serialized field. The original read the serialized value on every
    ///    hop, so each generation re-armed the chain at full strength and the cascade had no
    ///    depth limit at all — it ping-ponged between two prefabs forever, bounded only by
    ///    territory conversion. Deriving it from <see cref="Projectile.ChainGeneration"/> means
    ///    <c>Gun.FireSpherical</c>'s own <c>energy--</c> is the decrement, and the serialized
    ///    field survives only as the fallback for a LoadedGun that is not mounted on a
    ///    projectile at all.
    /// </summary>
    [RequireComponent(typeof(Projectile))]
    public class LoadedGun : Gun
    {
        [Header("Projectile Configuration")]
        [SerializeField] float speed = 20;
        [SerializeField] float projectileTime = 1.5f;
        [SerializeField] FiringPatterns firingPattern = FiringPatterns.Spherical;

        [Tooltip("Fallback volley energy, used only when this gun is not mounted on a " +
                 "projectile. On a chain spike the host projectile's remaining " +
                 "ChainGeneration wins.")]
        [SerializeField] int energy;

        Projectile _host;

        void Awake() => _host = GetComponent<Projectile>();

        /// <summary>
        /// Fires one volley outward from this object. Returns the number of generations the
        /// children carry, or -1 when nothing was fired.
        /// </summary>
        public int FireGun()
        {
            int volleyEnergy = _host ? _host.ChainGeneration : energy;

            // Terminal generation: steal, but do not propagate. This single line is the depth
            // cap the original never had.
            if (volleyEnergy <= 0) return -1;

            // Carry the firing vessel's reach down one more generation, decayed by the
            // cascade's falloff, then let the base gun stamp both onto the children it spawns.
            float falloff = _host ? _host.ChainRangeFalloff : 1f;
            float reach   = (_host ? _host.ChainRangeScale : 1f) * falloff;
            ChainRangeScale   = reach;
            ChainRangeFalloff = falloff;

            FireGun(transform, speed * reach, Vector3.zero, 1, true,
                    projectileTime, 0, firingPattern, volleyEnergy);

            return volleyEnergy - 1;
        }
    }
}
