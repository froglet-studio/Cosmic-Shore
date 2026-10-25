using System.Collections.Generic;
using CosmicShore.Utility;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The vortex ring's LENS (Docs/BLACK_HOLE.md §5.3): a subtle doughnut of bent light round a ring's core — the
    /// black hole's warp laid along a circle instead of round a point, with no shadow and no singularity (Garrett,
    /// 2026-10-24: "a cool warping effect like the black holes use but convey a more subtle toroid").
    ///
    /// <para><b>How to use it.</b> Every frame a ring should bend light, call <see cref="Set"/> with a key that names
    /// it (the ring object itself), and <see cref="Remove"/> it when the ring dies. A ring not Set for
    /// <see cref="StaleFrames"/> frames is dropped, so a destroyed owner never leaves a lens behind. The first live
    /// ring switches the depth texture on (through <see cref="BlackHoleLens.CameraSupport"/>) and the render pass on;
    /// the last one gone switches both off, so a scene with no ring pays nothing.</para>
    ///
    /// <para><b>Any camera.</b> The ring's screen ellipse is worked out per camera from that camera's REAL
    /// <c>projectionMatrix</c> and <c>worldToCameraMatrix</c> (<see cref="ScreenRing"/>), never from its field of
    /// view, so the Pistol's off-axis seat lens (<c>Matrix4x4.Frustum</c>), the chase camera and the Scene view all
    /// draw it where the ring is.</para>
    ///
    /// <para><b>The knobs</b> live on the material, <c>Resources/VortexRingLens.mat</c> (bend, rim, chromatic split,
    /// swirl); per ring the caller passes only its strength (0..1, e.g. <see cref="LifeFade"/>) and the rim's tint.</para>
    /// </summary>
    public static class VortexRingLens
    {
        /// <summary>The lens material, under any Resources folder.</summary>
        public const string MaterialResourcePath = "VortexRingLens";

        /// <summary>The bank the shader reads per camera: the nearest rings first (<c>VORTEX_RING_LENS_MAX_RINGS</c>).</summary>
        public const int MaxRings = 8;

        /// <summary>A ring not <see cref="Set"/> for more than this many frames is dropped.</summary>
        public const int StaleFrames = 2;

        /// <summary>The bend's width σ as a share of the tube's radius: the rim (at u = 1/0.6) sits on the tube's edge.</summary>
        public const float SigmaPerTube = 0.6f;

        /// <summary>The bend reaches this many σ from the core (<c>VORTEX_RING_LENS_REACH</c>).</summary>
        public const float Reach = 3f;

        /// <summary>Seconds to fade in after the ring is born, and out before it dies (<see cref="LifeFade"/>).</summary>
        public const float DefaultFadeIn = 0.25f, DefaultFadeOut = 0.5f;

        /// <summary>The rim's tint when the caller has no domain colour: the lab's ring colour, 0x9fe8ff.</summary>
        public static readonly Color DefaultTint = new(0x9f / 255f, 0xe8 / 255f, 1f, 1f);

        struct Ring
        {
            public object Key;
            public Vector3 Centre, Axis;
            public float Radius, Tube, Strength, Spin;
            public Color Tint;
            public int Frame;
        }

        struct Drawn
        {
            public Vector4 C, A, Z, L, T;
            public float Depth;
        }

        static readonly List<Ring> s_rings = new();
        static readonly Drawn[] s_drawn = new Drawn[MaxRings];
        static bool s_holdingCameras;
        static Material s_material;
        static bool s_materialResolved;

        static readonly ProfilerMarker s_screenMarker = new("VortexRingLens.ScreenRings");
        static readonly ProfilerMarker s_maintainMarker = new("VortexRingLens.Maintain");

        /// <summary>Rings live now (every one the pass may draw).</summary>
        public static int Count => s_rings.Count;

        /// <summary>
        /// Place or move the ring named <paramref name="key"/> this frame. World units throughout:
        /// <paramref name="radius"/> is the ring's radius (centre to core), <paramref name="tube"/> the tube's radius
        /// (the Pistol's <c>pVortexR × VortexRing.Tube</c>), <paramref name="strength01"/> how much it bends now
        /// (0 draws nothing; <see cref="LifeFade"/> is the usual value), <paramref name="tint"/> the rim's colour and
        /// <paramref name="spin"/> how fast the swirl ripples (the ring's own spin, radians per second; 0 holds it still).
        /// </summary>
        public static void Set(object key, Vector3 centre, Vector3 axis, float radius, float tube, float strength01,
            Color tint, float spin = 0f)
        {
            if (key == null) return;
            var ring = new Ring
            {
                Key = key, Centre = centre, Axis = axis, Radius = radius, Tube = tube,
                Strength = Mathf.Clamp01(strength01), Spin = spin, Tint = tint, Frame = Time.frameCount,
            };
            int i = IndexOf(key);
            if (i >= 0) { s_rings[i] = ring; return; }
            s_rings.Add(ring);
            if (!s_holdingCameras)
            {
                s_holdingCameras = true;
                BlackHoleLens.CameraSupport.Acquire();
            }
        }

        /// <summary>Stop drawing the ring named <paramref name="key"/>. False when it was not live.</summary>
        public static bool Remove(object key)
        {
            int i = key == null ? -1 : IndexOf(key);
            if (i < 0) return false;
            s_rings.RemoveAt(i);
            ReleaseIfEmpty();
            return true;
        }

        /// <summary>Drop every ring.</summary>
        public static void Clear()
        {
            s_rings.Clear();
            ReleaseIfEmpty();
        }

        /// <summary>
        /// The usual strength for a ring <paramref name="age"/> seconds old with <paramref name="remaining"/> seconds
        /// to live: it swells in over <paramref name="fadeIn"/> and fades out over the last <paramref name="fadeOut"/>,
        /// smoothly at both ends, so a ring never pops in or out.
        /// </summary>
        public static float LifeFade(float age, float remaining, float fadeIn = DefaultFadeIn, float fadeOut = DefaultFadeOut)
        {
            float a = fadeIn > 0f ? Smooth01(age / fadeIn) : (age >= 0f ? 1f : 0f);
            float b = fadeOut > 0f ? Smooth01(remaining / fadeOut) : (remaining > 0f ? 1f : 0f);
            return a * b;
        }

        static float Smooth01(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        static int IndexOf(object key)
        {
            for (int i = 0; i < s_rings.Count; i++)
                if (Equals(s_rings[i].Key, key)) return i;
            return -1;
        }

        static void ReleaseIfEmpty()
        {
            if (s_rings.Count > 0 || !s_holdingCameras) return;
            s_holdingCameras = false;
            BlackHoleLens.CameraSupport.Release();
        }

        /// <summary>
        /// The shared lens material, or null when it cannot be drawn (no ring then bends light — never Unity's magenta
        /// error shader). Warns once.
        /// </summary>
        public static Material SharedMaterial
        {
            get
            {
                if (!s_materialResolved)
                {
                    s_material = Resources.Load<Material>(MaterialResourcePath);
                    string reason = null;
                    if (s_material == null) reason = $"Resources/{MaterialResourcePath}.mat is missing";
                    else BlackHoleLens.IsDrawable(s_material, out reason);
                    if (reason != null)
                    {
                        CSDebug.LogWarning($"[VortexRingLens] The lens cannot draw: {reason}. Vortex rings bend no light.");
                        s_material = null;
                    }
                    s_materialResolved = true;
                }
                return s_material;
            }
        }

        /// <summary>
        /// One ring on one camera's screen, from the camera's REAL matrices — any perspective projection, the off-axis
        /// <c>Matrix4x4.Frustum</c> included. Screen-height units, the lens's frame: p = (uv − 0.5)·(aspect, 1), +y up.
        ///
        /// <para>A circle through a perspective camera is an exact ellipse while the whole ring is in front of the
        /// camera. With the ring's points <c>c + R(cos θ e1 + sin θ e2)</c>, the clip position is linear in (cos θ,
        /// sin θ), so the screen point is <c>(X0 + A cos θ + B sin θ) / (w0 + wc cos θ + ws sin θ)</c>. Rotating θ so
        /// the denominator is <c>w0(1 + λ cos θ')</c> (λ = |(wc, ws)| / w0 &lt; 1) and substituting the aberration map
        /// <c>cos θ' = (cos ψ − λ)/(1 − λ cos ψ)</c> makes the point LINEAR in (cos ψ, sin ψ): centre + U cos ψ + V sin ψ.
        /// Its principal axes are the ellipse's. Nothing is inverted, so a ring seen edge-on (b → 0) stays exact.</para>
        ///
        /// <para>Out: <paramref name="rc"/> the centre and semi-axes a ≥ b; <paramref name="ra"/> the major direction,
        /// the cull reach (3σ at the nearest point) and 1; <paramref name="rz"/> the core's inverse depth
        /// <c>x + y·cos τ + z·sin τ</c> at the ellipse angle τ (exact: 1/w = (1 − λ cos ψ)/(w0(1 − λ²))) and σ per unit of
        /// inverse depth; <paramref name="depth"/> the centre's clip w (the sort key); <paramref name="nearFade"/> 1, or
        /// less as the ring's nearest point closes on the near plane. False when any of the ring is within the near plane,
        /// it is degenerate, or it is wholly off the screen.</para>
        /// </summary>
        public static bool ScreenRing(Matrix4x4 projection, Matrix4x4 worldToCamera, float aspect, float nearClip,
            Vector3 centre, Vector3 axis, float radius, float tube,
            out Vector4 rc, out Vector4 ra, out Vector4 rz, out float depth, out float nearFade)
        {
            rc = ra = rz = default;
            depth = 0f;
            nearFade = 0f;
            if (!(radius > 0f) || !(tube > 0f) || !(aspect > 0f)) return false;
            Vector3 n = axis.sqrMagnitude > 1e-12f ? axis.normalized : Vector3.forward;
            Vector3 e1 = Vector3.Cross(n, Mathf.Abs(n.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 e2 = Vector3.Cross(n, e1);
            Matrix4x4 vp = projection * worldToCamera;

            Clip(vp, centre.x, centre.y, centre.z, 1.0, out double hx, out double hy, out double w0);
            Clip(vp, radius * e1.x, radius * e1.y, radius * e1.z, 0.0, out double ax, out double ay, out double wc);
            Clip(vp, radius * e2.x, radius * e2.y, radius * e2.z, 0.0, out double bx, out double by, out double ws);
            double sx = aspect * 0.5, sy = 0.5;
            hx *= sx; hy *= sy; ax *= sx; ay *= sy; bx *= sx; by *= sy;

            double rho = System.Math.Sqrt(wc * wc + ws * ws);
            double wMin = w0 - rho;
            if (!(wMin > nearClip)) return false;
            double band = 1.0 + 0.25 * radius;
            nearFade = (float)System.Math.Min(1.0, (wMin - nearClip) / band);

            // θ' = θ − φ puts the denominator in the form w0 + ρ cos θ'.
            double phi = System.Math.Atan2(ws, wc), cp = System.Math.Cos(phi), sp = System.Math.Sin(phi);
            double arx = ax * cp + bx * sp, ary = ay * cp + by * sp;
            double brx = bx * cp - ax * sp, bry = by * cp - ay * sp;
            double lam = rho / w0, d = 1.0 - lam * lam, sd = System.Math.Sqrt(d), k0 = 1.0 / (w0 * d);
            double cx = (hx - lam * arx) * k0, cy = (hy - lam * ary) * k0;
            double ux = (arx - lam * hx) * k0, uy = (ary - lam * hy) * k0;
            double vx = brx / (w0 * sd), vy = bry / (w0 * sd);

            // The principal axes of U cos ψ + V sin ψ.
            double t0 = 0.5 * System.Math.Atan2(2.0 * (ux * vx + uy * vy), ux * ux + uy * uy - vx * vx - vy * vy);
            double c0 = System.Math.Cos(t0), s0 = System.Math.Sin(t0);
            double px = ux * c0 + vx * s0, py = uy * c0 + vy * s0;
            double qx = -ux * s0 + vx * c0, qy = -uy * s0 + vy * c0;
            double a = System.Math.Sqrt(px * px + py * py), b = System.Math.Sqrt(qx * qx + qy * qy);
            if (b > a)
            {
                t0 += 0.5 * System.Math.PI;
                c0 = System.Math.Cos(t0); s0 = System.Math.Sin(t0);
                (px, py, qx, qy) = (qx, qy, -px, -py);
                (a, b) = (b, a);
            }
            if (!(a > 1e-9)) return false;
            double ex = px / a, ey = py / a, fx = -ey, fy = ex;
            double sf = qx * fx + qy * fy >= 0.0 ? 1.0 : -1.0;

            double sigmaPerInvZ = SigmaPerTube * tube * projection.m11 * 0.5;
            double reach = Reach * sigmaPerInvZ / wMin;
            if (!(sigmaPerInvZ > 0.0)) return false;

            // Wholly off the screen (the ellipse's bounding box, grown by the reach): nothing to draw.
            double halfX = System.Math.Sqrt(a * a * ex * ex + b * b * fx * fx) + reach;
            double halfY = System.Math.Sqrt(a * a * ey * ey + b * b * fy * fy) + reach;
            if (System.Math.Abs(cx) - halfX > 0.5 * aspect || System.Math.Abs(cy) - halfY > 0.5) return false;

            rc = new Vector4((float)cx, (float)cy, (float)a, (float)b);
            ra = new Vector4((float)ex, (float)ey, (float)reach, 1f);
            rz = new Vector4((float)k0, (float)(-lam * k0 * c0), (float)(lam * k0 * s0 * sf), (float)sigmaPerInvZ);
            depth = (float)w0;
            return true;
        }

        static void Clip(in Matrix4x4 m, double x, double y, double z, double w, out double cx, out double cy, out double cw)
        {
            cx = m.m00 * x + m.m01 * y + m.m02 * z + m.m03 * w;
            cy = m.m10 * x + m.m11 * y + m.m12 * z + m.m13 * w;
            cw = m.m30 * x + m.m31 * y + m.m32 * z + m.m33 * w;
        }

        /// <summary>The foreground margin (eye units): opaque mass nearer than the core by more than this is not bent.</summary>
        public static float ForegroundMargin(float tube) => SigmaPerTube * Reach * Mathf.Max(0f, tube);

        /// <summary>The aspect the lens's frame uses for <paramref name="cam"/>: its pixel rect's, which the shader's uv spans.</summary>
        public static float ScreenAspect(Camera cam) =>
            cam.pixelHeight > 0 ? (float)cam.pixelWidth / cam.pixelHeight : Mathf.Max(0.01f, cam.aspect);

        /// <summary>
        /// Every live ring <paramref name="cam"/> sees, the nearest <see cref="MaxRings"/> first, as the shader's rows
        /// (layout in <c>VortexRingLens.hlsl</c>). Rings with no strength, behind the camera or off its screen are left
        /// out; the count is what the pass draws (0 skips the pass, its copy included).
        /// </summary>
        internal static int ScreenRings(Camera cam, Vector4[] c, Vector4[] a, Vector4[] z, Vector4[] l, Vector4[] t)
        {
            using var _ = s_screenMarker.Auto();
            float aspect = ScreenAspect(cam);
            var projection = cam.projectionMatrix;
            var view = cam.worldToCameraMatrix;
            bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            int count = 0;
            for (int i = 0; i < s_rings.Count; i++)
            {
                var ring = s_rings[i];
                if (ring.Strength <= 0f) continue;
                if (!ScreenRing(projection, view, aspect, cam.nearClipPlane, ring.Centre, ring.Axis, ring.Radius, ring.Tube,
                        out var rc, out var ra, out var rz, out float depth, out float nearFade)) continue;
                float s = ring.Strength * nearFade;
                if (s <= 0f) continue;
                var tint = linear ? ring.Tint.linear : ring.Tint;
                var drawn = new Drawn
                {
                    C = rc, A = ra, Z = rz, L = new Vector4(s, s, 0f, ring.Spin),
                    T = new Vector4(tint.r, tint.g, tint.b, ForegroundMargin(ring.Tube)), Depth = depth,
                };
                // Keep the nearest MaxRings, nearest first (insertion into a short sorted bank).
                int at = count < MaxRings ? count : MaxRings;
                while (at > 0 && s_drawn[at - 1].Depth > depth) at--;
                if (at >= MaxRings) continue;
                int last = Mathf.Min(count, MaxRings - 1);
                for (int k = last; k > at; k--) s_drawn[k] = s_drawn[k - 1];
                s_drawn[at] = drawn;
                if (count < MaxRings) count++;
            }
            for (int i = 0; i < MaxRings; i++)
            {
                if (i < count)
                {
                    c[i] = s_drawn[i].C; a[i] = s_drawn[i].A; z[i] = s_drawn[i].Z; l[i] = s_drawn[i].L; t[i] = s_drawn[i].T;
                }
                else c[i] = a[i] = z[i] = l[i] = t[i] = Vector4.zero;
            }
            return count;
        }

        /// <summary>Once a frame before any camera renders: drop stale rings, keep every game camera's depth on.</summary>
        static void OnBeginContextRendering(ScriptableRenderContext context, List<Camera> cameras)
        {
            if (s_rings.Count == 0) return;
            using var _ = s_maintainMarker.Auto();
            if (!Application.isPlaying) { Clear(); return; }
            int frame = Time.frameCount;
            for (int i = s_rings.Count - 1; i >= 0; i--)
                if (frame - s_rings[i].Frame > StaleFrames) s_rings.RemoveAt(i);
            ReleaseIfEmpty();
            if (s_rings.Count > 0) BlackHoleLens.CameraSupport.Maintain();
        }

        /// <summary>Play-mode (re)entry: no ring is live yet; subscribe once.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ResetOnLoad()
        {
            s_rings.Clear();
            s_holdingCameras = false;
            s_materialResolved = false;
            s_material = null;
            RenderPipelineManager.beginContextRendering -= OnBeginContextRendering;
            RenderPipelineManager.beginContextRendering += OnBeginContextRendering;
        }
    }
}
