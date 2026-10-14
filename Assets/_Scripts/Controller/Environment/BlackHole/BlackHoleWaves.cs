using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Waves in space-time (Docs/BLACK_HOLE.md §5.2): the Vessel Studio's, ported line for line
    /// (Docs/Studios/StoatFlightStudio.html: <c>spawnWave</c>, <c>waveAlive</c>, <c>gwStrainAt</c>, <c>setWaveUniforms</c>).
    /// <list type="bullet">
    /// <item><b>In</b> — the white hole's light converging from <c>whInFrom</c> onto its spot over its birth (the departure reversed).</item>
    /// <item><b>Out</b> — the white hole's light leaving to infinity at <c>whOutSpeed</c>, fading over <c>whOutFade</c> (its death).</item>
    /// <item><b>Gravitational</b> — the burst a pair's mass leaves as when it annihilates: a front moving out at <c>gwSpeed</c>,
    /// a ringdown (<c>gwPeriod</c>, <c>gwRingdown</c>) behind it, weaker as 1/r, strongest sideways to the line the holes met
    /// along (sin² of the angle) and silent along it.</item>
    /// </list>
    /// The lens draws all three (<c>BlackHoleLens.shader</c>: the bend, the crests, the wash of a shell passing you, the view
    /// stretching as the wave passes you); <c>PrismGravityWarp.hlsl</c> stretches and squeezes the prisms the wave crosses
    /// (<c>gwPrism</c>). Photons only: nothing here moves mass. Local per peer, like every hole's visuals.
    /// </summary>
    public static class BlackHoleWaves
    {
        public enum Kind { In = 1, Out = 2, Gravitational = 3 }

        public struct Wave
        {
            public Kind Kind;
            public Vector3 Centre;
            /// <summary>The line the holes met along (unit), or zero: no sin² pattern.</summary>
            public Vector3 Axis;
            public float Mass;
            public float T0;
            /// <summary>An In shell's duration (the white hole's birth, the studio's <c>whInS</c>).</summary>
            public float InSeconds;
        }

        /// <summary>Inside this the 1/r falloff is held flat (the studio's <c>GW_R0</c>).</summary>
        public const float FlatRadius = 60f;

        /// <summary>The lens draws this many waves at most, the newest first (the studio's 4).</summary>
        public const int MaxDrawn = 4;

        static readonly List<Wave> s_waves = new();

        public static int Count => s_waves.Count;
        public static IReadOnlyList<Wave> Live => s_waves;

        /// <summary>The waves' clock: game time, so a slowed or paused game slows them as the studio's sim clock does.</summary>
        public static float Now => Time.time;

        public static void Spawn(Kind kind, Vector3 centre, Vector3 axis = default, float mass = 1f, float inSeconds = 1f)
        {
            if (s_waves.Count == 0) BlackHoleLensPass.AcquireWaves();
            s_waves.Add(new Wave
            {
                Kind = kind, Centre = centre, Axis = axis.sqrMagnitude > 1e-12f ? axis.normalized : Vector3.zero,
                Mass = mass, T0 = Now, InSeconds = Mathf.Max(0.01f, inSeconds),
            });
        }

        /// <summary>Drop the waves that are over (the studio's <c>waveAlive</c>). Called once a frame by the registry.</summary>
        public static void Tick(BlackHoleConfigSO config)
        {
            if (s_waves.Count == 0) return;
            float now = Now;
            for (int i = s_waves.Count - 1; i >= 0; i--)
                if (!Alive(s_waves[i], now - s_waves[i].T0, config)) s_waves.RemoveAt(i);
            if (s_waves.Count == 0) BlackHoleLensPass.ReleaseWaves();
        }

        public static bool Alive(in Wave w, float u, BlackHoleConfigSO config) => w.Kind switch
        {
            Kind.In => u < w.InSeconds,
            Kind.Out => u < 6f * config.WhOutFade && config.WhOutSpeed * u < 6000f,
            _ => u < 4000f / Mathf.Max(1f, config.GwSpeed) + 8f * config.GwRingdown,
        };

        /// <summary>The studio's <c>gwStrainAt</c>: the dimensionless strain at a point (±1 near the source).</summary>
        public static float Strain(Vector3 at, in Wave w, float now, BlackHoleConfigSO config)
        {
            Vector3 d = at - w.Centre;
            float r = d.magnitude + 1e-6f;
            float u = now - w.T0 - r / Mathf.Max(1f, config.GwSpeed), tau = Mathf.Max(0.01f, config.GwRingdown);
            if (u <= 0f || u > 8f * tau) return 0f;
            float ca = Vector3.Dot(d, w.Axis) / r;
            return w.Mass * Mathf.Exp(-u / tau) * Mathf.Sin(2f * Mathf.PI * u / Mathf.Max(0.02f, config.GwPeriod))
                   * Mathf.Min(1f, FlatRadius / r) * (1f - ca * ca);
        }

        static float Smooth(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        /// <summary>A shell's radius and brightness envelope at age <paramref name="u"/>.</summary>
        public static void Shell(in Wave w, float u, BlackHoleConfigSO config, out float radius, out float envelope)
        {
            if (w.Kind == Kind.In)
            {
                float s = u / Mathf.Max(0.01f, w.InSeconds);
                radius = config.WhInFrom * Mathf.Max(0f, 1f - s);
                envelope = Smooth(0f, 1f, s) * (1f - Smooth(0.85f, 1f, s));
            }
            else
            {
                radius = config.WhOutSpeed * u;
                envelope = Mathf.Exp(-u / Mathf.Max(0.05f, config.WhOutFade));
            }
        }

        /// <summary>
        /// The studio's <c>setWaveUniforms</c> for one camera: the newest <see cref="MaxDrawn"/> waves in front of it on screen
        /// (c: centre, kind 1 shell / 2 gravitational, live; p, q, ax: as the shader reads them), the view's own stretch
        /// (<paramref name="gwCam"/>: the strain AT the camera) and the wash of a shell passing it. Screen units as the lens's
        /// wells (<see cref="BlackHoleLens.ScreenWell"/>): screen heights from the centre, f = 0.5 / tan(fov_y / 2).
        /// </summary>
        public static int ScreenWaves(Matrix4x4 worldToCamera, Vector3 eye, float fieldOfViewY, float nearClip, float now,
            BlackHoleConfigSO config, Vector4[] c, Vector4[] p, Vector4[] q, Vector4[] ax, out float gwCam, out float shellWash)
        {
            for (int i = 0; i < c.Length; i++) c[i] = p[i] = q[i] = ax[i] = Vector4.zero;
            float f = 0.5f / Mathf.Tan(fieldOfViewY * Mathf.Deg2Rad * 0.5f);
            int slot = 0;
            float hc = 0f, wash = 0f;
            for (int n = s_waves.Count - 1; n >= 0 && slot < MaxDrawn && slot < c.Length; n--)
            {
                var w = s_waves[n];
                float u = now - w.T0;
                if (w.Kind == Kind.Gravitational) hc += Strain(eye, w, now, config);
                else
                {
                    // a light shell sweeping across the camera: a wash of its light as it passes you
                    Shell(w, u, config, out float shellR, out float shellEnv);
                    float x = (shellR - Vector3.Distance(eye, w.Centre)) / 45f;
                    wash += 0.22f * shellEnv * Mathf.Exp(-x * x) * Mathf.Min(1f, config.ShellBend / 6f);
                }
                var view = worldToCamera.MultiplyPoint3x4(w.Centre);
                float vz = -view.z;
                if (vz <= nearClip + 1f) continue;
                float sx = f * view.x / vz, sy = f * view.y / vz, k = f / vz;
                int i = slot++;
                if (w.Kind == Kind.Gravitational)
                {
                    // the sin² pattern needs the meeting axis on screen
                    float axx = 0f, axy = 0f, has = 0f;
                    if (w.Axis != Vector3.zero)
                    {
                        var tip = worldToCamera.MultiplyPoint3x4(w.Centre + w.Axis * 50f);
                        float tz = -tip.z;
                        // projected as the studio does (three.js Vector3.project), a point behind the camera included
                        if (Mathf.Abs(tz) > 1e-6f)
                        {
                            axx = 2f * (f * tip.x / tz - sx);
                            axy = 2f * (f * tip.y / tz - sy);
                            float l = Mathf.Sqrt(axx * axx + axy * axy);
                            if (l > 1e-4f) { axx /= l; axy /= l; has = 1f; }
                            else { axx = 0f; axy = 0f; }
                        }
                    }
                    c[i] = new Vector4(sx, sy, 2f, 1f);
                    p[i] = new Vector4(k, axx, config.GwAmp * w.Mass * k, u);
                    q[i] = new Vector4(Mathf.Max(1f, config.GwSpeed), Mathf.Max(0.02f, config.GwPeriod),
                        Mathf.Max(0.01f, config.GwRingdown), has);
                    ax[i] = new Vector4(axx, axy, 0f, 0f);
                }
                else
                {
                    Shell(w, u, config, out float shellR, out float shellEnv);
                    c[i] = new Vector4(sx, sy, 1f, 1f);
                    p[i] = new Vector4(shellR * k, config.ShellBend * shellEnv * k, 28f * k, 0.35f * shellEnv);
                }
            }
            gwCam = 0.05f * config.GwShake * config.GwAmp / 12f * hc;
            shellWash = Mathf.Min(0.3f, wash * 0.6f);
            return slot;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ResetOnLoad() => s_waves.Clear();
    }
}
