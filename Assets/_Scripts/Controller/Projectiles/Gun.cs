using System.Collections;
using UnityEngine;
using CosmicShore.Utility;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    public enum FiringPatterns
    {
        Default = 0,
        Spherical = 1,
        /// <summary>Shotgun blast: concentric rings of projectiles around the aim axis
        /// (<see cref="Gun.FireRingBlast"/>). Ring geometry is the CALLER's config - a caller
        /// selecting this pattern invokes FireRingBlast directly with its authored ring shape;
        /// FireGun itself falls back to a single aimed shot for this value.</summary>
        ConcentricRings = 2,
    }

    public class Gun : MonoBehaviour
    {
        [Header("Gun Settings")]
        [SerializeField] private float firePeriod = 0.2f;
        [SerializeField] private float sideLength = 2f;

        [Header("Dependencies")]
        [SerializeField] private ProjectileFactory projectileFactory;

        private IVesselStatus _vesselStatus;
        private Projectile _lastProjectile;

        private bool _onCooldown;

        /// <summary>
        /// Reach multiplier stamped onto every projectile this gun fires, so a chain reaction
        /// can carry the firing vessel's SPACE level all the way down its generations. Set by
        /// the action executor at fire time (never cached from a level read at Initialize);
        /// 1 = unscaled, which is every gun that does not chain.
        /// </summary>
        public float ChainRangeScale { get; set; } = 1f;

        /// <summary>
        /// Companion of <see cref="ChainRangeScale"/>: the fraction of its reach each chain
        /// generation hands to the next. 1 = no decay (the SPACE level-5 upgrade).
        /// </summary>
        public float ChainRangeFalloff { get; set; } = 1f;

        #region Initialization
        public void Initialize(IVesselStatus vesselStatus)
        {
            _vesselStatus = vesselStatus;
        }

        /// <summary>
        /// Supplies the pool at runtime instead of from the inspector. A gun mounted on a
        /// VESSEL is authored with its factory, but a <see cref="LoadedGun"/> riding a POOLED
        /// PROJECTILE cannot be — the factory is a scene object and the spike is a prefab
        /// asset — so the host projectile hands its own factory down when it launches. Without
        /// this every chain volley dies on a null factory, silently, one generation in.
        /// </summary>
        public void SetProjectileFactory(ProjectileFactory factory)
        {
            if (factory) projectileFactory = factory;
        }
        #endregion

        #region Public API
        public void FireGun(
            Transform containerTransform,
            float speed,
            Vector3 inheritedVelocity,
            float projectileScale,
            bool ignoreCooldown = false,
            float projectileTime = 3,
            float charge = 0,
            FiringPatterns firingPattern = FiringPatterns.Default,
            int energy = 0, bool detachAfterSpawn = false,
            bool stopOnFirstPrismImpact = false, bool spareOwnDomain = false,
            Vector3? aimDirection = null, float flightGrowthFactor = 1f,
            int sphericalPoints = 0)
        {
            if (_onCooldown && !ignoreCooldown) return;

            switch (firingPattern)
            {
                case FiringPatterns.Spherical:
                    FireSpherical(containerTransform, speed, inheritedVelocity,
                        projectileScale, projectileTime, charge, energy, sphericalPoints);
                    break;

                default:
                    // aimDirection lets a caller deflect the shot off the muzzle's forward —
                    // the accuracy-decay cone (GunSprayAccuracy) is the only user today. The gun
                    // itself owns no spread policy and rolls no dice: it is handed a direction.
                    FireSingle(containerTransform, speed, inheritedVelocity,
                        projectileScale, Vector3.zero, projectileTime, charge, energy, aimDirection, detachAfterSpawn,
                        stopOnFirstPrismImpact, spareOwnDomain, flightGrowthFactor);
                    break;
            }

            if (!ignoreCooldown)
            {
                _onCooldown = true;
                StartCoroutine(CooldownCoroutine());
            }
        }

        public void StopProjectile()
        {
            if (!_lastProjectile)
            {
                CSDebug.LogError("Last projectile not found!");
                return;
            }
            _lastProjectile.ReturnToFactory();
        }

        public void DetonateProjectile()
        {
            CSDebug.Log("Gun DetonateProjectile called");
            // Example: if (_lastProjectile is ExplodableProjectile ep) ep.Detonate();
        }
        #endregion

        /// <summary>
        /// Shotgun blast: concentric rings of projectiles around the aim axis (the
        /// container's forward), plus an optional spike straight down the middle. Ring r of
        /// <paramref name="rings"/> sits at cone angle <c>coneHalfAngleDegrees * r / rings</c>
        /// and carries <c>spikesPerFirstRing * r</c> projectiles, with alternate rings phase-
        /// staggered so the blast fills its own gaps. Fully deterministic by construction
        /// (no RNG draw at all), so every peer fires the identical fan.
        /// Rate limiting is the CALLER's (the Urchin executor owns an owed-seconds loop), so
        /// the gun's own cooldown is bypassed exactly as the executor's other calls do.
        /// </summary>
        public void FireRingBlast(
            Transform containerTransform,
            float speed,
            Vector3 inheritedVelocity,
            float projectileScale,
            float projectileTime,
            float charge,
            int energy,
            int rings,
            int spikesPerFirstRing,
            float coneHalfAngleDegrees,
            bool centerSpike)
        {
            Vector3 aim = containerTransform.forward;
            Vector3 perp = Vector3.Cross(aim, Vector3.up);
            if (perp.sqrMagnitude < 1e-4f) perp = Vector3.Cross(aim, Vector3.right);
            perp.Normalize();

            if (centerSpike)
                FireSingle(containerTransform, speed, inheritedVelocity,
                    projectileScale, aim * sideLength, projectileTime, charge, energy, aim);

            for (int r = 1; r <= rings; r++)
            {
                float coneAngle = coneHalfAngleDegrees * r / rings;
                int count = Mathf.Max(1, spikesPerFirstRing * r);
                float phase = (r % 2) * (180f / count); // stagger alternate rings

                Vector3 rim = Quaternion.AngleAxis(coneAngle, perp) * aim;
                for (int i = 0; i < count; i++)
                {
                    Vector3 dir = Quaternion.AngleAxis(360f * i / count + phase, aim) * rim;
                    FireSingle(containerTransform, speed, inheritedVelocity,
                        projectileScale, dir * sideLength, projectileTime, charge, energy, dir);
                }
            }
        }

        #region Firing Implementations
        private void FireSpherical(
            Transform containerTransform,
            float speed,
            Vector3 inheritedVelocity,
            float projectileScale,
            float projectileTime,
            float charge,
            int energy,
            int pointsOverride = 0)
        {
            if (energy == 0 && pointsOverride <= 0) // tetrahedral pattern
            {
                Vector3[] tetrahedralVertices =
                {
                    new(1, 1, 1),
                    new(-1, -1, 1),
                    new(-1, 1, -1),
                    new(1, -1, -1)
                };

                foreach (var dir in tetrahedralVertices)
                {
                    var n = dir.normalized;
                    var offset = n * sideLength;
                    FireSingle(containerTransform, speed, inheritedVelocity,
                        projectileScale, offset, projectileTime, charge, 0, n);
                }
            }
            else // Golden Spiral method
            {
                // pointsOverride: the TOP-LEVEL barrage's density is the caller's to author
                // (the Urchin fires a dense sphere with no gaps - historically 18 authored
                // ShootPoint ports mirrored across the hull; the spiral supersedes them).
                // Chain children keep the energy-derived budget so a cascade's population
                // stays bounded by depth.
                int points = pointsOverride > 0 ? pointsOverride : 2 * (energy + 3);
                float phi = Mathf.PI * (3 - Mathf.Sqrt(5)); // golden angle

                // DETERMINISTIC, not Random.rotation. Every peer runs this volley (button
                // presses round-trip through R_VesselActionHandler, and a chain spike fires on
                // each machine independently), so a global RNG draw would orient the spiral
                // differently per peer and the prismscape would diverge on the very first
                // cascade. It would also perturb UnityEngine.Random's shared stream, which
                // deterministic systems seed - a gun firing dozens of times a second must not
                // be able to change what the HexRace track looks like.
                //
                // Seeded from the volley's ORIGIN and depth: two peers whose spike reached the
                // same prism fire the same pattern, so the cascade re-converges rather than
                // drifting further apart with every generation.
                var randomRotation = DeterministicOrientation(containerTransform.position, energy);

                // A CHAIN HOP spends a generation here - that decrement is the cascade's depth
                // ladder. The SHIP'S OWN volley must not: `energy` is already the depth the
                // pilot's CHARGE resolved, and spending one on the muzzle burst left the omni
                // barrage one tier shallower than the ring volley from the same authored
                // number - at the shipped resting depth that meant its spikes landed terminal
                // and the barrage never chain-reacted at all. The ship's volley is exactly the
                // call that authors a point count (`pointsOverride`); a hop never does, so the
                // override is the honest discriminator and no new argument is needed.
                if (pointsOverride <= 0) energy--;

                for (int i = 0; i < points; i++)
                {
                    float y = 1 - (i / (float)(points - 1)) * 2; // y from 1 to -1
                    float radius = Mathf.Sqrt(1 - y * y);

                    float theta = phi * i;
                    float x = Mathf.Cos(theta) * radius;
                    float z = Mathf.Sin(theta) * radius;

                    var dir = randomRotation * new Vector3(x, y, z);
                    var offset = dir * sideLength;

                    FireSingle(containerTransform, speed, inheritedVelocity,
                        projectileScale, offset, projectileTime, charge, energy, dir);
                }
            }
        }

        private void FireSingle(
            Transform containerTransform,
            float speed,
            Vector3 inheritedVelocity,
            float projectileScale,
            Vector3 offset,
            float projectileTime,
            float charge,
            int energy,
            Vector3? customDirection = null,
            bool detachAfterSpawn = false,
            bool stopOnFirstPrismImpact = false,
            bool spareOwnDomain = false,
            float flightGrowthFactor = 1f)
        {
            if (_vesselStatus == null)
            {
                CSDebug.LogError("Gun.FireSingle - VesselStatus is null!");
                return;
            }

            Vector3 direction = customDirection ?? containerTransform.forward;
            Vector3 spawnPos  = containerTransform.position;   // using container for spawn point

            SafeLookRotation.TryGet(direction, out var rotation, this);

            // keep existing behavior: spawn under container
            var projectile = projectileFactory.GetProjectile(energy, spawnPos, rotation, containerTransform);

            if (!projectile)
            {
                CSDebug.LogError($"Gun.FireSingle - Failed to spawn projectile of charge {energy}");
                return;
            }

            // tell the projectile how to parent THIS flight.
            // Domain is read live at fire time — never snapshot at Initialize
            // (locked rule: domains re-pick at runtime).
            projectile.Initialize(projectileFactory, _vesselStatus.Domain, _vesselStatus, charge, detachAfterSpawn,
                stopOnFirstPrismImpact, spareOwnDomain);

            // `energy` is BOTH the pool tier and the chain-reaction depth — one number, so a
            // spike's tier and how far it may still propagate can never disagree. Zero is
            // terminal (see Projectile.ChainGeneration).
            projectile.SetChainGeneration(energy);
            projectile.SetChainRangeScale(ChainRangeScale);
            projectile.SetChainRangeFalloff(ChainRangeFalloff);

            projectile.transform.localScale = projectileScale * projectile.InitialScale;

            // MASS in-flight growth: set BEFORE launch, which is where the round captures the
            // scale it will grow from. The gun owns no growth policy - it is handed a factor.
            projectile.SetFlightGrowth(flightGrowthFactor);

            projectile.Velocity = direction * speed + inheritedVelocity;
            projectile.LaunchProjectile(projectileTime);

            _lastProjectile = projectile;
        }
        #endregion

        #region Helpers

        /// <summary>
        /// A repeatable orientation for a spherical volley, derived from where it was fired and
        /// how deep into a chain it is — never from <c>UnityEngine.Random</c>.
        ///
        /// The position is quantized before hashing so two peers that agree about the volley to
        /// within a fraction of a unit agree exactly about its pattern. <see cref="QuantumSize"/>
        /// is the tolerance: large enough to absorb the small positional disagreement between
        /// peers simulating the same spike, small enough that two genuinely different volleys
        /// do not collide onto one pattern.
        /// </summary>
        /// <remarks>Public rather than internal so the edit-mode suite can bind it: tests
        /// compile into Assembly-CSharp-Editor, which is a different assembly and cannot see
        /// Assembly-CSharp's internals.</remarks>
        public static Quaternion DeterministicOrientation(Vector3 origin, int depth)
        {
            const float QuantumSize = 0.5f;

            unchecked
            {
                int h = 17;
                h = h * 31 + Mathf.RoundToInt(origin.x / QuantumSize);
                h = h * 31 + Mathf.RoundToInt(origin.y / QuantumSize);
                h = h * 31 + Mathf.RoundToInt(origin.z / QuantumSize);
                h = h * 31 + depth;

                // Three decorrelated angles out of one hash, via a cheap integer scramble.
                float yaw   = Scramble(h, 0) * 360f;
                float pitch = Scramble(h, 1) * 360f;
                float roll  = Scramble(h, 2) * 360f;
                return Quaternion.Euler(pitch, yaw, roll);
            }
        }

        /// <summary>Hash -> [0,1). Integer-only, so it is identical on every platform.</summary>
        static float Scramble(int hash, int salt)
        {
            unchecked
            {
                uint x = (uint)(hash + salt * 0x9E3779B9);
                x ^= x >> 16; x *= 0x7FEB352D;
                x ^= x >> 15; x *= 0x846CA68B;
                x ^= x >> 16;
                return (x & 0x00FFFFFF) / (float)0x01000000;
            }
        }

        private IEnumerator CooldownCoroutine()
        {
            yield return new WaitForSeconds(firePeriod);
            _onCooldown = false;
        }

        #endregion
    }
}