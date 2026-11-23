using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The DRAWN half of the Stoat's annihilation ripple (<c>R_VesselActions/STOAT_DIPOLE.md</c> §1c "The drawn wave"):
    /// a space-time ripple with no colour of its own. From the point where the hole went back to nothing a spherical
    /// wave runs out; its front and the few crests trailing it BEND the scene behind them (and brighten and darken it a
    /// little, crest and trough), weaker as it spreads (1/r) and fading as it settles. Presentation only; what the front
    /// DOES is <see cref="StoatRippleExecutor"/>'s, on the same clock (<see cref="StoatRippleMath.Front"/>).
    ///
    /// <para><b>How it draws.</b> Each live ripple is a record here (no GameObject); <see cref="StoatRipplePass"/> copies
    /// the camera colour after the transparents and redraws the pixels the waves cross from that copy, bent, in one
    /// full-screen triangle (<c>CosmicShore/StoatRipple</c>). Both black-hole looks draw it the same way; while no ripple
    /// lives the pass is not enqueued and costs nothing. Yash, 2026-11-21: "a near-realistic space-time ripple wave, no
    /// domain color" - it replaced the Mobile look's domain-colour ring and the lens sphere no renderer ever drew.</para>
    /// </summary>
    public static class StoatRipple
    {
        /// <summary><c>Resources/StoatRipple.mat</c> (shader <c>CosmicShore/StoatRipple</c>).</summary>
        public const string MaterialResourcePath = "StoatRipple";

        /// <summary>The shader draws this many ripples at most, the nearest first. Mirrors <c>STOAT_RIPPLE_MAX</c>.</summary>
        public const int MaxDrawn = 4;

        public struct Ripple
        {
            public Vector3 Centre;
            public float T0;
            public float Radius;
            public float ExpandSeconds;
            public float FadeSeconds;
            public float Wavelength;
            public float Bend;
            public float NearRadius;
            public float Shimmer;
            public float PassShare;
        }

        static readonly List<Ripple> s_live = new();
        static readonly int[] s_order = new int[16];
        static readonly float[] s_depth = new float[16];

        /// <summary>The ripples' clock: game time, so a slowed or paused game slows them with the front that strips.</summary>
        public static float Now => Time.time;

        /// <summary>Ripples still drawing (pruned on every spawn and every frame the pass asks).</summary>
        public static int Count
        {
            get { Prune(Now); return s_live.Count; }
        }

        /// <summary>A ripple at <paramref name="at"/> whose front runs out to <paramref name="radius"/>.</summary>
        public static void Spawn(Vector3 at, float radius, StoatRippleConfigSO config)
        {
            if (config == null) return;
            float now = Now;
            Prune(now);
            if (s_live.Count >= s_order.Length) s_live.RemoveAt(0);   // a burst: the oldest goes first
            s_live.Add(new Ripple
            {
                Centre = at, T0 = now, Radius = Mathf.Max(0.1f, radius),
                ExpandSeconds = config.ExpandSeconds, FadeSeconds = config.FadeSeconds, Wavelength = config.Wavelength,
                Bend = config.Bend, NearRadius = config.NearRadius, Shimmer = config.Shimmer, PassShare = config.PassShare,
            });
        }

        static void Prune(float now)
        {
            for (int i = s_live.Count - 1; i >= 0; i--)
            {
                var r = s_live[i];
                if (StoatRippleMath.Done(now - r.T0, r.ExpandSeconds, r.FadeSeconds)) s_live.RemoveAt(i);
            }
        }

        /// <summary>
        /// This camera's ripples as the shader takes them, nearest first, in the lens's screen units (screen heights from
        /// the centre, y up; <see cref="BlackHoleLens.ScreenWell"/>): <paramref name="c"/> = (centre x, centre y, the
        /// front's screen radius, the wavelength on screen), <paramref name="a"/> = (the bend on screen, the shimmer, 1, 0).
        /// <paramref name="pass"/> is the wave going THROUGH the camera: the whole view swells and settles by that much.
        /// Returns how many were written.
        /// </summary>
        public static int ScreenRipples(Matrix4x4 worldToCamera, Vector3 eye, float fieldOfViewY, float nearClip, float now,
            Vector4[] c, Vector4[] a, out float pass)
        {
            pass = 0f;
            Prune(now);
            int candidates = 0;
            for (int i = 0; i < s_live.Count && candidates < s_order.Length; i++)
            {
                s_order[candidates] = i;
                s_depth[candidates] = (s_live[i].Centre - eye).sqrMagnitude;
                candidates++;
            }
            for (int i = 1; i < candidates; i++)
            {
                int k = s_order[i];
                float dk = s_depth[k];
                int j = i - 1;
                while (j >= 0 && s_depth[s_order[j]] > dk) { s_order[j + 1] = s_order[j]; j--; }
                s_order[j + 1] = k;
            }

            float f = 0.5f / Mathf.Tan(fieldOfViewY * Mathf.Deg2Rad * 0.5f);
            int count = 0;
            for (int n = 0; n < candidates; n++)
            {
                var r = s_live[s_order[n]];
                float age = now - r.T0;
                float front = StoatRippleMath.Front(age, r.ExpandSeconds, r.Radius);
                float bend = StoatRippleMath.Amplitude(r.Bend, front, r.NearRadius, StoatRippleMath.Strength(age, r.ExpandSeconds, r.FadeSeconds));
                if (bend <= 0f || front <= 0f) continue;
                float distance = Vector3.Distance(eye, r.Centre);
                float wavelength = Mathf.Max(0.1f, r.Wavelength);

                // The wave reaching and crossing the camera: the view swells and settles (u > 0 once the front is past).
                pass += r.PassShare * f * bend * StoatRippleMath.Profile((front - distance) / wavelength);

                if (count >= MaxDrawn || count >= c.Length) continue;
                if (front >= distance) continue;                       // the camera is inside: no ring left to see
                var view = worldToCamera.MultiplyPoint3x4(r.Centre);
                float depth = -view.z;
                if (depth <= nearClip) continue;                       // behind the camera
                float rho = BlackHoleLens.Angular(f, front, distance);
                float lambda = Mathf.Max(f * wavelength / Mathf.Max(1f, distance),
                    BlackHoleLens.Angular(f, Mathf.Min(front + wavelength, distance * 0.999f), distance) - rho);
                c[count] = new Vector4(f * view.x / depth, f * view.y / depth, rho, Mathf.Max(1e-3f, lambda));
                a[count] = new Vector4(f * bend, r.Shimmer * bend / Mathf.Max(1e-4f, r.Bend), 1f, 0f);
                count++;
            }
            for (int i = count; i < c.Length; i++) c[i] = a[i] = Vector4.zero;
            return count;
        }

        /// <summary>Play-mode (re)entry: the ripples describe a scene that is gone.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_live.Clear();
    }
}
