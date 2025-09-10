using System.Collections.Generic;
using CosmicShore.Game.Projectiles;
using UnityEngine;

namespace CosmicShore.Game
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
            int resourceIndex)
        {
            if (impactor?.Ship?.ShipStatus == null) return;

            var ss = impactor.Ship.ShipStatus;

            var init = new AOEExplosion.InitializeStruct
            {
                OwnTeam            = ss.Team,
                Ship               = ss.Ship,
                MaxScale           = ComputeScaleForShip(ss, minExplosionScale, maxExplosionScale, resourceIndex),
                OverrideMaterial   = overrideMaterial ? overrideMaterial : ss.AOEExplosionMaterial,
                AnnonymousExplosion = false,
                SpawnPosition      = ss.ShipTransform.position,
                SpawnRotation      = ss.ShipTransform.rotation,
            };

            SpawnAllAndDetonate(aoePrefabs, init);
        }

        public static void CreateExplosion(
            AOEExplosion[] aoePrefabs,
            ProjectileImpactor impactor,
            float minExplosionScale,
            float maxExplosionScale)
        {
            if (impactor?.Projectile == null) return;

            var proj = impactor.Projectile;
            var ss   = proj.ShipStatus;

            var init = new AOEExplosion.InitializeStruct
            {
                OwnTeam            = ss.Team,
                Ship               = ss.Ship,
                MaxScale           = Mathf.Lerp(minExplosionScale, maxExplosionScale, proj.Charge),
                OverrideMaterial   = ss.AOEExplosionMaterial,
                AnnonymousExplosion = false,
                SpawnPosition      = proj.transform.position,
                SpawnRotation      = proj.transform.rotation
            };

            SpawnAllAndDetonate(aoePrefabs, init);
        }

        // ---------- Internals ----------

        static void SpawnAllAndDetonate(IEnumerable<AOEExplosion> prefabs, AOEExplosion.InitializeStruct init)
        {
            if (prefabs == null) return;

            foreach (var prefab in prefabs)
            {
                if (!prefab) continue;

                // Instantiate(T) directly returns AOEExplosion
                var aoe = Object.Instantiate(prefab);
                aoe.Initialize(init);
                aoe.Detonate();
            }
        }

        static float ComputeScaleForShip(IShipStatus ss, float min, float max, int resourceIndex)
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
