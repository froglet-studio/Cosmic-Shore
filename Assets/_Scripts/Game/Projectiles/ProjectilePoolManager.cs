using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game.Projectiles
{
    public class ProjectilePoolManager : GenericPoolManager<Projectile>
    {
        public Projectile Get(Vector3 position, Quaternion rotation, Transform parent) =>
            Get_(position, rotation, parent);
        
        public void Release(Projectile instance) =>  Release_(instance);
    }
}