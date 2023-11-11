using UnityEngine;

namespace CosmicShore.Game.Projectiles
{
    public class LoadedGun : Gun
    {
        [Header("Projectile Configuration")]
        [SerializeField] float speed = 20;
        [SerializeField] float projectileTime = 1.5f;
        [SerializeField] FiringPatterns firingPattern = FiringPatterns.Spherical;
        [SerializeField] int energy;
        //Vector3 scale;

        private void Start()
        {
            //scale = projectilePrefab.transform.localScale;
        }

        public void FireGun()
        {
            FireGun(transform.parent, speed, Vector3.zero, 1, true, projectileTime, 0, firingPattern, energy); 
        }

        //public void FireGun(Transform containerTransform, float speed, Vector3 inheritedVelocity,
        //    float projectileScale, bool ignoreCooldown = false, float projectileTime = 3, float charge = 0, FiringPatterns firingPattern = FiringPatterns.single)
    }
}