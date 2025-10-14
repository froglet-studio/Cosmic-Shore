// BlockProjectilePoolManager.cs
using CosmicShore.Core;
using UnityEngine;

namespace CosmicShore.Game.Projectiles
{
    public class BlockProjectilePoolManager : GenericPoolManager<Prism>
    {
        public override Prism Get(Vector3 position, Quaternion rotation, Transform parent, bool worldPositionStays)
        {
            var p = Get_(position, rotation, null);     
            p.transform.SetParent(null, true);       
            return p;
        }

        public override void Release(Prism instance) => Release_(instance);
    }
}