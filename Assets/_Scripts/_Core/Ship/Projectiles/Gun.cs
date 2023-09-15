using System.Collections;
using UnityEngine;

namespace StarWriter.Core
{
    public class Gun : MonoBehaviour
    {
        [SerializeField] GameObject projectilePrefab;

        public float projectileTime = 2;
        public float firePeriod = .2f;
        public Teams Team;
        public Ship Ship;
        bool onCooldown = false;
        Trail trail = new();

        ShipStatus shipData;
        public Coroutine MoveCoroutine;

        private void Start()
        {
            shipData = Ship.GetComponent<ShipStatus>();
        }

        Projectile projectile;
        public void FireGun(Transform containerTransform, float speed, Vector3 inheritedVelocity, 
            float projectileScale, bool ignoreCooldown = false, float projectileTime = 3, float charge = 0)
        {
            if (onCooldown && !ignoreCooldown)
                return;

            onCooldown = true;

            projectile = Instantiate(projectilePrefab).GetComponent<Projectile>();
            projectile.transform.rotation = Quaternion.LookRotation(transform.forward);
            projectile.transform.position = transform.position + projectile.transform.forward * 5;
            projectile.transform.localScale = projectileScale * Vector3.one;
            projectile.transform.parent = containerTransform;
            projectile.Velocity = projectile.transform.forward * speed + inheritedVelocity;
            projectile.Team = Team;
            projectile.Ship = Ship;
            if (projectile is ExplodableProjectile) ((ExplodableProjectile)projectile).Charge = charge;

            projectile.LaunchProjectile(projectileTime);
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