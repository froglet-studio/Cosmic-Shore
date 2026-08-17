using System.Collections.Generic;
using CosmicShore.Gameplay;
using Reflex.Core;
using Reflex.Injectors;
using UnityEngine;
using CosmicShore.Data;
namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The world-space volume a conic blast sweeps, in the form its containment test wants:
    /// an apex, a sweep axis, the gape axis the capsule extends along, and the capsule's radius
    /// and half-length expressed PER UNIT DEPTH so both open out with the cone.
    ///
    /// A point p is inside iff, with <c>rel = p - Apex</c> and <c>s = dot(rel, Axis)</c> in
    /// <c>[0, Height]</c>, the distance from p to the cross-section's segment
    /// (<c>±TanGapePerUnit·s</c> along <see cref="GapeAxis"/>) is at most
    /// <c>TanCorePerUnit·s</c> — the point-to-SEGMENT distance a CapsuleCollider uses, which is
    /// what makes the ends round. Mirrors <c>AOEConicSweepQueryJob.Execute</c> exactly.
    /// </summary>
    public struct BlastVolume
    {
        public Vector3 Apex;
        public Vector3 Axis;
        public Vector3 GapeAxis;
        public float Height;
        public float TanCorePerUnit;
        public float TanGapePerUnit;
        public bool IsValid;

        /// <summary>
        /// Is <paramref name="worldPoint"/> standing inside this blast, and how deep?
        ///
        /// This is the CPU transcription of the volume test three other places already run:
        /// <c>AOEConicSweepQueryJob.Execute</c> (the damage sweep), <c>PrismDestructionSight.hlsl</c>
        /// (the prism highlight) and the capsule trigger. Clamp onto the cross-section's segment
        /// first, then measure distance to that point — that ordering is what makes the ends round,
        /// and copying it rather than approximating with a cone is why a vessel lights up exactly
        /// when the blast would actually reach it.
        ///
        /// <paramref name="fill01"/> comes back on the same edge-weighted curve the shader uses, so
        /// a highlighted VESSEL and the highlighted PRISMS around it brighten together instead of
        /// reading as two separate effects that happen to share a trigger.
        /// </summary>
        public readonly bool Contains(Vector3 worldPoint, out float fill01)
        {
            const float EdgePower = 2f;   // PRISM_SIGHT_EDGE_POWER
            const float CoreFill = 0.35f; // PRISM_SIGHT_CORE_FILL

            fill01 = 0f;
            if (!IsValid || Height <= 0f) return false;

            Vector3 rel = worldPoint - Apex;

            // The near clip is the apex: mass (and pilots) BEHIND the vessel are never inside,
            // even though the axis extends backwards mathematically.
            float s = Vector3.Dot(rel, Axis);
            if (s <= 0f || s > Height) return false;

            float coreRadius = TanCorePerUnit * s;
            if (coreRadius <= 0f) return false;

            Vector3 radial = rel - Axis * s;
            float halfLength = TanGapePerUnit * s;
            float along = Mathf.Clamp(Vector3.Dot(radial, GapeAxis), -halfLength, halfLength);
            Vector3 offAxis = radial - GapeAxis * along;

            float d = offAxis.magnitude;
            if (d > coreRadius) return false;

            float edge = Mathf.Clamp01(d / coreRadius);
            fill01 = Mathf.Lerp(CoreFill, 1f, Mathf.Pow(edge, EdgePower));
            return true;
        }
    }

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
        ///
        /// <paramref name="coreExplosionScale"/> is the CONIC blast's capsule DIAMETER — the width
        /// it keeps across the beam at every charge, with charge buying capsule LENGTH along the
        /// vessel's gape instead of radius. It is independent of
        /// <paramref name="minExplosionScale"/> (which is the blast's length when the resource is
        /// empty) precisely so a blast can start as a short capsule rather than a sphere. 0 means
        /// "no separate core": the capsule collapses to the plain circular cone, which is what the
        /// spherical blast and every non-conic caller want.
        ///
        /// <paramref name="coreMultiplier"/> scales the capsule's DIAMETER and nothing else — the
        /// one dimension <paramref name="sizeMultiplier"/> is forbidden to touch on its own. The two
        /// are deliberately separate axes of the same blast: size grows the cone self-similarly
        /// (reach and base together, preserving the half-angle the resource set), while the core
        /// multiplier fattens or thins the beam ACROSS that cone without moving its reach. A caller
        /// that wants one element to own thickness passes it here; 1 leaves the authored capsule
        /// exactly as the asset drew it.
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
            bool? affectSelfOverride = null,
            float coreExplosionScale = 0f,
            float coreMultiplier = 1f)
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
                // The width a conic blast keeps across the beam at every charge, with charge buying
                // capsule length along the vessel's gape instead of radius. Authored SEPARATELY
                // from the empty-resource length, so an uncharged blast can already be a short
                // capsule rather than a sphere. Carries the same Space multiplier as MaxScale, so
                // the two stay one self-similar family and Space still cannot steal the angle the
                // resource set. Falls back to the empty length (= a sphere at rest) when the
                // caller authors no core.
                CoreScale            = (coreExplosionScale > 0f ? coreExplosionScale : minExplosionScale)
                                       * Mathf.Max(0.01f, sizeMultiplier)
                                       * Mathf.Max(0.01f, coreMultiplier),
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

        /// <summary>
        /// Spawn + detonate a pre-built explosion at an arbitrary world position/scale. Used when
        /// the caller isn't a vessel or projectile (e.g. the Rhino energy sword exploding a crystal
        /// at the crystal's location, scaled by the energy consumed).
        /// </summary>
        public static void CreateExplosion(
            AOEExplosion[] aoePrefabs,
            AOEExplosion.InitializeStruct init,
            Container container)
        {
            SpawnAllAndDetonate(aoePrefabs, init, container);
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
        /// <summary>
        /// The world-space volume a conic blast would sweep if it detonated RIGHT NOW — the shape
        /// a preview has to draw to be honest about the ability.
        ///
        /// Built from exactly the same authored numbers, the same resource read and the same
        /// <paramref name="sizeMultiplier"/> as <see cref="CreateExplosion"/> above, and returned
        /// in the form the Burst sweep query tests against (apex + axis + gape axis + the two
        /// tangents per unit depth, see <c>AOEConicSweepQueryJob</c>). One construction of the
        /// blast's shape, two consumers — a preview computed from a private copy of this arithmetic
        /// would drift the first time anyone retuned a scale.
        /// </summary>
        public static bool TryResolveConicVolume(
            AOEExplosion[] aoePrefabs,
            IVesselStatus ss,
            float minExplosionScale,
            float maxExplosionScale,
            int resourceIndex,
            Vector3 localOffset,
            float sizeMultiplier,
            float coreExplosionScale,
            float coreMultiplier,
            out BlastVolume volume)
        {
            volume = default;

            var cone = FindCone(aoePrefabs);
            if (cone == null || ss?.ShipTransform == null) return false;

            float size = Mathf.Max(0.01f, sizeMultiplier);
            float height = cone.AuthoredHeight * size;
            if (height <= 0f) return false;

            float maxScale = ComputeScaleForShip(ss, minExplosionScale, maxExplosionScale, resourceIndex) * size;
            float coreScale = (coreExplosionScale > 0f ? coreExplosionScale : minExplosionScale)
                              * size * Mathf.Max(0.01f, coreMultiplier);

            // Same clamp AOEConicExplosion.Initialize applies: the core can never exceed the base,
            // and a caller that authors none collapses the capsule to a plain circular cone.
            coreScale = coreScale > 0f ? Mathf.Min(coreScale, maxScale) : maxScale;

            var shipTransform = ss.ShipTransform;

            // The blast is spawned at the ship's rotation, so the gape axis is the authored
            // container-local direction taken into the ship's frame, with any component along the
            // sweep axis removed - exactly what AOEConicExplosion does to build _gapeAxisWorld.
            Vector3 axis = shipTransform.forward;
            Vector3 gape = shipTransform.TransformDirection(cone.AuthoredGapeAxis);
            gape -= axis * Vector3.Dot(gape, axis);
            gape = gape.sqrMagnitude > 1e-8f ? gape.normalized : shipTransform.up;

            volume = new BlastVolume
            {
                Apex = shipTransform.position + shipTransform.TransformDirection(localOffset),
                Axis = axis,
                GapeAxis = gape,
                Height = height,
                TanCorePerUnit = (coreScale * 0.5f) / height,
                TanGapePerUnit = ((maxScale - coreScale) * 0.5f) / height,
                IsValid = true,
            };
            return true;
        }

        static AOEConicExplosion FindCone(AOEExplosion[] prefabs)
        {
            if (prefabs == null) return null;
            foreach (var prefab in prefabs)
                if (prefab is AOEConicExplosion cone)
                    return cone;
            return null;
        }

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
