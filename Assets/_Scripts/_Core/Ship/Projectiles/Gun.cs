using System.Collections;
using UnityEngine;

namespace StarWriter.Core
{
    public enum FiringPatterns
    {
        Default = 0,
        Spherical = 1,
    }

    public class Gun : MonoBehaviour
    {
        [SerializeField] GameObject projectilePrefab;

        public float firePeriod = .2f;
        public Teams Team;
        public Ship Ship;
        bool onCooldown = false;
        float sideLength = 3;
        float barrelLength = 3;

        public Coroutine MoveCoroutine;

        Projectile projectile;

        public void FireGun(Transform containerTransform, float speed, Vector3 inheritedVelocity,
            float projectileScale, bool ignoreCooldown = false, float projectileTime = 3, float charge = 0, FiringPatterns firingPattern = FiringPatterns.Default, int energy = 0)
        {
            if (onCooldown && !ignoreCooldown)
                return;

            onCooldown = true;

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

                        foreach (Vector3 v in tetrahedralVertices)
                        {
                            Vector3 offset = v.normalized * sideLength;
                            FireProjectile(containerTransform, speed, inheritedVelocity, projectileScale, offset, v.normalized, projectileTime, charge);
                        }
                    }
                    else // Golden Spiral method for spherical pattern
                    {
                        int points = 10 * (int)energy; // Example scaling factor
                        float phi = Mathf.PI * (3 - Mathf.Sqrt(5)); // Golden angle

                        for (int i = 0; i < points; i++)
                        {
                            float y = 1 - (i / (float)(points - 1)) * 2; // y goes from 1 to -1
                            float radius = Mathf.Sqrt(1 - y * y); // Radius at y position

                            float theta = phi * i; // Azimuthal angle

                            float x = Mathf.Cos(theta) * radius;
                            float z = Mathf.Sin(theta) * radius;

                            Vector3 direction = new Vector3(x, y, z);
                            Vector3 offset = direction * sideLength;
                            FireProjectile(containerTransform, speed, inheritedVelocity, projectileScale, offset, direction, projectileTime, charge);
                        }
                    }
                    break;
                default: // Using default to cover single, HexRing, and DoubleHexRing as a single unified pattern

                    if (energy == 0)
                    {
                        FireProjectile(containerTransform, speed, inheritedVelocity, projectileScale, Vector3.zero, projectileTime, charge);
                    }
                    else
                    {
                        for (int ring = 0; ring < energy; ring++)
                        {
                            // Center point only for the first ring
                            if (ring == 0)
                            {
                                FireProjectile(containerTransform, speed, inheritedVelocity, projectileScale, Vector3.zero, projectileTime, charge);
                            }
                            else
                            {
                                int projectilesInThisRing = 6 * (ring); // This scales the number of projectiles with the ring number
                                float angleIncrement = 360f / projectilesInThisRing;

                                for (int i = 0; i < projectilesInThisRing; i++)
                                {
                                    Vector3 offset = Quaternion.Euler(0, 0, ring%2*30 + angleIncrement * i) * transform.right * sideLength * ring;
                                    FireProjectile(containerTransform, speed, inheritedVelocity, projectileScale, offset, projectileTime, charge);
                                }
                            }
                        }
                    }
                    break;
            }

            StartCoroutine(CooldownCoroutine());
        }

        void FireProjectile(Transform containerTransform, float speed, Vector3 inheritedVelocity,
           float projectileScale, Vector3 offset, float projectileTime = 3, float charge = 0)
        {
            FireProjectile(containerTransform, speed, inheritedVelocity,
            projectileScale, offset, transform.forward, projectileTime, charge);
        }

        void FireProjectile(Transform containerTransform, float speed, Vector3 inheritedVelocity,
            float projectileScale, Vector3 offset, Vector3 normalizedVelocity, float projectileTime = 3, float charge = 0)
        {
            Projectile projectileInstance = Instantiate(projectilePrefab,
                transform.position + Quaternion.LookRotation(transform.forward) * offset + (transform.forward * barrelLength), // position
                Quaternion.LookRotation(normalizedVelocity) // rotation
                ).GetComponent<Projectile>();
            projectileInstance.transform.localScale = projectileScale * Vector3.one;
            projectileInstance.transform.parent = containerTransform;
            projectileInstance.Velocity = normalizedVelocity * speed + inheritedVelocity;
            projectileInstance.Team = Team;
            projectileInstance.Ship = Ship;
            if (projectileInstance.TryGetComponent(out Gun projectileGun))
            {
                projectileGun.Team = Team;
                projectileGun.Ship = Ship;
            }
            if (projectileInstance is ExplodableProjectile) ((ExplodableProjectile)projectileInstance).Charge = charge;

            projectileInstance.LaunchProjectile(projectileTime);
        }

        IEnumerator CooldownCoroutine()
        {
            yield return new WaitForSeconds(firePeriod);
            onCooldown = false;
        }

        public void Detonate()
        {
            projectile.Detonate();
        }
       
    }
}