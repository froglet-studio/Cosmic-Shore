using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A Soliton Tunnel (or a Soliton Network) placed in the scene with no maker: the environmental fundamental on its own.
    /// Drop it on any GameObject: at Start it places its tubes at its transform (its forward the way in), or scattered
    /// at random within <see cref="scatterRadius"/>, from its <see cref="SolitonTunnelConfigSO"/> (look, tint, shape,
    /// life, slide). Any vessel with a <see cref="SolitonTunnelRider"/> (the Meerkat's) rides them: fly into a tunnel's way
    /// in, or a network's source. Permanent tubes stand forever; otherwise each stands <see cref="lifeSeconds"/>, collapses
    /// and is placed again after <see cref="respawnSeconds"/> (0 = never). It steps its own tubes and takes them away when it
    /// is disabled.
    /// </summary>
    public sealed class SolitonTunnelPlacer : MonoBehaviour
    {
        public enum Kind { Tunnel = 0, Network = 1 }

        [Tooltip("The tube's look, tint, shape, life and slide (a SolitonTunnelConfigSO; edit them all in Froglet Tools > Environment > Environmental Fundamentals).")]
        [SerializeField] SolitonTunnelConfigSO config;
        [Tooltip("A single Soliton Tunnel, or a Soliton Network.")]
        [SerializeField] Kind kind = Kind.Tunnel;
        [Tooltip("How many to place (each one its own random spot when scattered).")]
        [SerializeField, Min(1)] int count = 1;
        [Tooltip("0: at this transform. Above 0: each one at a random spot within this radius (u), heading a random way.")]
        [SerializeField, Min(0f)] float scatterRadius;
        [Tooltip("The random seed (0 = a new one each play).")]
        [SerializeField] int seed;

        [Header("Tunnel")]
        [Tooltip("Its shaft, 0 (the shortest) to 1 (the longest).")]
        [SerializeField, Range(0f, 1f)] float length = 0.5f;
        [Tooltip("Its bend, as a stick would push it (x right, y up; 0 = straight). Random each placing when scattered and 'Random Bend' is on.")]
        [SerializeField] Vector2 bend;
        [SerializeField] bool randomBend = true;
        [Tooltip("The shortest and longest shaft (u) and the most it bends (deg).")]
        [SerializeField] float shaftMin = 120f, shaftMax = 400f, bendMaxDegrees = 120f;

        [Header("Network")]
        [Tooltip("The network's reach, a share of its cell (0..1).")]
        [SerializeField, Range(0.05f, 1f)] float networkReach = 0.5f;
        [Tooltip("The cell the network fills (u).")]
        [SerializeField, Min(10f)] float cellRadius = 600f;

        [Header("Life")]
        [Tooltip("Stands forever (riders never collapse it).")]
        [SerializeField] bool permanent = true;
        [Tooltip("Not permanent: how long each one stands before it collapses (s).")]
        [SerializeField, Min(0.5f)] float lifeSeconds = 30f;
        [Tooltip("Not permanent: placed again this long after it is gone (s; 0 = never).")]
        [SerializeField, Min(0f)] float respawnSeconds = 5f;

        readonly List<SolitonTunnel> _tunnels = new();
        readonly List<SolitonNetwork> _networks = new();
        readonly List<float> _tunnelAges = new();
        readonly List<float> _networkAges = new();
        float _respawnIn = -1f;
        int _seq;
        System.Random _rand;

        /// <summary>The config it places from.</summary>
        public SolitonTunnelConfigSO Config { get => config; set => config = value; }

        void OnEnable()
        {
            _rand = new System.Random(seed != 0 ? seed : System.Environment.TickCount ^ GetInstanceID());
            PlaceAll();
        }

        void OnDisable() => ClearAll();

        /// <summary>Takes its tubes away and places them again (the editor window's Re-place).</summary>
        [ContextMenu("Re-place")]
        public void Replace()
        {
            ClearAll();
            PlaceAll();
        }

        void PlaceAll()
        {
            if (!config) return;
            for (int i = 0; i < count; i++) PlaceOne();
        }

        void ClearAll()
        {
            for (int i = 0; i < _tunnels.Count; i++) SolitonTunnel.Remove(_tunnels[i]);
            for (int i = 0; i < _networks.Count; i++) _networks[i].Remove();
            _tunnels.Clear();
            _networks.Clear();
            _tunnelAges.Clear();
            _networkAges.Clear();
            _respawnIn = -1f;
        }

        float R01() => (float)_rand.NextDouble();

        void Spot(out Vector3 a, out Vector3 fwd, out Vector3 right, out Vector3 up)
        {
            var tr = transform;
            if (scatterRadius <= 0f)
            {
                a = tr.position; fwd = tr.forward; right = tr.right; up = tr.up;
                return;
            }
            var dir = new Vector3(R01() * 2f - 1f, R01() * 2f - 1f, R01() * 2f - 1f);
            a = tr.position + dir.normalized * (scatterRadius * Mathf.Pow(R01(), 1f / 3f));
            var q = Quaternion.Euler(R01() * 360f, R01() * 360f, R01() * 360f);
            fwd = q * Vector3.forward; right = q * Vector3.right; up = q * Vector3.up;
        }

        void PlaceOne()
        {
            Spot(out var a, out var fwd, out var right, out var up);
            if (kind == Kind.Network)
            {
                var n = SolitonNetwork.Grow(config, null, ++_seq, unchecked((uint)_rand.Next()), networkReach, cellRadius,
                    a + fwd * (cellRadius * networkReach * 0.5f), a, fwd, right, up);
                n.Held = permanent;
                _networks.Add(n);
                _networkAges.Add(0f);
                return;
            }
            var b = randomBend && scatterRadius > 0f ? new Vector2(R01() * 2f - 1f, R01() * 2f - 1f) : bend;
            var sh = SolitonTunnelShapeMath.ShapeFor(config.Shape(shaftMin, shaftMax, bendMaxDegrees), length, b.x, b.y);
            Vector3 c = sh.Stick >= 0.05f ? right * sh.Dx + up * sh.Dy : right;
            c -= fwd * Vector3.Dot(c, fwd);
            var side = c.sqrMagnitude > 1e-8f ? c.normalized : right;
            var t = SolitonTunnel.Make(config, null, Color.white, ++_seq, a, fwd, side, sh, config.FormSeconds);
            t.Held = permanent;
            SolitonTunnel.Add(t);
            _tunnels.Add(t);
            _tunnelAges.Add(0f);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < _tunnels.Count; i++)
            {
                var t = _tunnels[i];
                _tunnelAges[i] += dt;
                if (!permanent && _tunnelAges[i] >= lifeSeconds) t.CollapseNow();
                if (t.Step(dt, out _)) continue;
                SolitonTunnel.Remove(t);
                _tunnels.RemoveAt(i);
                _tunnelAges.RemoveAt(i);
                i--;
                Gone();
            }
            for (int i = 0; i < _networks.Count; i++)
            {
                var n = _networks[i];
                _networkAges[i] += dt;
                if (!permanent && _networkAges[i] >= lifeSeconds) n.CollapseNow();
                if (n.Step(dt)) continue;
                n.Remove();
                _networks.RemoveAt(i);
                _networkAges.RemoveAt(i);
                i--;
                Gone();
            }
            if (_respawnIn >= 0f)
            {
                _respawnIn -= dt;
                if (_respawnIn < 0f && config && isActiveAndEnabled)
                    while (_tunnels.Count + _networks.Count < count) PlaceOne();
            }
        }

        // the spindle bridge over its tubes (drawn once a frame, shared with every maker)
        void LateUpdate()
        {
            if (_tunnels.Count + _networks.Count > 0) SolitonTunnelView.DrawOncePerFrame();
        }

        void Gone()
        {
            if (!permanent && respawnSeconds > 0f && _respawnIn < 0f) _respawnIn = respawnSeconds;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.6f);
            if (scatterRadius > 0f) Gizmos.DrawWireSphere(transform.position, scatterRadius);
            else Gizmos.DrawRay(transform.position, transform.forward * shaftMax);
        }
    }
}
