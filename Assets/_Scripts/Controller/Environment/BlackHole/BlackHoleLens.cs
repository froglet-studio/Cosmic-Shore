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
    /// carrier: a camera-facing quad at the hole sized to the lens, and one
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

        static Mesh s_quad;
        static Material s_material;
        static bool s_materialResolved;

        BlackHole _hole;
        MeshRenderer _renderer;
        MaterialPropertyBlock _block;

        /// <summary>
        /// The shared lens material, or null when it cannot be found (the hole then falls back to
        /// a plain black sphere — never an invisible hole).
        /// </summary>
        public static Material SharedMaterial
        {
            get
            {
                if (!s_materialResolved)
                {
                    s_material = Resources.Load<Material>(MaterialResourcePath);
                    if (s_material == null || s_material.shader == null || !s_material.shader.isSupported)
                    {
                        CSDebug.LogWarning("[BlackHole] Resources/BlackHoleLens.mat is missing or its shader is unsupported on this device; holes draw as plain black spheres.");
                        s_material = null;
                    }
                    s_materialResolved = true;
                }
                return s_material;
            }
        }

        /// <summary>Build the lens under <paramref name="hole"/>. Null when the material is unavailable.</summary>
        internal static BlackHoleLens Create(BlackHole hole)
        {
            var material = SharedMaterial;
            if (material == null) return null;

            var go = new GameObject("Lens");
            go.transform.SetParent(hole.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = Quad();
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
        /// A unit quad in XY with BOUNDS that are a unit cube, so a uniform scale of the lens
        /// diameter gives the renderer culling bounds that contain the billboard from every
        /// viewing angle (the vertex stage turns it toward the camera, which the mesh bounds
        /// cannot know).
        /// </summary>
        static Mesh Quad()
        {
            if (s_quad != null) return s_quad;
            s_quad = new Mesh { name = "BlackHoleLensQuad" };
            s_quad.SetVertices(new[]
            {
                new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f),
            });
            s_quad.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
            s_quad.bounds = new Bounds(Vector3.zero, Vector3.one);
            s_quad.hideFlags = HideFlags.DontSave;
            return s_quad;
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
