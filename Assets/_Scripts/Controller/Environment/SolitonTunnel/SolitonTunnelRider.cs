using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// One rider's SLIP-AND-SLIDE through a Soliton Tunnel or a Soliton Network, for any vessel (an environmental
    /// fundamental: nothing here is Meerkat-only). <see cref="Enter(SolitonTunnel, Vector3, float, Vector3)"/> puts it in a
    /// tube; <see cref="Step"/> slides it one frame (<see cref="SolitonTunnelMath.Step"/>: radial gravity to the inner wall,
    /// a bounce now and then, a small RT thrust, no steering) and hands back where the hull is, its facing, its speed and
    /// heading; on the way out it hands back the SLINGSHOT velocity (the velocity it had, × the exit multiplier, along the way
    /// it was going). A vessel's transformer flies the hull there in place of its own move (the Meerkat's:
    /// <see cref="MeerkatSolitonExecutor.StepRide"/> / <see cref="MeerkatSolitonExecutor.StepNetwork"/>).
    /// </summary>
    public sealed class SolitonTunnelRider
    {
        List<SolitonTunnelMath.Channel> _single;

        /// <summary>The tunnel it is sliding (null in a network or out).</summary>
        public SolitonTunnel Tunnel { get; private set; }
        /// <summary>The network it is sliding (null in a tunnel or out).</summary>
        public SolitonNetwork Network { get; private set; }
        /// <summary>Its state inside (null when out).</summary>
        public SolitonTunnelMath.Flight Flight { get; private set; }
        /// <summary>The channels it slides (a tunnel is a network of one).</summary>
        public IReadOnlyList<SolitonTunnelMath.Channel> Channels => Network != null ? Network.Channels : _single;
        /// <summary>True while it is in a tube (from the way in to the way out).</summary>
        public bool Riding => Flight != null;
        /// <summary>How far along its channel it is (u; below 0 while it dives to the mouth).</summary>
        public float Along => Flight != null ? (float)Flight.S : 0f;
        /// <summary>The tube's config (its slide numbers).</summary>
        public SolitonTunnelConfigSO Config => Tunnel != null ? Tunnel.Config : Network?.Config;

        /// <summary>Into <paramref name="t"/> from <paramref name="position"/> at <paramref name="speed"/>: it dives straight to
        /// the way in, then slides.</summary>
        public void Enter(SolitonTunnel t, Vector3 position, float speed, Vector3 up)
        {
            Tunnel = t;
            Network = null;
            _single = SolitonTunnelMath.Single(new SlidePath(t), t.L);
            Flight = SolitonTunnelMath.Enter(_single, 0, D(position), Mathf.Max(1f, speed), D(up));
        }

        /// <summary>Into <paramref name="n"/>'s channel <paramref name="channel"/> (its source is 0).</summary>
        public void Enter(SolitonNetwork n, int channel, Vector3 position, float speed)
        {
            Tunnel = null;
            Network = n;
            _single = null;
            Flight = SolitonTunnelMath.Enter(n.Channels, channel, D(position), Mathf.Max(1f, speed), n.Up);
        }

        /// <summary>Out (or the tube went from under it).</summary>
        public void Clear()
        {
            Tunnel = null;
            Network = null;
            Flight = null;
            _single = null;
        }

        /// <summary>The tube it is in is still alive (a despawn takes it from under the rider).</summary>
        public bool TubeStillLive
        {
            get
            {
                if (Tunnel != null) { var l = SolitonTunnel.Live; for (int i = 0; i < l.Count; i++) if (l[i] == Tunnel) return true; return false; }
                if (Network != null) { var l = SolitonNetwork.Live; for (int i = 0; i < l.Count; i++) if (l[i] == Network) return true; return false; }
                return false;
            }
        }

        /// <summary>The slide's numbers from the tube's config, its speeds and thrust × <paramref name="rush"/> (a vessel's
        /// own speed-up; 1 = none).</summary>
        public SolitonTunnelMath.FlightSettings Settings(float rush = 1f)
        {
            var cfg = Config;
            if (cfg) return Network != null ? cfg.NetworkFlight(rush) : cfg.Flight(rush);
            return new SolitonTunnelMath.FlightSettings
            {
                Gravity = 60, Bounce = 0.45, Friction = 0.05, Thrust = 25 * rush, MinSpeed = 30 * rush, MaxSpeed = 119 * rush,
                Tip = 1, ExitMultiplier = 1.2,
            };
        }

        /// <summary>
        /// One frame of the slide with RT at <paramref name="throttle"/> (0..1). Where the hull is, its facing (its up toward
        /// the tube's axis once inside; <paramref name="current"/> before), its speed and heading. On the frame it comes out
        /// (<see cref="SolitonTunnelMath.FlightResult.OutOfTip"/> / <see cref="SolitonTunnelMath.FlightResult.OutOfSource"/>)
        /// <paramref name="exitVelocity"/> is the slingshot: its speed and heading are that velocity's. The caller then
        /// <see cref="Clear"/>s it.
        /// </summary>
        public SolitonTunnelMath.FlightResult Step(in SolitonTunnelMath.FlightSettings p, float throttle, float dt, Quaternion current,
            Vector3 fallbackUp, out Vector3 position, out Quaternion rotation, out float speed, out Vector3 heading,
            out Vector3 exitVelocity)
        {
            var g = Flight;
            exitVelocity = default;
            if (g == null)
            {
                position = default; rotation = current; speed = 0f; heading = current * Vector3.forward;
                return SolitonTunnelMath.FlightResult.OutOfSource;
            }
            var res = SolitonTunnelMath.Step(g, Channels, Network != null ? Network.AliveFn : null, p, throttle, dt, out var nose);
            position = V(g.Pos);
            heading = V(nose);
            var up = g.Free ? V(g.Upv) : fallbackUp;
            rotation = Pose(heading, up, current);
            speed = (float)(g.Free ? System.Math.Sqrt(g.Vel.LengthSq) : g.VIn);
            if (res == SolitonTunnelMath.FlightResult.Riding) return res;
            // the slingshot: out the way it was going, at the speed it had (× the exit multiplier)
            var v = g.Free && g.Vel.LengthSq > 1e-6 ? V(SolitonTunnelMath.ExitVelocity(g, p))
                : heading.normalized * (float)(g.VIn * System.Math.Max(0, p.ExitMultiplier));
            exitVelocity = v;
            speed = v.magnitude;
            if (speed > 1e-4f) heading = v / speed;
            rotation = Pose(heading, up, current);
            return res;
        }

        /// <summary>A facing with <paramref name="nose"/> forward and <paramref name="up"/> as near up as it can be.</summary>
        public static Quaternion Pose(Vector3 nose, Vector3 up, Quaternion fallback)
        {
            if (nose.sqrMagnitude < 1e-8f) return fallback;
            up -= nose * Vector3.Dot(up, nose);
            if (up.sqrMagnitude < 1e-8f) up = Vector3.Cross(nose, Mathf.Abs(nose.x) < 0.9f ? Vector3.right : Vector3.up);
            return Quaternion.LookRotation(nose, up);
        }

        /// <summary>A rider would go in: <paramref name="position"/> heading <paramref name="forward"/> is at
        /// <paramref name="t"/>'s way in (inside its mouth, going in).</summary>
        public static bool AtWayIn(SolitonTunnel t, Vector3 position, Vector3 forward) =>
            t != null && t.Standing && MeerkatSolitonMath.CanEnter(position, forward, t.A, t.T0, t.M, out _);

        /// <summary>...at <paramref name="n"/>'s source (channel 0's mouth).</summary>
        public static bool AtSource(SolitonNetwork n, Vector3 position, Vector3 forward)
        {
            if (n == null || n.Channels.Count == 0 || !n.Alive(0)) return false;
            var c = n.Channels[0];
            var a = V(SolitonTunnelMath.ChanAt(c, 0, out var t0));
            float m = (float)SolitonTunnelMath.Radius(c, 0, n.Config ? n.Config.NetworkTip : 1f);
            return MeerkatSolitonMath.CanEnter(position, forward, a, V(t0), m, out _);
        }

        static SolitonTunnelMath.D3 D(Vector3 v) => new(v.x, v.y, v.z);
        static Vector3 V(SolitonTunnelMath.D3 v) => new((float)v.X, (float)v.Y, (float)v.Z);

        /// <summary>
        /// A Soliton Tunnel as a tube the slide flies (the page's <c>slAt</c> / <c>slNear</c> / <c>slR</c>): its centreline
        /// the conic arc (straight on past either end), its wall the full hyperboloid.
        /// </summary>
        public sealed class SlidePath : SolitonTunnelMath.ITubePath
        {
            readonly SolitonTunnel _t;

            public SlidePath(SolitonTunnel t) => _t = t;

            public SolitonTunnelMath.D3 At(double s, out SolitonTunnelMath.D3 tangent)
            {
                float f = (float)s;
                Vector3 p, tan;
                if (f <= 0f) { tan = _t.T0; p = _t.A + _t.T0 * f; }
                else if (f >= _t.L) { p = _t.At(_t.L, out tan); p += tan * (f - _t.L); }
                else p = _t.At(f, out tan);
                tangent = D(tan);
                return D(p);
            }

            public double Nearest(SolitonTunnelMath.D3 p) =>
                SolitonTunnelShapeMath.NearestAlong(_t.A, _t.T0, _t.Side, _t.Path, _t.L, V(p));

            public double Radius(double s) =>
                SolitonTunnelShapeMath.Radius(_t.B, _t.M, _t.L, 0f, _t.L, Mathf.Clamp((float)s, 0f, _t.L));
        }
    }
}
