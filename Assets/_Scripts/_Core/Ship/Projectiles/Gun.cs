using System.Collections;
using UnityEngine;

namespace StarWriter.Core
{
    public enum FiringPatterns
    {
        single = 0,
        HexRing = 1,
        DoubleHexRing = 2
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
            float projectileScale, bool ignoreCooldown = false, float projectileTime = 3, float charge = 0, FiringPatterns firingPattern = FiringPatterns.single)
        {
            if (onCooldown && !ignoreCooldown)
                return;

            onCooldown = true;

            switch (firingPattern)
            {
                case FiringPatterns.single:
                    FireProjectile(Vector3.zero);
                    break;

                case FiringPatterns.HexRing:
                    FireProjectile(Vector3.zero); // Center
                    for (int i = 0; i < 6; i++)
                    {
                        Vector3 offset = Quaternion.Euler(0, 60 * i, 0) * transform.right * sideLength;
                        FireProjectile(offset);
                    }
                    break;

                case FiringPatterns.DoubleHexRing:
                    FireProjectile(Vector3.zero); // Center

                    for (int i = 0; i < 6; i++)
                    {
                        Vector3 innerOffset = Quaternion.Euler(0, 0, 60 * i) * transform.right * sideLength;
                        FireProjectile(innerOffset); // Inner hexagon

                        Vector3 outerOffset = innerOffset * 2; // Outer hexagon corners
                        FireProjectile(outerOffset);

                        Vector3 midpointOffset = Quaternion.Euler(0, 0, 30 + 60 * i) * transform.right * sideLength * 2;
                        FireProjectile(midpointOffset); // Outer hexagon midpoint
                    }
                    break;
            }

            void FireProjectile(Vector3 offset)
            {
                Projectile projectileInstance = Instantiate(projectilePrefab,
                    transform.position + Quaternion.LookRotation(transform.forward) * offset + (transform.forward*barrelLength), // position
                    Quaternion.LookRotation(transform.forward) // rotation
                    ).GetComponent<Projectile>();
                projectileInstance.transform.localScale = projectileScale * Vector3.one;
                projectileInstance.transform.parent = containerTransform;
                projectileInstance.Velocity = transform.forward * speed + inheritedVelocity;
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

            StartCoroutine(CooldownCoroutine());
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