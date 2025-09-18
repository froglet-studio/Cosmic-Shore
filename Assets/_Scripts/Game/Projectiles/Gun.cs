using CosmicShore.Core;
using System.Collections;
using UnityEngine;
using UnityEngine.Video;

namespace CosmicShore.Game.Projectiles
{
    public enum FiringPatterns
    {
        Default = 0,
        Spherical = 1,
    }

    public class Gun : MonoBehaviour
    {
        [SerializeField]
        float _firePeriod = .2f;

        Teams _team;
        IVesselStatus vesselStatus;

        bool _onCooldown = false;
        float _sideLength = 2;
        float _barrelLength = 2f;

        Projectile _projectile;

        [SerializeField]
        PoolManager _poolManager;

        public void Initialize(IVesselStatus vesselStatus)
        {          
            this.vesselStatus = vesselStatus;
            _team = this.vesselStatus.Team;
        }

        public void FireGun(Transform containerTransform, float speed, Vector3 inheritedVelocity,
            float projectileScale, bool ignoreCooldown = false, float projectileTime = 3, float charge = 0, FiringPatterns firingPattern = FiringPatterns.Default, int energy = 0)
        {
            if (_onCooldown && !ignoreCooldown)
                return;

            _onCooldown = true;

            switch (firingPattern)
            {
                case FiringPatterns.Spherical:
                    if (energy == 0) // Tetrahedral pattern
                    {
                        Vector3[] tetrahedralVertices = {
                            new Vector3(1, 1, 1),
                            new Vector3(-1, -1, 1),
                            new Vector3(-1, 1, -1),
                            new Vector3(1, -1, -1)
                        };

                        foreach (Vector3 direction in tetrahedralVertices)
                        {
                            Vector3 offset = direction.normalized * _sideLength;
                            FireProjectile(containerTransform, speed, inheritedVelocity, projectileScale, offset, direction.normalized, projectileTime, charge);
                        }
                    }
                    else // Golden Spiral method for spherical pattern
                    {
                        int points = 2 * ((int)energy + 3); // 
                        float phi = Mathf.PI * (3 - Mathf.Sqrt(5)); // Golden angle
                        var randomRotation = Random.rotation;
                        energy--;
                        for (int i = 0; i < points; i++)
                        {
                            float y = 1 - (i / (float)(points - 1)) * 2; // y goes from 1 to -1
                            float radius = Mathf.Sqrt(1 - y * y); // Radius at y position

                            float theta = phi * i; // Azimuthal angle

                            float x = Mathf.Cos(theta) * radius;
                            float z = Mathf.Sin(theta) * radius;

                            Vector3 direction = randomRotation * (new Vector3(x, y, z));
                            Vector3 offset = direction * _sideLength;
                            FireProjectile(containerTransform, speed, inheritedVelocity, projectileScale, offset, direction, projectileTime, charge, energy);
                        }
                    }
                    break;
                default: // Using default to cover single, HexRing, and DoubleHexRing as a single unified pattern . . . all this ring functionality is ready to be restored upon refactor
      
                    //for (int ring = 0; ring <= energy; ring++)
                    //{
                    //    // Center point only for the first ring
                    //    if (ring == 0)
                    //    {
                            FireProjectile(containerTransform, speed, inheritedVelocity, projectileScale, Vector3.zero, projectileTime, charge, energy);
                    //    }
                    //    else
                    //    {
                    //        int projectilesInThisRing = 6 * (ring); // This scales the number of projectiles with the ring number
                    //        float angleIncrement = 360f / projectilesInThisRing;

                    //        for (int i = 0; i < projectilesInThisRing; i++)
                    //        {
                    //            Vector3 offset = Quaternion.Euler(0, 0, ring%2 * 30 + angleIncrement * i) * transform.right * _sideLength * ring;
                    //            FireProjectile(containerTransform, speed, inheritedVelocity, projectileScale, offset, projectileTime, charge, energy);
                    //        }
                    //    }
                    //}
                    break;
            }

            StartCoroutine(CooldownCoroutine());
        }

        void FireProjectile(Transform containerTransform, float speed, Vector3 inheritedVelocity, float projectileScale, Vector3 offset, float projectileTime = 3, float charge = 0, int energy = 0)
        {
            FireProjectile(containerTransform, speed, inheritedVelocity,
            projectileScale, offset, transform.forward, projectileTime, charge, energy);
        }

        void FireProjectile(Transform containerTransform, float speed, Vector3 inheritedVelocity,
            float projectileScale, Vector3 offset, Vector3 normalizedVelocity, float projectileTime = 3, float charge = 0, int energy = 0)
        {
            if (vesselStatus == null)
            {
                Debug.LogError("Gun.FireProjectile - VesselStatus is null. Cannot fire projectile.");
                return;
            }

            string poolTag = GetPoolTag(energy);
            if (poolTag == null)
            {
                Debug.LogError("Gun.FireProjectile - PoolTag is null. Cannot spawn projectile.");
                return;
            }
            
            Vector3 spawnPosition = transform.position + Quaternion.LookRotation(transform.forward) * offset + (transform.forward * _barrelLength);
            Quaternion rotation = Quaternion.LookRotation(normalizedVelocity);

            if (_poolManager == null)
            {
                Debug.LogError("Gun.FireProjectile - PoolManager is null. Cannot spawn projectile.");
                return;
            }

            var projectileGO = _poolManager.SpawnFromPool(poolTag, spawnPosition, rotation);
            if (projectileGO == null)
            {
                Debug.LogError("No projectile gameobject available in pool to spawn!");
                return;
            }
            if (!projectileGO.TryGetComponent(out _projectile))
            {
                Debug.LogError("Gun.FireProjectile - Failed to spawn projectile from pool. Try increasing pool size!");
                return;
            }
            _projectile.Initialize(_poolManager, _team, vesselStatus, charge);
            _projectile.transform.localScale = projectileScale * _projectile.InitialScale;
            _projectile.transform.SetParent(containerTransform != null ? containerTransform : null, true);
            _projectile.Velocity = normalizedVelocity * speed + inheritedVelocity;
            _projectile.LaunchProjectile(projectileTime);
        }

        private string GetPoolTag(int energy)
        {
            if (energy > 1) return "SuperEnergizedProjectile";
            else if (energy > 0) return "EnergizedProjectile";
            else return "Projectile";
        }

        IEnumerator CooldownCoroutine()
        {
            yield return new WaitForSeconds(_firePeriod);
            _onCooldown = false;
        }

        public void StopProjectile()
        {
            if (_projectile != null)
                _projectile.Stop();
        }

        public void DetonateProjectile()
        {
            Debug.Log("GunExplode");
            // if (_projectile is ExplodableProjectile ep) ep.Detonate();
        }
       
    }
}