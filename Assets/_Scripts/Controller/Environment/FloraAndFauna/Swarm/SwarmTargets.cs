using System.Collections.Generic;
using UnityEngine;
using SVector3 = System.Numerics.Vector3;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Round 8 (Docs/SWARM_FAUNA.md §16.1): the Unity-typed front door to the swarm member query. A weapon that
    /// finds PRISMS through <see cref="PrismSpatialIndex"/> builds the SAME volume here with the SAME arguments it
    /// hands the index, and gets back the swarm members that are only data (no GameObject) standing in it. Each
    /// constructor is the matching <see cref="SwarmVolume"/> - the shipped prism test, transcribed and proven
    /// against the Burst code by Tools/Build/swarm_core_harness (R8a) - so a member is hit exactly where its body
    /// prism would have been.
    /// </summary>
    public static class SwarmTargets
    {
        static SVector3 N(Vector3 v) => new(v.x, v.y, v.z);

        /// <summary>PrismSpatialIndex.QuerySphere / the spherical explosion.</summary>
        public static SwarmVolume Sphere(Vector3 centre, float radius) => SwarmVolume.Sphere(N(centre), radius);

        /// <summary>PrismSpatialIndex.QuerySegment - a projectile's swept step.</summary>
        public static SwarmVolume Capsule(Vector3 a, Vector3 b, float radius) => SwarmVolume.Capsule(N(a), N(b), radius);

        /// <summary>PrismSpatialIndex.QueryCone - the sniper's round.</summary>
        public static SwarmVolume Cone(Vector3 apex, Vector3 direction, float length, float halfAngleDegrees, float minRadius) =>
            SwarmVolume.Cone(N(apex), N(direction), length, halfAngleDegrees, minRadius);

        /// <summary>PrismSpatialIndex.ProcessExplosionConeFrame's slab - the Dolphin's cone.</summary>
        public static SwarmVolume ConeSlab(Vector3 apex, Vector3 axis, Vector3 gapeAxis, float sliceMin, float sliceMax,
                                           float tanCoreHalfAngle, float tanGapePerUnit) =>
            SwarmVolume.ConeSlab(N(apex), N(axis), N(gapeAxis), sliceMin, sliceMax, tanCoreHalfAngle, tanGapePerUnit);

        /// <summary>PrismSpatialIndex.ProcessExplosionCylinderFrame's slab - the Scarab's plate.</summary>
        public static SwarmVolume CylinderSlab(Vector3 origin, Vector3 axis, float sliceMin, float sliceMax, float radius, bool mirrored) =>
            SwarmVolume.CylinderSlab(N(origin), N(axis), sliceMin, sliceMax, radius, mirrored);

        /// <summary>Members whose BODY prism centre is in <paramref name="v"/> (what a prism-damaging weapon tests).</summary>
        public static int Bodies(in SwarmVolume v, List<SwarmFauna.MemberHit> results) => SwarmFauna.CollectMembers(v, false, results);

        /// <summary>Members whose HEART is in <paramref name="v"/> (what a lifeform-crystal effect tests).</summary>
        public static int Hearts(in SwarmVolume v, List<SwarmFauna.MemberHit> results) => SwarmFauna.CollectMembers(v, true, results);

        /// <summary>True while any GPU-drawn swarm is alive - lets a weapon skip the member pass outright.</summary>
        public static bool Any => SwarmFauna.Live.Count > 0;
    }
}
