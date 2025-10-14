using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game.Projectiles
{
    public class ProjectilePoolManager : GenericPoolManager<Projectile>
    {
        public override Projectile Get(Vector3 position, Quaternion rotation, Transform parent, bool worldPositionStays) =>
            Get_(position, rotation, parent);

        public override void Release(Projectile instance)
        {
            if (!instance.gameObject.activeSelf)
            {
                Debug.LogError("Projectile already released! Should not call twice!");
                return;
            }
            
            Release_(instance);
        }
            
    }
}