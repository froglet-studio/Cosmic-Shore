using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// THE RULE of a Soliton Tunnel (Yash, 2026-11-20: "anything inside a wormhole won't interact with anything outside the
    /// hole"; 2026-10-28: "it's like going through another dimension"): a rider, and the prisms and crystals a tunnel
    /// carries, are in that tube and nowhere else. Two things interact only when they are in the SAME place: both in the
    /// world, or both in the same tube. A rider and a rival outside, a rider and a rider in another tube, a blast outside and
    /// cargo inside: apart.
    ///
    /// <para><b>Who answers.</b> Whoever flies a hull through a tube registers as an <see cref="IOccupant"/> and says which
    /// tube it is in (the Meerkat: <see cref="MeerkatSolitonExecutor"/>, on every machine; its owner knows it is riding, a
    /// peer reads the hull's place against the tubes it has, <see cref="TubeAt"/>). Whoever carries cargo through a tube
    /// <see cref="Seal(Object, object)"/>s it for the run (<see cref="MeerkatSwallow"/>). Nothing here knows a vessel class.</para>
    ///
    /// <para><b>Who asks.</b> The places where one thing lands on another, with one line each: a blast, a round, a mine or
    /// a skimmer meeting a hull (<c>ExplosionImpactor</c>, <c>ProjectileImpactor</c>, <c>MineImpactor</c>,
    /// <c>SkimmerImpactor</c>, <c>VesselImpactor</c>), a crystal meeting its collector, a sentinel choosing whom to zap.
    /// A scene with no rider and no cargo answers "not apart" at once (<see cref="Idle"/>). A swept contact a mode hands
    /// over on purpose (a race ring threaded inside a tube) does not ask: it still counts.</para>
    ///
    /// The audit of every way in or out: <c>R_VesselActions/MEERKAT.md</c> §11.
    /// </summary>
    public static class SolitonIsolation
    {
        /// <summary>A hull that may be inside a tube. Its own code says which.</summary>
        public interface IOccupant
        {
            /// <summary>The hull.</summary>
            IVesselStatus Status { get; }
            /// <summary>The tube it is in now (a <see cref="SolitonTunnel"/>, a <see cref="SolitonNetwork"/>, or a token of
            /// its own for a place only it is in), or null in the world.</summary>
            object Tube { get; }
        }

        /// <summary>How far outside a tube's wall a peer's copy of a rider still reads as inside it (u): the slide presses
        /// the hull to the wall and a replicated pose lags a little.</summary>
        public const float HullMargin = 4f;

        static readonly List<IOccupant> s_occupants = new();
        static readonly Dictionary<int, object> s_cargo = new();

        /// <summary>No hull that could be inside a tube and no cargo in one: nothing is apart from anything.</summary>
        public static bool Idle => s_occupants.Count == 0 && s_cargo.Count == 0;

        public static void Register(IOccupant occupant)
        {
            if (occupant != null && !s_occupants.Contains(occupant)) s_occupants.Add(occupant);
        }

        public static void Unregister(IOccupant occupant) => s_occupants.Remove(occupant);

        /// <summary>Every hull that may be inside a tube (the lens asks which ride the tube a camera is in).</summary>
        public static IReadOnlyList<IOccupant> Occupants => s_occupants;

        /// <summary>Cargo goes into <paramref name="tube"/> (a carried prism or crystal). Pair with <see cref="Unseal"/>.</summary>
        public static void Seal(Object cargo, object tube)
        {
            if (cargo && tube != null) s_cargo[cargo.GetInstanceID()] = tube;
        }

        /// <summary>Cargo is out of its tube (fired, set down, or gone).</summary>
        public static void Unseal(Object cargo)
        {
            if (!ReferenceEquals(cargo, null)) s_cargo.Remove(cargo.GetInstanceID());
        }

        /// <summary>The tube <paramref name="hull"/> is in, or null in the world (or for a hull nobody flies through tubes).</summary>
        public static object TubeOf(IVesselStatus hull)
        {
            if (hull == null) return null;
            for (int i = 0; i < s_occupants.Count; i++)
                if (ReferenceEquals(s_occupants[i].Status, hull)) return s_occupants[i].Tube;
            return null;
        }

        /// <summary>The tube carrying <paramref name="cargo"/>, or null.</summary>
        public static object TubeOf(Object cargo) =>
            s_cargo.Count > 0 && cargo && s_cargo.TryGetValue(cargo.GetInstanceID(), out var tube) ? tube : null;

        /// <summary>The whole rule: two places are apart unless they are the same place (null is the world).</summary>
        public static bool ApartPlaces(object placeA, object placeB) => !ReferenceEquals(placeA, placeB);

        /// <summary>One of these hulls is inside a tube the other is not in: nothing of one lands on the other.</summary>
        public static bool Apart(IVesselStatus a, IVesselStatus b) => !Idle && ApartPlaces(TubeOf(a), TubeOf(b));

        /// <summary>The hull and the cargo are not in the same place (a carried crystal and a hull outside its tube).</summary>
        public static bool Apart(IVesselStatus hull, Object cargo) => !Idle && ApartPlaces(TubeOf(hull), TubeOf(cargo));

        /// <summary>The hull is inside a tube: nothing in the world reaches it, and it reaches nothing in the world.</summary>
        public static bool Sealed(IVesselStatus hull) => !Idle && TubeOf(hull) != null;

        /// <summary>The cargo is inside a tube.</summary>
        public static bool Sealed(Object cargo) => TubeOf(cargo) != null;

        // ------------------------------------------------------------------ where a point is

        /// <summary>
        /// Whether a point <paramref name="fromAxis"/> from a tube's centreline, level with <paramref name="along"/> on it, is
        /// in the tube's hollow: within its live window [<paramref name="s0"/>, <paramref name="s1"/>] and inside its wall
        /// (<paramref name="radius"/> there) by <paramref name="margin"/>.
        /// </summary>
        public static bool InHollow(float along, float s0, float s1, float fromAxis, float radius, float margin) =>
            s1 > s0 && along >= s0 && along <= s1 && radius > 0f && fromAxis <= radius + Mathf.Max(0f, margin);

        /// <summary>
        /// The live tube whose hollow holds <paramref name="point"/> (a tunnel, else a network), or null. What a peer asks of a
        /// rider it does not simulate: the riding flag is not replicated, the hull's pose and every tube are.
        /// </summary>
        public static object TubeAt(Vector3 point, float margin = HullMargin)
        {
            var tunnels = SolitonTunnel.Live;
            for (int i = 0; i < tunnels.Count; i++)
            {
                var t = tunnels[i];
                if (t == null || t.Dying || t.W.S1 - t.W.S0 < 1f) continue;
                float s = SolitonTunnelShapeMath.NearestAlong(t.A, t.T0, t.Side, t.Path, t.L, point);
                if (s < t.W.S0 || s > t.W.S1) continue;
                if (InHollow(s, t.W.S0, t.W.S1, Vector3.Distance(point, t.At(s, out _)), t.Radius(s), margin)) return t;
            }
            var nets = SolitonNetwork.Live;
            if (nets.Count == 0) return null;
            var p = SolitonNetwork.D(point);
            for (int n = 0; n < nets.Count; n++)
            {
                var net = nets[n];
                double tip = net.Config ? net.Config.NetworkTip : 1.0;
                for (int c = 0; c < net.Channels.Count; c++)
                {
                    ref var w = ref net.Life[c];
                    if (w.Phase == SolitonTunnelMath.Phase.Gone || w.S1 - w.S0 < 1) continue;
                    var ch = net.Channels[c];
                    double s = SolitonTunnelMath.Nearest(ch.Path, p);
                    if (s < w.S0 || s > w.S1) continue;
                    float fromAxis = (float)SolitonTunnelMath.At(ch.Path, s, out _).DistanceTo(p);
                    if (InHollow((float)s, (float)w.S0, (float)w.S1, fromAxis, (float)SolitonTunnelMath.Radius(ch, s, tip), margin))
                        return net;
                }
            }
            return null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void ResetOnLoad()
        {
            s_occupants.Clear();
            s_cargo.Clear();
        }
    }
}
