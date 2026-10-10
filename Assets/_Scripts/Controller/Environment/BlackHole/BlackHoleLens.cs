using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// What the player SEES of the black and white holes (Docs/BLACK_HOLE.md §5.1): the Vessel Studio's lens,
    /// ported line for line (Docs/Studios/StoatFlightStudio.html, <c>lensMat</c> / <c>setLensUniforms</c>).
    /// EVERY hole the camera sees is drawn in ONE full-screen pass (<see cref="BlackHoleLensPass"/> +
    /// <c>BlackHoleLens.shader</c>): each hole adds a displacement to where the pixel samples the scene, the pixel
    /// samples it once, then a black hole's shadow and photon ring and a white hole's white-hot core are laid
    /// over it. No painted accretion disc: what orbits the hole is the real mass the gravity field moves.
    ///
    /// <para><b>Why one pass (2026-10-10).</b> Unity used to draw one lens SPHERE per hole, each bending a copy
    /// of the scene taken before any lens. A Stoat pair's spheres overlap, so the one drawn last painted over its
    /// partner (the black hole hid the white hole), and each sphere swapped in the skybox wherever a bent ray
    /// landed on something in front of the hole, which in lava lamp read as a large disc round the hole. A sum
    /// cannot depend on the order of the holes, and nothing is swapped in.</para>
    ///
    /// <para>This component is the per-hole marker: while it is enabled the hole is in <see cref="Live"/>, the
    /// pass is enqueued and the cameras keep their depth texture. Its numbers are read per camera by
    /// <see cref="ScreenWells"/>, which is the studio's <c>setLensUniforms</c> in C#.</para>
    ///
    /// <para><b>What it bends.</b> <see cref="BlackHoleLensPass"/> copies the camera's colour AFTER the
    /// transparents and draws the lens from that copy, so alpha-blended mass (the snow shards, particles) is bent
    /// with everything else. The lens also reads URP's depth texture (a vessel in front of the hole is not bent),
    /// which the project has OFF in <c>URP_Asset</c>; <see cref="CameraSupport"/> turns it on for every enabled
    /// game camera only while at least one lens is live, and restores each camera's own setting after.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BlackHoleLens : MonoBehaviour
    {
        public const string MaterialResourcePath = "BlackHoleLens";

        /// <summary>The most holes one pass draws, the nearest first (<c>BLACK_HOLE_LENS_MAX_WELLS</c>).</summary>
        public const int MaxWells = 8;

        /// <summary>The kinds the shader draws (<c>_BHWellP.x</c>).</summary>
        public const float KindBlackHole = 1f, KindWhiteHole = 2f, KindSmoothAttractor = 3f, KindSmoothRepulsor = 4f;

        static readonly List<BlackHoleLens> s_live = new();

        /// <summary>Every enabled lens: the holes the pass draws.</summary>
        public static IReadOnlyList<BlackHoleLens> Live => s_live;

        static Material s_material;
        static bool s_materialResolved;

        BlackHole _hole;

        /// <summary>The hole this lens draws.</summary>
        public BlackHole Hole => _hole;

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
            if (SharedMaterial == null) return null;
            var go = new GameObject("Lens");
            go.transform.SetParent(hole.transform, false);
            var lens = go.AddComponent<BlackHoleLens>();
            lens._hole = hole;
            // AddComponent ran OnEnable before the hole was set: register now that the lens knows its hole.
            if (lens.isActiveAndEnabled && !s_live.Contains(lens)) s_live.Add(lens);
            return lens;
        }

        void OnEnable()
        {
            if (_hole != null && !s_live.Contains(this)) s_live.Add(this);
            CameraSupport.Acquire();
            BlackHoleLensPass.Acquire();
        }

        void OnDisable()
        {
            s_live.Remove(this);
            CameraSupport.Release();
            BlackHoleLensPass.Release();
        }

        /// <summary>One hole as the lens needs it, in world terms (<see cref="FromHole"/>).</summary>
        public struct Well
        {
            /// <summary>The hole's centre.</summary>
            public Vector3 Position;
            /// <summary>A horizon hole's eased horizon r_s; a smooth well's eased core width.</summary>
            public float Radius;
            /// <summary><see cref="KindBlackHole"/>, <see cref="KindWhiteHole"/>, <see cref="KindSmoothAttractor"/> or <see cref="KindSmoothRepulsor"/>.</summary>
            public float Kind;
            /// <summary>A horizon hole's lens bend (× Einstein); a smooth well's graded strength A (capped at 0.95).</summary>
            public float LensStrength;
            /// <summary>The owner's domain colour and how far the shadow / core take it (0 = the studio's look).</summary>
            public Color Tint;
            public float TintAmount;
        }

        /// <summary>
        /// A hole's lens numbers, or false when it draws nothing (eased out). The studio's wells: a black hole
        /// and a white hole bend with the config's lens strengths, a smooth well (the crystal pair) with its own A.
        /// </summary>
        public static bool FromHole(BlackHole hole, BlackHoleConfigSO config, out Well well)
        {
            well = default;
            if (hole == null) return false;
            bool smooth = hole.IsSmooth;
            float radius = (smooth ? hole.Softening : hole.HorizonRadius) * hole.WarpWeight;
            if (!(radius > 1e-4f)) return false;
            well.Position = hole.transform.position;
            well.Radius = radius;
            if (smooth)
            {
                well.Kind = hole.IsSource ? KindSmoothRepulsor : KindSmoothAttractor;
                well.LensStrength = Mathf.Min(0.95f, Mathf.Max(0f, hole.LensStrength * hole.Amplitude));
            }
            else
            {
                well.Kind = hole.IsSource ? KindWhiteHole : KindBlackHole;
                well.LensStrength = hole.IsSource ? config.WhiteLensStrength : config.LensStrength;
                well.Tint = hole.DomainTint;
                well.TintAmount = hole.DomainTintAmount;
            }
            return true;
        }

        /// <summary>
        /// One hole as a camera sees it: the studio's <c>setLensUniforms</c>, per hole. Screen units are screen
        /// heights from the centre, y up; an angle θ from the view axis lands at f·tan θ, f = 0.5 / tan(fov_y / 2).
        /// <paramref name="c"/>: xy the centre on screen, z its depth along the view axis, w 1.
        /// <paramref name="p"/>: x the kind, y the angular radius r_c, z a smooth well's A, w a horizon hole's
        /// Einstein term θ_E² × lens strength (θ_E = f·tan √(2 r_s / D)).
        /// <paramref name="m"/>: x the lens reach (f·tan atan(reach·r_s / D)), w the foreground margin (2.6 r_s; a
        /// smooth well's core). False for a hole behind the camera or within a unit of its near plane, as in the studio.
        /// </summary>
        public static bool ScreenWell(in Well well, Matrix4x4 worldToCamera, Vector3 cameraPosition, float fieldOfViewY,
            float nearClip, float lensReach, out Vector4 c, out Vector4 p, out Vector4 m)
        {
            c = p = m = default;
            var view = worldToCamera.MultiplyPoint3x4(well.Position);
            float depth = -view.z;
            if (depth <= nearClip + 1f) return false;
            float f = 0.5f / Mathf.Tan(fieldOfViewY * Mathf.Deg2Rad * 0.5f);
            float distance = Vector3.Distance(cameraPosition, well.Position);
            float rs = well.Radius;
            c = new Vector4(f * view.x / depth, f * view.y / depth, depth, 1f);
            if (well.Kind < 2.5f)
            {
                float einstein = f * Mathf.Tan(Mathf.Min(1.2f, Mathf.Sqrt(2f * rs / distance)));
                p = new Vector4(well.Kind, Angular(f, rs, distance), 0f, einstein * einstein * Mathf.Max(0f, well.LensStrength));
                m = new Vector4(f * Mathf.Tan(Mathf.Min(1.45f, Mathf.Atan(lensReach * rs / distance))), 0f, 0f, 2.6f * rs);
            }
            else
            {
                p = new Vector4(well.Kind, Angular(f, rs, distance), Mathf.Min(0.95f, well.LensStrength), 0f);
                m = new Vector4(0f, 0f, 0f, rs);
            }
            return true;
        }

        /// <summary>The studio's <c>ang(r)</c>: the screen radius of a sphere of radius r at distance D.</summary>
        public static float Angular(float f, float r, float distance) =>
            f * Mathf.Tan(Mathf.Min(1.45f, Mathf.Asin(Mathf.Min(0.999f, r / Mathf.Max(distance, r * 1.001f)))));

        /// <summary>The config's look, as the shader takes it: x kShadow, y kCore, z ring glow, w ring width.</summary>
        public static Vector4 Look(BlackHoleConfigSO config) => new(config.ShadowSize, config.WhiteCoreSize,
            config.PhotonRingGlow, Mathf.Max(0.005f, config.PhotonRingWidth));

        /// <summary>x lens fade start, y white core brightness, z white core sky mix, w the camera's aspect.</summary>
        public static Vector4 Look2(BlackHoleConfigSO config, float aspect) => new(Mathf.Min(0.99f, config.LensFadeStart),
            config.WhiteCoreBrightness, config.WhiteCoreSkyMix, aspect);

        static readonly Well[] s_wells = new Well[32];
        static readonly float[] s_depth = new float[32];
        static readonly int[] s_order = new int[32];

        /// <summary>
        /// Every live hole <paramref name="cam"/> sees, the nearest <see cref="MaxWells"/> first, into the four rows
        /// the shader reads (<c>_BHWellC/P/M/T</c>). Returns how many rows are filled; the rest are zeroed.
        /// </summary>
        public static int ScreenWells(Camera cam, BlackHoleConfigSO config, Vector4[] c, Vector4[] p, Vector4[] m, Vector4[] t)
        {
            int candidates = 0;
            var eye = cam.transform.position;
            for (int i = 0; i < s_live.Count && candidates < s_wells.Length; i++)
            {
                var lens = s_live[i];
                if (lens == null || !FromHole(lens._hole, config, out var well)) continue;
                s_wells[candidates] = well;
                s_depth[candidates] = (well.Position - eye).sqrMagnitude;
                s_order[candidates] = candidates;
                candidates++;
            }
            // nearest first (insertion sort: a handful of holes)
            for (int i = 1; i < candidates; i++)
            {
                int k = s_order[i];
                int j = i - 1;
                while (j >= 0 && s_depth[s_order[j]] > s_depth[k]) { s_order[j + 1] = s_order[j]; j--; }
                s_order[j + 1] = k;
            }

            int count = 0;
            var worldToCamera = cam.worldToCameraMatrix;
            for (int i = 0; i < candidates && count < MaxWells && count < c.Length; i++)
            {
                ref var well = ref s_wells[s_order[i]];
                if (!ScreenWell(well, worldToCamera, eye, cam.fieldOfView, cam.nearClipPlane, config.LensRadiusMultiplier,
                        out c[count], out p[count], out m[count])) continue;
                t[count] = new Vector4(well.Tint.r, well.Tint.g, well.Tint.b, Mathf.Clamp01(well.TintAmount));
                count++;
            }
            for (int i = count; i < c.Length; i++) c[i] = p[i] = m[i] = t[i] = Vector4.zero;
            return count;
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
        /// patched, the vessel camera in lava-lamp freestyle had no scene copy and painted the old
        /// lens sphere black.) The colour the lens bends is <see cref="BlackHoleLensPass"/>'s
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
            s_live.Clear();
            s_materialResolved = false;
            s_material = null;
        }
    }
}
