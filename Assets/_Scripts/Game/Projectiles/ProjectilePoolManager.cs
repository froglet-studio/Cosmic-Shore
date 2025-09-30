using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game.Projectiles
{
    public class ProjectilePoolManager : GenericPoolManager<Projectile>
    {
        public override Projectile Get(Vector3 position, Quaternion rotation, Transform parent, bool worldPositionStays) =>
            Get_(position, rotation, parent);
        
        public override void Release(Projectile instance) =>  Release_(instance);
    }
}