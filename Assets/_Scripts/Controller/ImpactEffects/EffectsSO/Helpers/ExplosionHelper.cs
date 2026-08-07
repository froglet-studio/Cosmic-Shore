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

        /// <summary>
        /// Spawns and detonates a vessel-sourced explosion.
        ///
        /// <paramref name="sizeMultiplier"/> grows the whole blast SELF-SIMILARLY: it scales the
        /// base diameter and, for a cone, the axial reach by the same factor. That coupling is not
        /// optional — a cone's half-angle IS baseRadius/height, so scaling one dimension alone
        /// silently re-shapes the blast. Callers that want to reach further without changing the
        /// cone's angle pass it here; whatever set the ANGLE (the resource) stays in charge of the
        /// angle. <paramref name="affectSelfOverride"/> replaces the impactor's authored friendly
        /// fire (null keeps it).
        /// </summary>
        public static void CreateExplosion(
            AOEExplosion[] aoePrefabs,
            VesselImpactor impactor,
            float minExplosionScale,
            float maxExplosionScale,
            Material overrideMaterial,
            int resourceIndex,
            Vector3 localOffset,
            float sizeMultiplier = 1f,
            bool? affectSelfOverride = null)
        {
            if (impactor?.Vessel?.VesselStatus == null) return;

            var ss = impactor.Vessel.VesselStatus;
            var shipTransform = ss.ShipTransform;

            var init = new AOEExplosion.InitializeStruct
            {
                OwnDomain            = ss.Domain,
                Vessel               = ss.Vessel,
                MaxScale             = ComputeScaleForShip(ss, minExplosionScale, maxExplosionScale, resourceIndex)
                                       * Mathf.Max(0.01f, sizeMultiplier),
                // The blast at EMPTY resource - the width a conic blast keeps across the beam at
                // every charge, with charge buying capsule length along the vessel's gape instead
                // of radius. Carries the same Space multiplier as MaxScale so the two stay one
                // self-similar family and Space still cannot steal the angle the resource set.
                CoreScale            = minExplosionScale * Mathf.Max(0.01f, sizeMultiplier),
                OverrideMaterial     = overrideMaterial ? overrideMaterial : ss.AOEExplosionMaterial,
                AnnonymousExplosion  = false,
                SpawnPosition        = shipTransform.position + shipTransform.TransformDirection(localOffset),
                SpawnRotation        = shipTransform.rotation,
                // Same factor on the cone's reach, so the half-angle the resource chose survives.
                HeightOverride       = AuthoredConeHeight(aoePrefabs) * Mathf.Max(0.01f, sizeMultiplier),
                AffectSelfOverride   = affectSelfOverride,
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

        /// <summary>
        /// The authored axial reach of the first cone in the set, or 0 when the set holds none —
        /// which AOEConicExplosion reads as "no override, keep what the prefab says". Scaling from
        /// the prefab's own number keeps the art the source of the cone's baseline shape.
        /// </summary>
        static float AuthoredConeHeight(AOEExplosion[] prefabs)
        {
            if (prefabs == null) return 0f;
            foreach (var prefab in prefabs)
                if (prefab is AOEConicExplosion cone)
                    return cone.AuthoredHeight;
            return 0f;
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
