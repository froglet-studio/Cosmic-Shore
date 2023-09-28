using System.Collections;
using System.ComponentModel;
using System.Runtime.InteropServices.WindowsRuntime;
using UnityEngine;

namespace StarWriter.Core
{
    public class LoadedGun : Gun
    {
        [Header("Projectile Configuration")]
        [SerializeField] float speed = 20;
        [SerializeField] float projectileTime = 3;
        [SerializeField] FiringPatterns firingPattern = FiringPatterns.Spherical;
        [SerializeField] int energy;
        //Vector3 scale;

        private void Start()
        {
            //scale = projectilePrefab.transform.localScale;
        }

        public void FireGun()
        {
            FireGun(Ship.Player.transform, speed, Vector3.zero, 1, true, projectileTime, 0, firingPattern, energy); // charge could be used to limit recursion depth
        }

        //public void FireGun(Transform containerTransform, float speed, Vector3 inheritedVelocity,
        //    float projectileScale, bool ignoreCooldown = false, float projectileTime = 3, float charge = 0, FiringPatterns firingPattern = FiringPatterns.single)
    }
}