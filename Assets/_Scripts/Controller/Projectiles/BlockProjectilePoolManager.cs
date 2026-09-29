// BlockProjectilePoolManager.cs
using CosmicShore.Gameplay;
using Reflex.Attributes;
using Reflex.Core;
using Reflex.Injectors;
using UnityEngine;
using CosmicShore.Utility;
namespace CosmicShore.Gameplay
{
    public class BlockProjectilePoolManager : GenericPoolManager<Prism>
    {
        [Inject] private Container _container;

        public override Prism Get(Vector3 position, Quaternion rotation, Transform parent, bool worldPositionStays)
        {
            var p = Get_(position, rotation, null);
            p.transform.SetParent(null, true);
            return p;
        }

        public override void Release(Prism instance) => Release_(instance);

        // Same contract as ProjectilePoolManager, and for the same reason: these prisms
        // CARRY a Projectile (the Sparrow turret shot's collider and impact chain), and an
        // un-injected Projectile NREs on its null AudioSystem inside LaunchProjectile —
        // which, from inside the turret's fire loop, is swallowed by the loop's catch and
        // silently ends the burst. OnInstanceCreated rather than CreateFunc so the async
        // buffer refills are injected too.
        protected override void OnInstanceCreated(Prism obj)
        {
            if (_container != null)
                GameObjectInjector.InjectRecursive(obj.gameObject, _container);
        }
    }
}
