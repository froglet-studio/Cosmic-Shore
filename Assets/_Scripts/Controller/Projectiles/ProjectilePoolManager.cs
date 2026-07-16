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

        protected override Projectile CreateFunc()
        {
            var obj = base.CreateFunc();
            if (_container != null)
                GameObjectInjector.InjectRecursive(obj.gameObject, _container);
            return obj;
        }
    }
}