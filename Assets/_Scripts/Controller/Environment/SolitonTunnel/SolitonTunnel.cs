using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// One SOLITON TUNNEL, everyone's: an environmental fundamental (Yash, 2026-10-29), a bent hyperboloid tube any rider can
    /// slip-and-slide through (<see cref="SolitonTunnelRider"/>, <see cref="SolitonTunnelMath.Step"/>). A maker forms it (the
    /// Meerkat's RT, <see cref="MeerkatSolitonExecutor"/>), or a <see cref="SolitonTunnelPlacer"/> lays it in a scene with no
    /// maker. Its centreline is one conic arc (<see cref="SolitonTunnelShapeMath"/>), its wall the throat-to-mouth
    /// hyperboloid; it forms over its window, stays, collapses to its middle and fades (<see cref="Step"/>). Every tube alive
    /// is in <see cref="Live"/>: the lens (<see cref="SolitonTunnelLensPass"/>) bends space round it, the bridge
    /// (<see cref="SolitonTunnelView"/>) draws over it, tinted <see cref="Tint"/>.
    /// </summary>
    public sealed class SolitonTunnel
    {
        static readonly List<SolitonTunnel> s_live = new();

        /// <summary>Every Soliton Tunnel alive now, everyone's (made or placed).</summary>
        public static IReadOnlyList<SolitonTunnel> Live => s_live;

        public Vector3 A, T0, Side;
        public float L, S, R, B, M, Theta, FormT;   // L = S (the shaft) + R (the bend's radius) x Theta
        public SolitonTunnelShapeMath.Conic Path;   // its centreline, one conic arc round its middle
        public SolitonTunnelShapeMath.Window W;
        public bool Dying;
        public float DieT;
        /// <summary>Its maker's domain colour (white with no maker).</summary>
        public Color Colour;
        /// <summary>Its tint (rgb) and the tint's strength (a): <see cref="SolitonTunnelConfigSO.Tint"/>.</summary>
        public Color Tint;
        /// <summary>Who formed it (null: placed, no maker).</summary>
        public ISolitonTunnelMaker Maker;
        /// <summary>Its look, shape, life and slide.</summary>
        public SolitonTunnelConfigSO Config;
        public int Seq;
        public float Born;          // when it formed here
        public float SwallowLook;   // the Meerkat's swallow's next look round its way in (MeerkatSwallow)
        // [Garrett's kit hook] the kit its owner stamped on it (MeerkatSolitonKit.Kit* bits) and its exit portal's size,
        // replicated with it; Held while a sentinel guards it, or placed for good (it never starts its stay)
        public byte Kit;
        public float PortalSize;
        public bool Held;

        public Vector3 At(float s, out Vector3 tangent) => SolitonTunnelShapeMath.At(A, T0, Side, Path, s, out tangent);
        public void Frame(float s, out Vector3 normal, out Vector3 binormal) =>
            SolitonTunnelShapeMath.Frame(T0, Side, Path, s, out normal, out binormal);
        public float BendAngle(float s) => SolitonTunnelShapeMath.BendAngle(Path, s);
        /// <summary>The page's <c>whAtX</c>: the centreline at s, carried on straight before the way in and after the way out.</summary>
        public Vector3 AtExtended(float s)
        {
            if (s <= 0f) return A + T0 * s;
            var p = At(Mathf.Min(s, L), out var tangent);
            return s > L ? p + tangent * (s - L) : p;
        }
        public float Radius(float s) => SolitonTunnelShapeMath.Radius(B, M, L, W.S0, W.S1, s);
        public float Fade => SolitonTunnelShapeMath.Fade(Dying, DieT);
        /// <summary>The page's <c>standing</c>: formed, whole from mouth to mouth, not collapsing (a rider can go in).</summary>
        public bool Standing =>
            !Dying && (W.Phase == SolitonTunnelShapeMath.Phase.Open || W.Phase == SolitonTunnelShapeMath.Phase.Stay)
            && W.S0 <= 0.5f && W.S1 >= L - 0.5f;

        /// <summary>
        /// A tube of <paramref name="shape"/> (<see cref="SolitonTunnelShapeMath.ShapeAt"/>) from <paramref name="a"/> heading
        /// <paramref name="t0"/>, bending toward <paramref name="side"/>, forming over <paramref name="formSeconds"/>, tinted
        /// by <paramref name="config"/> from its maker's <paramref name="colour"/>. Not yet on <see cref="Live"/>
        /// (<see cref="Add"/>).
        /// </summary>
        public static SolitonTunnel Make(SolitonTunnelConfigSO config, ISolitonTunnelMaker maker, Color colour, int seq,
            Vector3 a, Vector3 t0, Vector3 side, in SolitonTunnelShapeMath.Shape shape, float formSeconds)
        {
            var t = new SolitonTunnel
            {
                A = a, T0 = t0, Side = side,
                L = shape.L, S = shape.S, R = shape.R, B = shape.B, M = shape.M, Theta = shape.Theta,
                FormT = Mathf.Max(0.05f, formSeconds),
                Path = new SolitonTunnelShapeMath.Conic(shape.L, shape.Theta, shape.Oval),
                Colour = colour, Maker = maker, Config = config, Seq = seq, Born = Time.time,
            };
            t.Tint = config ? config.Tint(colour, maker != null) : new Color(colour.r, colour.g, colour.b, 0f);
            t.W = SolitonTunnelShapeMath.NewWindow(t.L);
            return t;
        }

        /// <summary>On <see cref="Live"/> (drawn and rideable).</summary>
        public static void Add(SolitonTunnel t) { if (t != null && !s_live.Contains(t)) s_live.Add(t); }

        /// <summary>Off <see cref="Live"/>.</summary>
        public static void Remove(SolitonTunnel t) => s_live.Remove(t);

        /// <summary>Every tube <paramref name="maker"/> made, off <see cref="Live"/>.</summary>
        public static void RemoveAllBy(ISolitonTunnelMaker maker)
        {
            for (int i = s_live.Count - 1; i >= 0; i--) if (ReferenceEquals(s_live[i].Maker, maker)) s_live.RemoveAt(i);
        }

        /// <summary>
        /// Its life this frame (the page's <c>stepTunnel</c>): forms over its window, stays <paramref name="staySeconds"/>
        /// once its rider is out (forever while <see cref="Held"/>), collapses over <paramref name="collapseSeconds"/>, then
        /// fades over <see cref="SolitonTunnelShapeMath.FadeSeconds"/>. <paramref name="collapsed"/> is true on the step it
        /// finishes collapsing (the maker's collapse sound). False once it is gone (the caller takes it off
        /// <see cref="Live"/>).
        /// </summary>
        public bool Step(float staySeconds, float collapseSeconds, float dt, out bool collapsed)
        {
            collapsed = false;
            if (Dying)
            {
                DieT += dt;
                return DieT < SolitonTunnelShapeMath.FadeSeconds;
            }
            float stay = Held ? float.PositiveInfinity : staySeconds;
            if (!SolitonTunnelShapeMath.Step(ref W, L, FormT, stay, collapseSeconds, dt)) return true;
            Dying = true;
            DieT = 0f;
            collapsed = true;
            return true;
        }

        /// <summary>Its life by its own config.</summary>
        public bool Step(float dt, out bool collapsed) =>
            Step(Config ? Config.StaySeconds : 10f, Config ? Config.CollapseSeconds : 0.8f, dt, out collapsed);

        /// <summary>Its rider came out: an open tube starts its stay.</summary>
        public void RiderOut() => SolitonTunnelShapeMath.RiderOut(ref W);

        /// <summary>A newer tube replaces it: it collapses now (unless <see cref="Held"/>).</summary>
        public void CollapseNow() { if (!Dying && !Held) SolitonTunnelShapeMath.CollapseNow(ref W); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ResetOnLoad() => s_live.Clear();
    }
}
