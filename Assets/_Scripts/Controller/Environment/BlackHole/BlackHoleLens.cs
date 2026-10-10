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
    /// around it — the gravitational lens — and the shadow. No painted accretion disc: what orbits
    /// the hole is the real mass the gravity field moves. The work is all in <c>BlackHoleLens.shader</c> /
    /// <c>BlackHoleLens.hlsl</c> (a per-pixel Schwarzschild ray trace); this component is the
    /// carrier: the lens SPHERE around the hole, sized to the lens (the shader draws its far side,
    /// so the lens is right from every viewpoint, including from inside it), and one
    /// <see cref="MaterialPropertyBlock"/> of per-hole numbers written each frame.
    ///
    /// The per-frame write is fine here and would not be on a prism: this is one renderer per hole
    /// (at most four), not mass — the clock-material law governs prisms, and a hole is not one.
    ///
    /// <para><b>The sky.</b> A ray bent off the screen shows the scene's OWN skybox, which
    /// <see cref="BlackHoleSky"/> renders into six faces while any lens is live — never URP's baked
    /// environment reflection, which is Unity's default sky until the scene's lighting is generated.</para>
    ///
    /// <para><b>What it bends.</b> <see cref="BlackHoleLensPass"/> copies the camera's colour AFTER the
    /// transparents and draws the lens from that copy, so alpha-blended mass (the snow shards,
    /// particles) behind the hole is bent with everything else. The lens also reads URP's depth
    /// texture (to leave opaque mass in front of the hole unbent), which the project has OFF in
    /// <c>URP_Asset</c>; <see cref="CameraSupport"/> turns it on for every enabled game camera only
    /// while at least one lens is live, and restores each camera's own setting when the last hole
    /// goes.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BlackHoleLens : MonoBehaviour
    {
        public const string MaterialResourcePath = "BlackHoleLens";

        static readonly int HorizonId = Shader.PropertyToID("_BHHorizon");
        static readonly int LensId = Shader.PropertyToID("_BHLens");
        static readonly int WhiteId = Shader.PropertyToID("_BHWhite");
        static readonly int CoreId = Shader.PropertyToID("_BHCore");
        static readonly int TintId = Shader.PropertyToID("_BHTint");
        static readonly int ThroatId = Shader.PropertyToID("_BHThroat");
        static readonly int SmoothId = Shader.PropertyToID("_BHSmooth");

        /// <summary>A smooth well's lens sphere, in core widths: its bend is u·e^(−u²/2), gone by u = 4.</summary>
        const float SmoothLensRadius = 4f;

        static readonly int SmoothCentreId = Shader.PropertyToID("_SmoothWellCentre");
        static readonly int SmoothStrengthId = Shader.PropertyToID("_SmoothWellStrength");
        static readonly int SmoothCountId = Shader.PropertyToID("_SmoothWellCount");
        static readonly Vector4[] s_smoothCentre = new Vector4[4];
        static readonly Vector4[] s_smoothStrength = new Vector4[4];
        static int s_publishedSmooth;

        /// <summary>
        /// Publish every smooth well (Docs/CRYSTAL_WORMHOLE.md) to the lens shader's global bank once a
        /// frame: centre, core width (eased by the warp weight), and the SIGNED lens strength scaled by
        /// the hole's amplitude. Every smooth lens sphere sums the whole bank, so overlapping lenses agree
        /// and an attractor and a repulsor meeting cancel. Skipped entirely while there are none.
        /// </summary>
        internal static void PublishSmoothWells(IReadOnlyList<BlackHole> holes)
        {
            int count = 0;
            for (int i = 0; i < holes.Count && count < s_smoothCentre.Length; i++)
            {
                var h = holes[i];
                if (h == null || !h.IsSmooth) continue;
                var p = h.transform.position;
                s_smoothCentre[count] = new Vector4(p.x, p.y, p.z, h.Softening * h.WarpWeight);
                s_smoothStrength[count] = new Vector4(h.Sign * h.LensStrength * h.Amplitude, 0f, 0f, 0f);
                count++;
            }
            if (count == 0 && s_publishedSmooth == 0) return;
            for (int i = count; i < s_smoothCentre.Length; i++)
            {
                s_smoothCentre[i] = Vector4.zero;
                s_smoothStrength[i] = Vector4.zero;
            }
            Shader.SetGlobalVectorArray(SmoothCentreId, s_smoothCentre);
            Shader.SetGlobalVectorArray(SmoothStrengthId, s_smoothStrength);
            Shader.SetGlobalFloat(SmoothCountId, count);
            s_publishedSmooth = count;
        }

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

        void OnEnable()
        {
            CameraSupport.Acquire();
            BlackHoleSky.Acquire();
            BlackHoleLensPass.Acquire();
        }

        void OnDisable()
        {
            CameraSupport.Release();
            BlackHoleSky.Release();
            BlackHoleLensPass.Release();
        }

        void LateUpdate()
        {
            if (_hole == null || _renderer == null) return;
            var config = BlackHoleRegistry.Config;

            // The hole grows in on spawn and shrinks away on despawn: the effective horizon rides
            // the same eased weight as the prism warp, so the shadow never pops.
            // A smooth well's "horizon" in the shader is its CORE WIDTH, and its lens a graded bulge.
            bool smooth = _hole.IsSmooth;
            float rs = (smooth ? _hole.Softening : _hole.HorizonRadius) * _hole.WarpWeight;
            float lensR = smooth ? SmoothLensRadius : config.LensRadiusMultiplier;
            transform.localScale = Vector3.one * Mathf.Max(2f * lensR * rs, 1e-3f);
            transform.localRotation = Quaternion.identity;

            _renderer.GetPropertyBlock(_block);
            _block.SetFloat(HorizonId, rs);
            // z: the trace's polarity. A HORIZON hole is traced +1 whatever its sign: outside the horizon a
            // white hole's spacetime is the black hole's, so it bends light the same way and its rays through
            // the horizon draw the core (Docs/BLACK_HOLE.md §11 — this branch's look, kept over the merge's
            // diverging source, §13). A smooth well (§12) has no horizon and keeps charming-cerf's signed lens.
            _block.SetVector(LensId, new Vector4(lensR, config.LensSteps, smooth ? _hole.Sign : 1f, config.LensFadeStart));
            _block.SetFloat(ThroatId, smooth ? 0f : _hole.ThroatRadius);
            // A flag for the smooth path; the strengths it sums come from the global bank.
            _block.SetFloat(SmoothId, smooth ? 1f : 0f);
            // A white hole's horizon emits (§11): a white-hot core over its disc. A smooth well has no horizon.
            _block.SetFloat(WhiteId, _hole.IsSource && !smooth ? 1f : 0f);
            // z, w: a black hole's photon ring (the studio's warm edge glow); none on a white hole or a smooth well.
            bool ring = !smooth && !_hole.IsSource;
            _block.SetVector(CoreId, new Vector4(config.WhiteCoreBrightness, config.WhiteCoreSkyMix,
                ring ? config.PhotonRingGlow : 0f, config.PhotonRingWidth));
            // An owned hole's domain tint (BlackHole.DomainTint): the shadow's dark, the core's light. 0 = untinted.
            var tint = _hole.DomainTint;
            _block.SetVector(TintId, new Vector4(tint.r, tint.g, tint.b, smooth ? 0f : _hole.DomainTintAmount));
            _renderer.SetPropertyBlock(_block);
        }

        /// <summary>
        /// The camera the player is LOOKING THROUGH: the last base game camera that rendered to the
        /// screen in the most recent frame — the image left on screen — else <see cref="Camera.main"/>.
        /// Not <c>Camera.main</c> first: in the real game the vessel's camera (CameraManager's
        /// "CM PlayerCam", its own Unity Camera) is UNTAGGED in Bootstrap, so while you fly,
        /// <c>Camera.main</c> is the menu's camera — still rendering, every camera at depth 0, and
        /// not the one on screen. Spawning "ahead of the camera" measures from this one.
        /// </summary>
        public static Camera ViewCamera()
        {
            var cam = CameraSupport.LastScreenCamera;
            return cam != null && cam.isActiveAndEnabled ? cam : Camera.main;
        }

        /// <summary>
        /// Keeps the depth texture on for EVERY enabled game camera while any lens is live, and
        /// restores each camera's own setting when the last one goes. Every camera, not one: the
        /// lens draws in whichever camera sees it. (While only <c>Camera.main</c> — the menu's — was
        /// patched, the vessel camera in lava-lamp freestyle had no scene copy and painted the whole
        /// 30 r_s lens sphere black.) The colour the lens bends is <see cref="BlackHoleLensPass"/>'s
        /// own after-transparents copy, so the opaque copy is no longer switched on. Owner-restores-
        /// only: it never clears a value it did not set. Also tracks <see cref="LastScreenCamera"/>
        /// for <see cref="ViewCamera"/>.
        /// </summary>
        internal static class CameraSupport
        {
            static int s_users;
            static readonly Dictionary<Camera, CameraOverrideOption> s_patched = new();
            static readonly List<Camera> s_stale = new();
            static Camera[] s_buffer = new Camera[8];

            /// <summary>The last base game camera that finished rendering to the screen.</summary>
            internal static Camera LastScreenCamera { get; private set; }

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

                // A camera switched on since last frame (a vessel spawn, the death or end camera)
                // is patched before it renders a lens.
                if (s_buffer.Length < Camera.allCamerasCount) s_buffer = new Camera[Mathf.NextPowerOfTwo(Camera.allCamerasCount)];
                int n = Camera.GetAllCameras(s_buffer);
                for (int i = 0; i < n; i++)
                {
                    var cam = s_buffer[i];
                    s_buffer[i] = null;
                    if (cam == null || cam.cameraType != CameraType.Game || s_patched.ContainsKey(cam)) continue;
                    var data = cam.GetUniversalAdditionalCameraData();
                    if (data == null || data.renderType != CameraRenderType.Base) continue;
                    s_patched[cam] = data.requiresDepthOption;
                    data.requiresDepthOption = CameraOverrideOption.On;
                }

                // Forget cameras that were destroyed (scene changes), so the set does not grow.
                foreach (var cam in s_patched.Keys)
                    if (cam == null) s_stale.Add(cam);
                foreach (var cam in s_stale) s_patched.Remove(cam);
                s_stale.Clear();
            }

            static void Restore()
            {
                foreach (var kv in s_patched)
                {
                    if (kv.Key == null) continue;
                    var data = kv.Key.GetUniversalAdditionalCameraData();
                    if (data == null) continue;
                    data.requiresDepthOption = kv.Value;
                }
                s_patched.Clear();
            }

            static void OnEndCameraRendering(ScriptableRenderContext context, Camera cam)
            {
                if (cam == null || cam.cameraType != CameraType.Game || cam.targetTexture != null) return;
                if (cam.TryGetComponent<UniversalAdditionalCameraData>(out var data) && data.renderType != CameraRenderType.Base) return;
                LastScreenCamera = cam;
            }

            /// <summary>Play-mode (re)entry: forget the previous session's cameras (they are gone).</summary>
            internal static void ResetOnLoad()
            {
                s_users = 0;
                s_patched.Clear();
                LastScreenCamera = null;
                RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
                RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
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
