using System.Collections.Generic;
using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The I4 gauntlet's RAYS (SWITCHBACK.md "The I4 gauntlet"; Yash, 2026-11-03: "the pathway can be highlighted with some
    /// rays"): a thin beam of light down every segment of the way through each blocked gate - the lead-in to its first
    /// key, key to key, the last key to the door, and on through the ring - from <see cref="SwitchbackBlastRun.RaySegments"/>.
    ///
    /// <para>Each ray is a faceted hexagonal volume, lit and glowing (<c>Resources/SwitchbackPathRay.mat</c>: URP Lit with
    /// emission, #E8F6FF, the Blue family's white like every course feature), never a flat line: nothing in the world
    /// looks flat. The whole course is ONE static mesh and one draw, built once when the course is raised. No collider
    /// and no physics layer: a ray is light, so no pilot, sense, sweep or blast ever meets it. Every peer builds its own
    /// from the broadcast course, so nothing about it is networked.</para>
    /// </summary>
    public sealed class SwitchbackPathRays : MonoBehaviour
    {
        /// <summary><c>Resources/SwitchbackPathRay.mat</c>.</summary>
        public const string MaterialResourcePath = "SwitchbackPathRay";
        static Material s_material;
        static bool s_warned;

        Mesh _mesh;

        /// <summary>The rays of <paramref name="layout"/>'s gauntlet on <paramref name="course"/>, <paramref name="radius"/>
        /// u thick, as a new root object in <paramref name="scene"/> (the mode's own, so it goes when the mode's scene does,
        /// even when another scene is the active one); null (nothing drawn) below I4 or without the material.</summary>
        public static SwitchbackPathRays Create(SwitchbackFeatureLayout layout, IReadOnlyList<RaceGate> course, float radius,
                                                Scene scene = default)
        {
            var segments = new List<Vector3>(64);
            SwitchbackBlastRun.RaySegments(layout, course, segments);
            if (segments.Count < 2 || radius <= 0f) return null;
            var material = SharedMaterial();
            if (!material) return null;

            var verts = new List<Vector3>(segments.Count * 20);
            var normals = new List<Vector3>(segments.Count * 20);
            var tris = new List<int>(segments.Count * 36);
            for (int i = 0; i + 1 < segments.Count; i += 2)
                SwitchbackRayMesh.AppendBeam(segments[i], segments[i + 1], radius, verts, normals, tris);
            if (tris.Count == 0) return null;

            var go = new GameObject("SwitchbackPathRays");
            if (scene.IsValid() && scene.isLoaded) SceneManager.MoveGameObjectToScene(go, scene);
            var rays = go.AddComponent<SwitchbackPathRays>();
            rays._mesh = new Mesh { name = "SwitchbackPathRays" };
            if (verts.Count > 65000) rays._mesh.indexFormat = IndexFormat.UInt32;
            rays._mesh.SetVertices(verts);
            rays._mesh.SetNormals(normals);
            rays._mesh.SetTriangles(tris, 0);
            rays._mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = rays._mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return rays;
        }

        static Material SharedMaterial()
        {
            if (s_material) return s_material;
            s_material = Resources.Load<Material>(MaterialResourcePath);
            WorldColorTrace.Paint(s_material);   // loaded after the scene's repaint: born in the worn colour set
            if (!s_material && !s_warned)
            {
                s_warned = true;
                CSDebug.LogError($"[Switchback] Resources/{MaterialResourcePath}.mat is missing: the I4 gauntlet's path rays are not drawn.");
            }
            return s_material;
        }

        void OnDestroy()
        {
            if (_mesh) Destroy(_mesh);
        }
    }
}
