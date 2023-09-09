using System.Collections;
using UnityEngine;

namespace StarWriter.Core
{
    public class Gun : MonoBehaviour
    {
        [SerializeField] GameObject projectilePrefab;

        public bool Detonate = false;
        public float projectileTime = 2;
        public float firePeriod = .2f;
        public Teams Team;
        public Ship Ship;
        bool onCooldown = false;
        Trail trail = new();

        ShipStatus shipData;

        private void Start()
        {
            shipData = Ship.GetComponent<ShipStatus>();
        }

        public void FireGun(Transform containerTransform, float speed, Vector3 inheritedVelocity, 
            float projectileScale, bool ignoreCooldown = false, float projectileTime = 3, float charge = 0)
        {
            if (onCooldown && !ignoreCooldown)
                return;

            onCooldown = true;

            var projectile = Instantiate(projectilePrefab).GetComponent<Projectile>();
            projectile.transform.rotation = Quaternion.LookRotation(transform.forward);
            projectile.transform.position = transform.position + projectile.transform.forward * 5;
            projectile.transform.localScale = projectileScale * Vector3.one;
            projectile.transform.parent = containerTransform;
            projectile.Velocity = projectile.transform.forward * speed + inheritedVelocity;
            projectile.Team = Team;
            projectile.Ship = Ship;
            if (projectile is ExplodableProjectile) ((ExplodableProjectile)projectile).Charge = charge;

            StartCoroutine(MoveProjectileCoroutine(projectile, projectileTime));
            StartCoroutine(CooldownCoroutine());
        }

        IEnumerator CooldownCoroutine()
        {
            yield return new WaitForSeconds(firePeriod);
            onCooldown = false;
        }

        IEnumerator MoveProjectileCoroutine(Projectile projectile, float projectileTime)
        {
            var elapsedTime = 0f;
            var velocity = projectile.Velocity;
            
            while (elapsedTime < projectileTime)
            {
                if (projectile == null) yield break;
                if (Detonate)
                {
                    Destroy(projectile.gameObject);
                    yield break;
                }
                elapsedTime += Time.deltaTime;              
                projectile.transform.position += velocity * Time.deltaTime;
                yield return null;
            }

            Destroy(projectile.gameObject);
        }
    }
}