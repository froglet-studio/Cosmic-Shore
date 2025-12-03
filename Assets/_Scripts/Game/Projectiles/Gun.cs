using System.Collections;
using UnityEngine;
using CosmicShore.Utility;

namespace CosmicShore.Game.Projectiles
{
    public enum FiringPatterns
    {
        Default = 0,
        Spherical = 1,
    }

    public class Gun : MonoBehaviour
    {
        [Header("Gun Settings")]
        [SerializeField] private float firePeriod = 0.2f;
        [SerializeField] private float sideLength = 2f;

        [Header("Dependencies")]
        [SerializeField] private ProjectileFactory projectileFactory;

        private Domains domain;
        private IVesselStatus _vesselStatus;
        private Projectile _lastProjectile;

        private bool _onCooldown;

        #region Initialization
        public void Initialize(IVesselStatus vesselStatus)
        {
            _vesselStatus = vesselStatus;
            domain = vesselStatus.Domain;
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
            int energy = 0, bool detachAfterSpawn = false)
        {
            if (_onCooldown && !ignoreCooldown) return;

            _onCooldown = true;

            switch (firingPattern)
            {
                case FiringPatterns.Spherical:
                    FireSpherical(containerTransform, speed, inheritedVelocity,
                        projectileScale, projectileTime, charge, energy);
                    break;

                default:
                    FireSingle(containerTransform, speed, inheritedVelocity,
                        projectileScale, Vector3.zero, projectileTime, charge, energy, null, detachAfterSpawn);
                    break;
            }

            if (!ignoreCooldown) StartCoroutine(CooldownCoroutine());
        }

        public void StopProjectile()
        {
            if (!_lastProjectile)
            {
                Debug.LogError("Last projectile not found!");
                return;
            }
            _lastProjectile.ReturnToFactory();
        }

        public void DetonateProjectile()
        {
            Debug.Log("Gun DetonateProjectile called");
            // Example: if (_lastProjectile is ExplodableProjectile ep) ep.Detonate();
        }
        #endregion

        #region Firing Implementations
        private void FireSpherical(
            Transform containerTransform,
            float speed,
            Vector3 inheritedVelocity,
            float projectileScale,
            float projectileTime,
            float charge,
            int energy)
        {
            if (energy == 0) // tetrahedral pattern
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
                    var offset = dir.normalized * sideLength;
                    FireSingle(containerTransform, speed, inheritedVelocity,
                        projectileScale, offset, projectileTime, charge, 0, dir.normalized);
                }
            }
            else // Golden Spiral method
            {
                int points = 2 * (energy + 3);
                float phi = Mathf.PI * (3 - Mathf.Sqrt(5)); // golden angle
                var randomRotation = Random.rotation;
                energy--;

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
            bool detachAfterSpawn = false)                // << NEW
        {
            if (_vesselStatus == null)
            {
                Debug.LogError("Gun.FireSingle - VesselStatus is null!");
                return;
            }

            Vector3 direction = customDirection ?? transform.forward;
            Vector3 spawnPos  = containerTransform.position;   // using container for spawn point

            SafeLookRotation.TryGet(direction, out var rotation, this);

            // keep existing behavior: spawn under container
            var projectile = projectileFactory.GetProjectile(energy, spawnPos, rotation, containerTransform);

            if (!projectile)
            {
                Debug.LogError($"Gun.FireSingle - Failed to spawn projectile of charge {energy}");
                return;
            }

            // tell the projectile how to parent THIS flight
            projectile.Initialize(projectileFactory, domain, _vesselStatus, charge, detachAfterSpawn);

            projectile.transform.localScale = projectileScale * projectile.InitialScale;
            projectile.Velocity = direction * speed + inheritedVelocity;
            projectile.LaunchProjectile(projectileTime);

            _lastProjectile = projectile;
        }
        #endregion

        #region Helpers
        private IEnumerator CooldownCoroutine()
        {
            yield return new WaitForSeconds(firePeriod);
            _onCooldown = false;
        }

        #endregion
    }
}