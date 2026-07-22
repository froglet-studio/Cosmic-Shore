using CosmicShore.Utility;
using Reflex.Attributes;
using Reflex.Core;
using Reflex.Injectors;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    public class ProjectilePoolManager : GenericPoolManager<Projectile>
    {
        [Inject] private Container _container;

        public override Projectile Get(Vector3 position, Quaternion rotation, Transform parent, bool worldPositionStays) =>
            Get_(position, rotation, parent);

        public override void Release(Projectile instance)
        {
            if (!instance.gameObject.activeSelf)
            {
                CSDebug.LogError("Projectile already released! Should not call twice!");
                return;
            }

            Release_(instance);
        }

        // OnInstanceCreated (not CreateFunc) so async InstantiateAsync refills are
        // injected too — an un-injected projectile NREs on its null AudioSystem in
        // LaunchProjectile and every shot from that instance is a dud.
        protected override void OnInstanceCreated(Projectile obj)
        {
            if (_container != null)
                GameObjectInjector.InjectRecursive(obj.gameObject, _container);
        }
    }
}