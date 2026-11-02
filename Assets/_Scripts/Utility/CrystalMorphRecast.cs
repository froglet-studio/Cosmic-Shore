using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// The hull cast for a target that does NOT surround the crystal: every vertex keeps its
    /// direction from the crystal's own middle (<paramref name="sourceCentre"/>) and is cast along it
    /// from the target's centre. Without it, a small target beside the crystal (the Shrike's flora
    /// bud, three units above where the crystal was) sees the whole cage in one narrow cone of
    /// directions and the plates pile onto one side of it.
    ///
    /// Built ON the shipped <see cref="CrystalMorphMeshBuilder.TryBuild(Mesh, in CrystalMorphMeshBuilder.ConvexHullTarget, float, float, out string)"/>
    /// without changing it: the hull is translated so its centre sits on the crystal's middle, cast
    /// plainly, and the landed points (TEXCOORD2) are translated back. A translation changes no
    /// normal and no direction, so frame 0 is still the crystal vertex for vertex and frame 1 lies
    /// on the real target. The Dolphin's PR (#1680) grows the same thing as a builder overload; once
    /// both land, this can forward to it.
    /// </summary>
    public static class CrystalMorphRecast
    {
        public static Mesh TryBuildAround(Mesh source, in CrystalMorphMeshBuilder.ConvexHullTarget target,
                                          Vector3 sourceCentre, float phaseStart, float phaseEnd,
                                          out string diagnosis)
        {
            if (!target.IsValid)
            {
                diagnosis = "the target hull is empty or malformed";
                return null;
            }

            Vector3 delta = target.Centre - sourceCentre;
            var corners = new Vector3[target.Corners.Length];
            for (int i = 0; i < corners.Length; i++) corners[i] = target.Corners[i] - delta;
            var shifted = new CrystalMorphMeshBuilder.ConvexHullTarget(sourceCentre, corners, target.Normals);

            var mesh = CrystalMorphMeshBuilder.TryBuild(source, in shifted, phaseStart, phaseEnd, out diagnosis);
            if (mesh == null) return null;

            var uv2 = new List<Vector4>();
            mesh.GetUVs(CrystalMorphMeshBuilder.TargetUVChannel, uv2);
            var bounds = source.bounds;
            for (int i = 0; i < uv2.Count; i++)
            {
                var p = uv2[i];
                uv2[i] = new Vector4(p.x + delta.x, p.y + delta.y, p.z + delta.z, p.w);
                bounds.Encapsulate(new Vector3(uv2[i].x, uv2[i].y, uv2[i].z));
            }
            for (int i = 0; i < target.Corners.Length; i++) bounds.Encapsulate(target.Corners[i]);
            mesh.SetUVs(CrystalMorphMeshBuilder.TargetUVChannel, uv2);
            mesh.bounds = bounds;
            return mesh;
        }
    }
}
