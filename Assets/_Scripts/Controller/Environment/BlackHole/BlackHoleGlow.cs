using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.Rendering;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The MOBILE look of holes and their waves (Docs/BLACK_HOLE.md §5.4, <see cref="HoleLook.Mobile"/>, the default): the
    /// Meerkat water tube's recipe (<c>MeerkatWaterTube.shader</c>, <c>MeerkatWaterTubeView</c>) applied to the black / white
    /// hole pair. No space warping: nothing behind a hole is bent, no prism or hull is stretched, the view never stretches,
    /// no full-screen wash, no screen copy, no depth texture. Each hole, wave and ripple is ONE small camera-facing annulus
    /// drawn with <see cref="Graphics.RenderMesh"/> in the forward transparent queue (<c>BlackHoleGlow.shader</c>):
    /// <list type="bullet">
    /// <item>a black hole: a dark core (its shadow) with a thin warm photon ring and a faint halo;</item>
    /// <item>a white hole: a bright soft core;</item>
    /// <item>a gravitational wave: its ringdown as faint concentric ripples (crests glow, troughs shade), local — faded out
    /// by <see cref="BlackHoleConfigSO.MobileWaveReach"/>;</item>
    /// <item>a white hole's light shell and the Stoat's charge ripple (<see cref="StoatRipple"/>): a thin soft light ring.</item>
    /// </list>
    /// Photons only, like the lens: nothing here is read by physics. Drawn every frame by the registry's driver
    /// (<see cref="BlackHoleRegistry"/>) and by each live <see cref="StoatRipple"/>.
    /// </summary>
    public static class BlackHoleGlow
    {
        /// <summary><c>Resources/BlackHoleGlow.mat</c> (shader <c>CosmicShore/BlackHoleGlow</c>).</summary>
        public const string MaterialResourcePath = "BlackHoleGlow";

        /// <summary>Segments round the annulus: 2 × this triangles per draw.</summary>
        public const int Segments = 48;

        public const float ModeBlackHole = 0f, ModeWhiteHole = 1f, ModeRing = 2f, ModeRipple = 3f;

        /// <summary>The studio's photon-ring colour (BlackHoleLens.hlsl: float3(1, 0.8, 0.55)).</summary>
        public static readonly Color RingColour = new(1f, 0.8f, 0.55f, 1f);
        public static readonly Color WhiteCoreColour = new(1f, 0.98f, 0.94f, 1f);
        public static readonly Color GravityWaveColour = new(0.78f, 0.88f, 1f, 1f);
        public static readonly Color ShellColour = new(1f, 0.96f, 0.86f, 1f);

        /// <summary>A shell's light ring half-width (u): the studio's shell band (28 u wide).</summary>
        public const float ShellHalfWidth = 14f;

        /// <summary>Most waves drawn at once, the newest first (the lens's own cap).</summary>
        public const int MaxWavesDrawn = BlackHoleWaves.MaxDrawn;

        static readonly int CentreId = Shader.PropertyToID("_Centre");
        static readonly int RadiiId = Shader.PropertyToID("_Radii");
        static readonly int ShapeId = Shader.PropertyToID("_Shape");
        static readonly int WaveId = Shader.PropertyToID("_Wave");
        static readonly int AxisId = Shader.PropertyToID("_Axis");
        static readonly int ColourId = Shader.PropertyToID("_Colour");
        static readonly int FadeId = Shader.PropertyToID("_Fade");
        static readonly int ModeId = Shader.PropertyToID("_Mode");

        static Material s_material;
        static bool s_resolved;
        static Mesh s_mesh;
        static readonly List<MaterialPropertyBlock> s_blocks = new();
        static int s_used, s_frame = -1;

        /// <summary>
        /// The shared material, or null (one warning) when it is missing or its shader cannot run here — the holes then
        /// draw as plain spheres (<see cref="BlackHole"/>'s fallback), never invisible and never magenta.
        /// </summary>
        public static Material SharedMaterial
        {
            get
            {
                if (s_resolved) return s_material;
                s_resolved = true;
                var material = Resources.Load<Material>(MaterialResourcePath);
                string reason = material == null ? $"Resources/{MaterialResourcePath}.mat is missing"
                    : material.shader == null || !material.shader.isSupported ? "its shader is not supported on this device" : null;
                if (reason != null)
                    CSDebug.LogWarning($"[BlackHole] The mobile glow cannot draw: {reason}. Holes draw as plain spheres.");
                else s_material = material;
                return s_material;
            }
        }

        /// <summary>True when holes should draw with this (the Mobile look, the drawn look on, the material usable).</summary>
        public static bool Active(BlackHoleConfigSO config) =>
            config != null && !config.SpaceWarp && config.LensEnabled && SharedMaterial != null;

        /// <summary>The flat annulus: positionOS = (cos θ, sin θ, t), t 0 = inner edge, 1 = outer edge. Built once.</summary>
        public static Mesh Annulus()
        {
            if (s_mesh != null) return s_mesh;
            var v = new Vector3[2 * Segments];
            var tris = new int[6 * Segments];
            for (int i = 0; i < Segments; i++)
            {
                float a = i * (2f * Mathf.PI / Segments);
                float c = Mathf.Cos(a), s = Mathf.Sin(a);
                v[2 * i] = new Vector3(c, s, 0f);
                v[2 * i + 1] = new Vector3(c, s, 1f);
                int j = (i + 1) % Segments, t = 6 * i;
                tris[t] = 2 * i; tris[t + 1] = 2 * i + 1; tris[t + 2] = 2 * j + 1;
                tris[t + 3] = 2 * i; tris[t + 4] = 2 * j + 1; tris[t + 5] = 2 * j;
            }
            s_mesh = new Mesh { name = "BlackHoleGlowAnnulus", vertices = v, triangles = tris, hideFlags = HideFlags.DontSave };
            // The vertex stage places every vertex itself (world space, facing the camera); culling uses the per-draw
            // world bounds handed to RenderMesh, so the mesh's own bounds only need to be non-degenerate.
            s_mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 2f);
            return s_mesh;
        }

        // ---------------- The registry's per-frame draw ----------------

        /// <summary>Every hole that still draws (live and easing out). Called once a frame by the registry's driver.</summary>
        public static void DrawHoles(IReadOnlyList<BlackHole> holes, BlackHoleConfigSO config)
        {
            if (!Active(config)) return;
            for (int i = 0; i < holes.Count; i++)
            {
                var h = holes[i];
                if (h == null || !h.isActiveAndEnabled) continue;
                if (!HoleShape(h, config, out var centre, out float outer, out var shape, out float mode, out var colour)) continue;
                Draw(centre, 0f, outer, mode, shape, Vector4.zero, Vector4.zero, colour, 1f);
            }
        }

        /// <summary>
        /// One hole's mobile look (pure: the editor tests read it). False when it draws nothing (eased out). The sizes
        /// are the studio lens's own — the shadow at <see cref="BlackHoleConfigSO.ShadowSize"/> horizons, the photon ring
        /// at its edge, the white core at <see cref="BlackHoleConfigSO.WhiteCoreSize"/> — scaled by the hole's weight and
        /// amplitude, so a pair still sinks to a point as it annihilates.
        /// </summary>
        public static bool HoleShape(BlackHole hole, BlackHoleConfigSO config, out Vector3 centre, out float outer,
            out Vector4 shape, out float mode, out Color colour)
        {
            centre = default; outer = 0f; shape = default; mode = 0f; colour = default;
            if (!BlackHoleLens.FromHole(hole, config, out var well)) return false;
            centre = well.Position;
            float tint = Mathf.Clamp01(well.TintAmount);
            if (well.Kind == BlackHoleLens.KindBlackHole)
            {
                float shadow = config.ShadowSize * well.Radius;
                mode = ModeBlackHole;
                shape = new Vector4(shadow, config.PhotonRingWidth, config.PhotonRingGlow, config.MobileHaloGlow);
                outer = shadow * (1.04f + 3f * config.PhotonRingWidth + 0.6f);
                colour = Color.Lerp(RingColour, well.Tint, tint);
            }
            else if (well.Kind == BlackHoleLens.KindWhiteHole)
            {
                float core = config.WhiteCoreSize * well.Radius;
                mode = ModeWhiteHole;
                shape = new Vector4(core, 0f, config.WhiteCoreBrightness, 0.5f);
                outer = core * 1.6f;
                colour = Color.Lerp(WhiteCoreColour, well.Tint, 0.5f * tint);
            }
            else
            {
                // a smooth well (the crystal pair, the Thresher's): no horizon to show — a faint ring at its core width
                mode = ModeRing;
                float strength = Mathf.Clamp01(well.LensStrength);
                shape = new Vector4(well.Radius, Mathf.Max(0.5f, 0.15f * well.Radius), 0.2f * strength, 0f);
                outer = well.Radius * 1.6f;
                colour = Color.Lerp(well.Kind == BlackHoleLens.KindSmoothRepulsor ? WhiteCoreColour : RingColour, well.Tint, tint);
            }
            colour.a = 1f;
            return outer > 1e-4f;
        }

        /// <summary>The live waves (BlackHoleWaves), the newest first, each as a local ring. Called once a frame by the driver.</summary>
        public static void DrawWaves(BlackHoleConfigSO config)
        {
            if (!Active(config)) return;
            var live = BlackHoleWaves.Live;
            if (live.Count == 0) return;
            float now = BlackHoleWaves.Now, reach = config.MobileWaveReach;
            int drawn = 0;
            for (int n = live.Count - 1; n >= 0 && drawn < MaxWavesDrawn; n--)
            {
                var w = live[n];
                float u = now - w.T0;
                if (w.Kind == BlackHoleWaves.Kind.Gravitational)
                {
                    if (!RippleShape(w, u, config, out float inner, out float outer, out var shape, out var wave, out var axis, out float fade))
                        continue;
                    Draw(w.Centre, inner, outer, ModeRipple, shape, wave, axis, GravityWaveColour, fade);
                }
                else
                {
                    BlackHoleWaves.Shell(w, u, config, out float radius, out float envelope);
                    float fade = ReachFade(radius, reach) * envelope;
                    if (fade <= 1e-3f) continue;
                    Draw(w.Centre, Mathf.Max(0f, radius - 2f * ShellHalfWidth), radius + 2f * ShellHalfWidth, ModeRing,
                        new Vector4(radius, ShellHalfWidth, config.MobileWaveGlow, 0f), Vector4.zero, Vector4.zero, ShellColour, fade);
                }
                drawn++;
            }
        }

        /// <summary>
        /// A gravitational wave's mobile ring at age <paramref name="u"/> (pure: the tests read it): the disc from the
        /// ringdown's tail to its front, faded out before the front reaches <see cref="BlackHoleConfigSO.MobileWaveReach"/>.
        /// The waveform is <see cref="BlackHoleWaves.Strain"/>'s, evaluated per pixel by the shader.
        /// </summary>
        public static bool RippleShape(in BlackHoleWaves.Wave w, float u, BlackHoleConfigSO config, out float inner, out float outer,
            out Vector4 shape, out Vector4 wave, out Vector4 axis, out float fade)
        {
            float speed = config.GwSpeed, tau = config.GwRingdown;
            float front = speed * u;
            outer = Mathf.Min(front, config.MobileWaveReach);
            inner = Mathf.Max(0f, speed * (u - 8f * tau));
            fade = ReachFade(front, config.MobileWaveReach);
            float glow = config.MobileWaveGlow * config.GwGlow * Mathf.Clamp(w.Mass, 0f, 1.5f);
            shape = new Vector4(0f, 0f, glow, config.MobileWaveShade * Mathf.Clamp(w.Mass, 0f, 1.5f));
            wave = new Vector4(u, speed, config.GwPeriod, tau);
            axis = w.Axis == Vector3.zero ? Vector4.zero : new Vector4(w.Axis.x, w.Axis.y, w.Axis.z, 1f);
            return fade > 1e-3f && outer > inner && glow + shape.w > 0f;
        }

        /// <summary>1 while a front is well inside the reach, easing to 0 as it arrives there: the waves stay local.</summary>
        public static float ReachFade(float front, float reach)
        {
            float t = Mathf.Clamp01((front - 0.6f * reach) / Mathf.Max(1e-3f, 0.4f * reach));
            return 1f - t * t * (3f - 2f * t);
        }

        /// <summary>A thin soft light ring (the Stoat's charge ripple): <paramref name="front"/> u out, ±<paramref name="halfWidth"/>.</summary>
        public static void DrawRing(Vector3 centre, float front, float halfWidth, Color colour, float brightness, float fade)
        {
            if (fade <= 1e-3f || brightness <= 0f || SharedMaterial == null) return;
            halfWidth = Mathf.Max(0.25f, halfWidth);
            colour.a = 1f;
            Draw(centre, Mathf.Max(0f, front - 2.5f * halfWidth), front + 2.5f * halfWidth, ModeRing,
                new Vector4(front, halfWidth, brightness, 0f), Vector4.zero, Vector4.zero, colour, fade);
        }

        static void Draw(Vector3 centre, float inner, float outer, float mode, Vector4 shape, Vector4 wave, Vector4 axis,
            Color colour, float fade)
        {
            var material = SharedMaterial;
            if (material == null || !(outer > 1e-4f)) return;
            var mpb = Next();
            mpb.SetVector(CentreId, centre);
            mpb.SetVector(RadiiId, new Vector4(inner, outer, 0f, 0f));
            mpb.SetVector(ShapeId, shape);
            mpb.SetVector(WaveId, wave);
            mpb.SetVector(AxisId, axis);
            mpb.SetColor(ColourId, colour);
            mpb.SetFloat(FadeId, fade);
            mpb.SetFloat(ModeId, mode);
            var rp = new RenderParams(material)
            {
                matProps = mpb,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
                worldBounds = new Bounds(centre, Vector3.one * (2f * outer)),
            };
            Graphics.RenderMesh(rp, Annulus(), 0, Matrix4x4.identity);
        }

        static MaterialPropertyBlock Next()
        {
            if (s_frame != Time.frameCount) { s_frame = Time.frameCount; s_used = 0; }
            if (s_used == s_blocks.Count) s_blocks.Add(new MaterialPropertyBlock());
            var mpb = s_blocks[s_used++];
            mpb.Clear();
            return mpb;
        }

        /// <summary>Play-mode (re)entry: the material cache describes a session that is gone.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnLoad()
        {
            s_resolved = false;
            s_material = null;
            s_blocks.Clear();
            s_used = 0;
            s_frame = -1;
        }
    }
}
