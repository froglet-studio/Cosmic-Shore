using System.Collections;
using UnityEngine;

namespace StarWriter.Core
{
    public class Gun : MonoBehaviour
    {
        [SerializeField] GameObject projectilePrefab;

        public float speed = 10;
        public float projectileTime = 5;
        public float firePeriod = .2f;
        public Teams Team;
        public Ship Ship;
        bool onCooldown = false;

        public void FireGun(Transform containerTransform, Vector3 inheritedVelocity)
        {
            if (onCooldown)
                return;

            

            onCooldown = true;

            var projectile = Instantiate(projectilePrefab);
            projectile.transform.rotation = Quaternion.LookRotation(transform.up);
            projectile.transform.position = transform.position + projectile.transform.forward * 2;
            projectile.transform.parent = containerTransform;
            projectile.GetComponent<Projectile>().Velocity = projectile.transform.forward * speed + inheritedVelocity;
            projectile.GetComponent<Projectile>().Team = Team;
            projectile.GetComponent<Projectile>().Ship = Ship;

            StartCoroutine(MoveProjectileCoroutine(projectile));
            StartCoroutine(CooldownCoroutine());
        }

        IEnumerator CooldownCoroutine()
        {
            yield return new WaitForSeconds(firePeriod);
            onCooldown = false;
        }

        IEnumerator MoveProjectileCoroutine(GameObject projectile)
        {
            var elapsedTime = 0f;
            while (elapsedTime < projectileTime)
            {
                elapsedTime += Time.deltaTime;
                projectile.transform.position += projectile.GetComponent<Projectile>().Velocity * Time.deltaTime;
                yield return null;
            }

            Destroy(projectile);
        }
    }
}