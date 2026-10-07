using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// What the player SEES of a black hole (Docs/BLACK_HOLE.md §5.1): the scene behind it bent
    /// around it — the gravitational lens — the shadow, and the accretion disc lensed over the top
    /// and bottom of the shadow. The work is all in <c>BlackHoleLens.shader</c> /
    /// <c>BlackHoleLens.hlsl</c> (a per-pixel Schwarzschild ray trace); this component is the
    /// carrier: the lens SPHERE around the hole, sized to the lens (the shader draws its far side,
    /// so the lens is right from every viewpoint, including from inside it), and one
    /// <see cref="MaterialPropertyBlock"/> of per-hole numbers written each frame.
    ///
    /// The per-frame write is fine here and would not be on a prism: this is one renderer per hole
    /// (at most four), not mass — the clock-material law governs prisms, and a hole is not one.
    ///
    /// <para><b>The camera textures.</b> The lens reads URP's opaque-scene copy and depth texture,
    /// which the project has OFF in <c>URP_Asset</c> (they cost a copy every frame). Rather than
    /// switching them on for every scene, <see cref="CameraSupport"/> turns them on for the MAIN
    /// camera only while at least one lens is live, follows the main camera if it changes, and
    /// restores the camera's own settings when the last hole goes.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BlackHoleLens : MonoBehaviour
    {
        public const string MaterialResourcePath = "BlackHoleLens";

        static readonly int HorizonId = Shader.PropertyToID("_BHHorizon");
        static readonly int LensId = Shader.PropertyToID("_BHLens");
        static readonly int SpinId = Shader.PropertyToID("_BHSpin");
        static readonly int DiskId = Shader.PropertyToID("_BHDisk");
        static readonly int Disk2Id = Shader.PropertyToID("_BHDisk2");

        /// <summary>Icosahedron subdivisions of <see cref="LensSphere"/> (2 = 320 triangles).</summary>
        const int LensSphereSubdivisions = 2;

        static Mesh s_sphere;
        static Material s_material;
        static bool s_materialResolved;

        BlackHole _hole;
        MeshRenderer _renderer;
        MaterialPropertyBlock _block;

        /// <summary>
        /// The shared lens material, or null when it cannot be drawn (the hole then falls back to
        /// a plain black sphere — never an invisible hole, and never Unity's magenta error shader).
        /// </summary>
        public static Material SharedMaterial
        {
            get
            {
                if (!s_materialResolved)
                {
                    s_material = Resources.Load<Material>(MaterialResourcePath);
                    if (!IsDrawable(s_material, out string reason))
                    {
                        CSDebug.LogWarning($"[BlackHole] The lens cannot draw: {reason}. Holes draw as plain black spheres.");
                        s_material = null;
                    }
                    s_materialResolved = true;
                }
                return s_material;
            }
        }

        /// <summary>
        /// Whether <paramref name="material"/> renders as the lens rather than as nothing or as
        /// magenta. <c>Shader.isSupported</c> alone is not enough: a shader that FAILED TO COMPILE
        /// still reports supported and draws as Unity's error shader, so in the Editor the compile
        /// state is asked directly and its first error is handed back as the reason.
        /// </summary>
        public static bool IsDrawable(Material material, out string reason)
        {
            if (material == null)
            {
                reason = $"Resources/{MaterialResourcePath}.mat is missing";
                return false;
            }
            var shader = material.shader;
            if (shader == null)
            {
                reason = $"{material.name} has no shader";
                return false;
            }
            if (!shader.isSupported)
            {
                reason = $"{shader.name} is not supported on this device";
                return false;
            }
#if UNITY_EDITOR
            if (UnityEditor.ShaderUtil.ShaderHasError(shader))
            {
                reason = $"{shader.name} failed to compile ({FirstCompileError(shader)})";
                return false;
            }
#endif
            reason = null;
            return true;
        }

#if UNITY_EDITOR
        static string FirstCompileError(Shader shader)
        {
            foreach (var message in UnityEditor.ShaderUtil.GetShaderMessages(shader))
            {
                if (message.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)
                    return $"{message.message} at {message.file}:{message.line}";
            }
            return "see the shader's Inspector";
        }
#endif

        /// <summary>Build the lens under <paramref name="hole"/>. Null when the material is unavailable.</summary>
        internal static BlackHoleLens Create(BlackHole hole)
        {
            var material = SharedMaterial;
            if (material == null) return null;

            var go = new GameObject("Lens");
            go.transform.SetParent(hole.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = LensSphere();
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            var lens = go.AddComponent<BlackHoleLens>();
            lens._hole = hole;
            lens._renderer = renderer;
            lens._block = new MaterialPropertyBlock();
            return lens;
        }

        /// <summary>
        /// The lens volume: an icosphere that CIRCUMSCRIBES the unit-diameter sphere — every face
        /// lies at or outside radius 0.5 — with every face wound outward. A uniform scale of the
        /// lens diameter then covers every ray that passes through the lens, from outside it or
        /// from inside it, and the shader's <c>Cull Front</c> draws exactly one layer: the far
        /// side. The shader discards past the true lens radius, so the facets never show.
        /// </summary>
        public static Mesh LensSphere()
        {
            if (s_sphere != null) return s_sphere;

            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var vertices = new List<Vector3>
            {
                new(-1f, t, 0f), new(1f, t, 0f), new(-1f, -t, 0f), new(1f, -t, 0f),
                new(0f, -1f, t), new(0f, 1f, t), new(0f, -1f, -t), new(0f, 1f, -t),
                new(t, 0f, -1f), new(t, 0f, 1f), new(-t, 0f, -1f), new(-t, 0f, 1f),
            };
            for (int i = 0; i < vertices.Count; i++) vertices[i] = vertices[i].normalized;
            var triangles = new List<int>
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11, 1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9, 4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1,
            };

            var midpoints = new Dictionary<long, int>();
            int Midpoint(int a, int b)
            {
                long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                if (midpoints.TryGetValue(key, out int index)) return index;
                vertices.Add(((vertices[a] + vertices[b]) * 0.5f).normalized);
                midpoints[key] = vertices.Count - 1;
                return vertices.Count - 1;
            }

            for (int level = 0; level < LensSphereSubdivisions; level++)
            {
                var next = new List<int>(triangles.Count * 4);
                for (int i = 0; i < triangles.Count; i += 3)
                {
                    int a = triangles[i], b = triangles[i + 1], c = triangles[i + 2];
                    int ab = Midpoint(a, b), bc = Midpoint(b, c), ca = Midpoint(c, a);
                    next.Add(a); next.Add(ab); next.Add(ca);
                    next.Add(b); next.Add(bc); next.Add(ab);
                    next.Add(c); next.Add(ca); next.Add(bc);
                    next.Add(ab); next.Add(bc); next.Add(ca);
                }
                triangles = next;
                midpoints.Clear();
            }

            // Wind every face outward (Unity's front face has cross(b − a, c − a) toward the viewer)
            // and find the face plane closest to the centre; push the vertices out so that plane
            // sits at radius 0.5. Done here rather than trusted to the tables above.
            float closest = 1f;
            for (int i = 0; i < triangles.Count; i += 3)
            {
                Vector3 a = vertices[triangles[i]], b = vertices[triangles[i + 1]], c = vertices[triangles[i + 2]];
                var normal = Vector3.Cross(b - a, c - a);
                if (Vector3.Dot(normal, a + b + c) < 0f)
                {
                    (triangles[i + 1], triangles[i + 2]) = (triangles[i + 2], triangles[i + 1]);
                    normal = -normal;
                }
                closest = Mathf.Min(closest, Vector3.Dot(normal.normalized, a));
            }
            float radius = 0.5f / closest;
            for (int i = 0; i < vertices.Count; i++) vertices[i] *= radius;

            s_sphere = new Mesh { name = "BlackHoleLensSphere", hideFlags = HideFlags.DontSave };
            s_sphere.SetVertices(vertices);
            s_sphere.SetTriangles(triangles, 0);
            s_sphere.RecalculateBounds();
            return s_sphere;
        }

        void OnEnable() => CameraSupport.Acquire();
        void OnDisable() => CameraSupport.Release();

        void LateUpdate()
        {
            if (_hole == null || _renderer == null) return;
            var config = BlackHoleRegistry.Config;

            // The hole grows in on spawn and shrinks away on despawn: the effective horizon rides
            // the same eased weight as the prism warp, so the shadow never pops.
            float rs = _hole.HorizonRadius * _hole.WarpWeight;
            float lensR = config.LensRadiusMultiplier;
            transform.localScale = Vector3.one * Mathf.Max(2f * lensR * rs, 1e-3f);
            transform.localRotation = Quaternion.identity;

            var axis = _hole.SpinAxis;
            float density = config.DiskBaseDensity + _hole.DiskFeed;

            _renderer.GetPropertyBlock(_block);
            _block.SetFloat(HorizonId, rs);
            _block.SetVector(LensId, new Vector4(lensR, config.LensSteps, 1f, config.LensFadeStart));
            _block.SetVector(SpinId, new Vector4(axis.x, axis.y, axis.z, 0f));
            _block.SetVector(DiskId, new Vector4(config.DiskInnerMultiplier, config.DiskOuterMultiplier, density, config.DiskBrightness));
            _block.SetVector(Disk2Id, new Vector4(config.DiskPeakTemperature, config.DiskDoppler,
                Time.time * config.DiskSpinSpeed, config.DiskNoiseScale));
            _renderer.SetPropertyBlock(_block);
        }

        /// <summary>
        /// Keeps the main camera's opaque and depth textures on while any lens is live, and
        /// restores what the camera had before when the last one goes. Follows the main camera if
        /// it changes (a vessel spawn swaps cameras). Owner-restores-only: it never clears a value
        /// it did not set.
        /// </summary>
        internal static class CameraSupport
        {
            static int s_users;
            static Camera s_patched;
            static CameraOverrideOption s_savedColor;
            static CameraOverrideOption s_savedDepth;

            internal static void Acquire()
            {
                s_users++;
                Maintain();
            }

            internal static void Release()
            {
                s_users = Mathf.Max(0, s_users - 1);
                if (s_users == 0) Restore();
            }

            /// <summary>Called every frame by the registry's driver while holes are live.</summary>
            internal static void Maintain()
            {
                if (s_users == 0) { Restore(); return; }
                var cam = Camera.main;
                if (cam == s_patched) return;
                Restore();
                if (cam == null) return;
                var data = cam.GetUniversalAdditionalCameraData();
                if (data == null) return;
                s_savedColor = data.requiresColorOption;
                s_savedDepth = data.requiresDepthOption;
                data.requiresColorOption = CameraOverrideOption.On;
                data.requiresDepthOption = CameraOverrideOption.On;
                s_patched = cam;
            }

            static void Restore()
            {
                if (s_patched == null) { s_patched = null; return; }
                var data = s_patched.GetUniversalAdditionalCameraData();
                if (data != null)
                {
                    data.requiresColorOption = s_savedColor;
                    data.requiresDepthOption = s_savedDepth;
                }
                s_patched = null;
            }

            /// <summary>Play-mode (re)entry: forget the previous session's camera (it is gone).</summary>
            internal static void ResetOnLoad()
            {
                s_users = 0;
                s_patched = null;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ResetOnLoad()
        {
            CameraSupport.ResetOnLoad();
            s_materialResolved = false;
            s_material = null;
        }
    }
}
