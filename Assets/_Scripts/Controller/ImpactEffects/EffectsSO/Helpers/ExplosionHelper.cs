using System.Collections.Generic;
using CosmicShore.Gameplay;
using Reflex.Core;
using Reflex.Injectors;
using UnityEngine;
using CosmicShore.Data;
namespace CosmicShore.Gameplay
{
    public static class ExplosionHelper
    {
        // ---------- Public API ----------

        public static void CreateExplosion(
            AOEExplosion[] aoePrefabs,
            VesselImpactor impactor,
            float minExplosionScale,
            float maxExplosionScale,
            Material overrideMaterial,
            int resourceIndex,
            Vector3 localOffset)
        {
            if (impactor?.Vessel?.VesselStatus == null) return;

            var ss = impactor.Vessel.VesselStatus;
            if (ss.Vessel == null || ss.ShipTransform == null) return;
            var shipTransform = ss.ShipTransform;

            var init = new AOEExplosion.InitializeStruct
            {
                OwnDomain            = ss.Domain,
                Vessel               = ss.Vessel,
                MaxScale             = ComputeScaleForShip(ss, minExplosionScale, maxExplosionScale, resourceIndex),
                OverrideMaterial     = overrideMaterial ? overrideMaterial : ss.AOEExplosionMaterial,
                AnnonymousExplosion  = false,
                SpawnPosition        = shipTransform.position + shipTransform.TransformDirection(localOffset),
                SpawnRotation        = shipTransform.rotation,
            };

            SpawnAllAndDetonate(aoePrefabs, init, impactor.DIContainer);
        }


        public static void CreateExplosion(
            AOEExplosion[] aoePrefabs,
            ProjectileImpactor impactor,
            float minExplosionScale,
            float maxExplosionScale)
        {
            if (impactor?.Projectile == null) return;

            var proj = impactor.Projectile;
            var ss   = proj.VesselStatus;
            if (ss?.Vessel == null) return;

            var init = new AOEExplosion.InitializeStruct
            {
                OwnDomain            = ss.Domain,
                Vessel               = ss.Vessel,
                MaxScale           = Mathf.Lerp(minExplosionScale, maxExplosionScale, proj.Charge),
                OverrideMaterial   = ss.AOEExplosionMaterial,
                AnnonymousExplosion = false,
                SpawnPosition      = proj.transform.position,
                SpawnRotation      = proj.transform.rotation
            };

            SpawnAllAndDetonate(aoePrefabs, init, impactor.DIContainer);
        }

        // ---------- Internals ----------

        static void SpawnAllAndDetonate(IEnumerable<AOEExplosion> prefabs, AOEExplosion.InitializeStruct init, Container container)
        {
            if (prefabs == null) return;

            foreach (var prefab in prefabs)
            {
                if (!prefab) continue;

                var aoe = Object.Instantiate(prefab);
                if (container != null)
                    GameObjectInjector.InjectRecursive(aoe.gameObject, container);
                aoe.Initialize(init);
                aoe.Detonate();
            }
        }

        static float ComputeScaleForShip(IVesselStatus ss, float min, float max, int resourceIndex)
        {
            var resources = ss?.ResourceSystem?.Resources;
            if (resources != null &&
                resourceIndex >= 0 &&
                resourceIndex < resources.Count)
            {
                var t = resources[resourceIndex].CurrentAmount;
                return Mathf.Lerp(min, max, t);
            }
            // Fallback behavior from original code: use max if resource index missing
            return max;
        }
    }
}
