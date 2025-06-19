using CosmicShore.Core;
using System.Collections;
using UnityEngine;

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
        IShipStatus _shipStatus;

        bool _onCooldown = false;
        float _sideLength = 2;
        float _barrelLength = 0;

        Projectile _projectile;

        public void Initialize(IShipStatus shipStatus)
        {          
            _shipStatus = shipStatus;
            _team = _shipStatus.Team;
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
                default: // Using default to cover single, HexRing, and DoubleHexRing as a single unified pattern
      
                    for (int ring = 0; ring <= energy; ring++)
                    {
                        // Center point only for the first ring
                        if (ring == 0)
                        {
                            FireProjectile(containerTransform, speed, inheritedVelocity, projectileScale, Vector3.zero, projectileTime, charge, energy);
                        }
                        else
                        {
                            int projectilesInThisRing = 6 * (ring); // This scales the number of projectiles with the ring number
                            float angleIncrement = 360f / projectilesInThisRing;

                            for (int i = 0; i < projectilesInThisRing; i++)
                            {
                                Vector3 offset = Quaternion.Euler(0, 0, ring%2 * 30 + angleIncrement * i) * transform.right * _sideLength * ring;
                                FireProjectile(containerTransform, speed, inheritedVelocity, projectileScale, offset, projectileTime, charge, energy);
                            }
                        }
                    }
                    break;
            }

            StartCoroutine(CooldownCoroutine());
        }

        void FireProjectile(Transform containerTransform, float speed, Vector3 inheritedVelocity,
           float projectileScale, Vector3 offset, float projectileTime = 3, float charge = 0, int energy = 0)
        {
            FireProjectile(containerTransform, speed, inheritedVelocity,
            projectileScale, offset, transform.forward, projectileTime, charge, energy);
        }

        void FireProjectile(Transform containerTransform, float speed, Vector3 inheritedVelocity,
            float projectileScale, Vector3 offset, Vector3 normalizedVelocity, float projectileTime = 3, float charge = 0, int energy = 0)
        {
            if (_shipStatus == null)
            {
                Debug.LogError("Gun.FireProjectile - ShipStatus is null. Cannot fire projectile.");
                return;
            }    

            Projectile projectileInstance = containerTransform.GetComponent<PoolManager>().SpawnFromPool(GetPoolTag(energy),
            transform.position + Quaternion.LookRotation(transform.forward) * offset + (transform.forward * _barrelLength), // position
            Quaternion.LookRotation(normalizedVelocity) // rotation
            ).GetComponent<Projectile>();

            projectileInstance.Initialize(_team, _shipStatus);
            projectileInstance.transform.localScale = projectileScale * projectileInstance.InitialScale;
            projectileInstance.transform.parent = containerTransform;
            projectileInstance.Velocity = normalizedVelocity * speed + inheritedVelocity;
            /*if (projectileInstance.TryGetComponent(out Gun projectileGun))
            {
                projectileGun._team = _team;
                projectileGun._shipStatus = _shipStatus;
            }*/
            if (projectileInstance is ExplodableProjectile) ((ExplodableProjectile)projectileInstance).Charge = charge;
            _projectile = projectileInstance;
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
            _projectile.Stop();
        }

        public void DetonateProjectile()
        {
            Debug.Log("GunExplode");
            if (_projectile is ExplodableProjectile) ((ExplodableProjectile)_projectile).Detonate();
        }
       
    }
}