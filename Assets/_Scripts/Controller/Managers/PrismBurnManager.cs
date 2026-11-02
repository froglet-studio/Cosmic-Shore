using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The game's burning-status system (net-new for the Grizzly's plasma claw — no
    /// burn/DoT existed before). Prisms that are ignited burn for a few seconds,
    /// roll to SPREAD to neighbors (via PrismSpatialIndex.QuerySphere), and on
    /// burnout are either DESTROYED or — when ignited by a Mass-5 plasma claw —
    /// STOLEN for the igniter's team (fire → theft → volume conversion).
    ///
    /// Visual: the Danger prism state (MakeDangerous), and - when Resources/PrismBurnFxConfig is present - the
    /// paper-burn proxies of <see cref="PrismBurnView"/> (near curl / mid dissolve / far nothing), with SILENT
    /// burnouts (<see cref="Prism.BurnOut"/>): the prism already burned away on screen, so no debris.
    ///
    /// Spread rule: every burn carries a <see cref="PrismBurnSpread"/> (generation cap, radius, chance x decay^gen,
    /// per-burn child cap, child duration scale). The claw's legacy calls use <see cref="PrismBurnSpread.Unbounded"/>
    /// of the fields below (its behaviour is unchanged); Fulmen passes its config's bounded, decaying "forest fire".
    /// Burn states are pooled (no allocation per burn once warm). Docs/Shaders/PRISM_EFFECTS.md.
    ///
    /// Perf contract: burning set is capped, ticks run at ~4 Hz staggered across
    /// frames, and burnouts are budgeted per tick (each Steal/Damage triggers
    /// material/animation work). Everything clears on turn end via ResetAll().
    /// </summary>
    public class PrismBurnManager : Singleton<PrismBurnManager>
    {
        [Header("Burn")]
        [SerializeField, Tooltip("Seconds a prism burns before its burnout outcome.")]
        float burnSeconds = 3f;
        [SerializeField, Tooltip("Radius scanned for spread targets around a burning prism.")]
        float spreadRadius = 25f;
        [SerializeField, Range(0f, 1f), Tooltip("Chance per spread tick that each nearby prism ignites.")]
        float spreadChance = 0.35f;
        [SerializeField, Tooltip("Seconds between spread attempts per burning prism.")]
        float spreadInterval = 0.75f;

        [Header("Budgets")]
        [SerializeField] int maxConcurrentBurning = 192;
        [SerializeField] int maxBurnoutsPerTick = 24;   // was 8: Fulmen lights dozens at once, and a silent burnout is cheap (no debris)
        [SerializeField, Tooltip("Main tick frequency (Hz).")]
        float tickHz = 4f;

        sealed class BurnState
        {
            public int Generation;           // 0 = lit directly (bolt / claw), n = n spreads from it
            public int Children;             // spreads this burn has lit (capped by Spread.MaxPerBurn)
            public float RootSeconds;        // the generation-0 burn's duration; children scale from it
            public PrismBurnSpread Spread;
            public PrismBurnStyle Style;
            public string Igniter;
            public Domains IgniterDomain;
            public float ExtinguishAt;
            public float NextSpreadAt;
            public bool ConvertOnBurnout;
            public ushort NetStrike;   // != 0: a networked Fulmen strike's burn (the server reports it, spreads inherit it)
            public bool Remote;        // a client's copy of a server burn: no spread, always the quiet burnout
        }

        /// <summary>SERVER: a networked burn was just lit (strike id, prism, its seconds and noise seed). Set by
        /// <see cref="FulmenNetSync"/>; null offline / on clients.</summary>
        public static System.Action<ushort, Prism, float, float> NetIgnited;

        readonly Dictionary<Prism, BurnState> _burning = new();
        readonly Stack<BurnState> _statePool = new();
        readonly List<Prism> _scratch = new();
        readonly List<Prism> _toResolve = new();
        readonly List<Prism> _tickOrder = new();   // snapshot; see Tick()
        float _nextTick;
        PrismBurnView _view;

        public PrismBurnView View => _view;

        public override void Awake()
        {
            base.Awake();
            _view = new PrismBurnView(PrismBurnFxConfigSO.Instance);
        }

        void OnDestroy()
        {
            _view?.Dispose();
        }

        public static PrismBurnManager EnsureInstance()
        {
            if (Instance != null) return Instance;
            var go = new GameObject(nameof(PrismBurnManager));
            return go.AddComponent<PrismBurnManager>();
        }

        public int BurningCount => _burning.Count;

        /// <summary>
        /// Ignites a prism. Super-shielded prisms cannot burn; already-burning prisms
        /// refresh their timer (and can upgrade to convert-on-burnout).
        /// <paramref name="seconds"/> &gt; 0 overrides <see cref="burnSeconds"/> for this burn (Fulmen's quick burns).
        /// True when this call lit a NEW burn.
        /// </summary>
        /// <paramref name="netStrike"/> != 0 tags the burn with a networked Fulmen strike: the server reports it to
        /// clients (<see cref="NetIgnited"/>) and so does every prism it spreads to.
        public bool Ignite(Prism prism, string igniter, Domains igniterDomain, bool convertOnBurnout, float seconds = -1f,
            ushort netStrike = 0)
            => Ignite(prism, igniter, igniterDomain, convertOnBurnout, seconds, netStrike, LegacySpread, PrismBurnStyle.Paper, 0);

        /// <summary>The claw's (and every legacy caller's) spread: this manager's radius / chance / interval, no
        /// generation cap - exactly the rule before <see cref="PrismBurnSpread"/> existed.</summary>
        public PrismBurnSpread LegacySpread => PrismBurnSpread.Unbounded(spreadRadius, spreadChance, spreadInterval);

        /// <summary>Ignites with an explicit spread rule and look. <paramref name="generation"/> is 0 for a direct hit.
        /// Fulmen: <c>Ignite(prism, .., cfg.BurnSeconds, netId, cfg.BurnSpread, PrismBurnStyle.Laser, 0)</c>.</summary>
        public bool Ignite(Prism prism, string igniter, Domains igniterDomain, bool convertOnBurnout, float seconds,
            ushort netStrike, in PrismBurnSpread spread, PrismBurnStyle style, int generation)
        {
            if (prism == null || prism.destroyed) return false;
            if (prism.prismProperties != null && prism.prismProperties.IsSuperShielded) return false;
            if (prism.Domain == igniterDomain) return false;   // never burn your own volume

            if (_burning.TryGetValue(prism, out var state))
            {
                // A burn's clock starts when it is LIT and is NEVER extended by
                // re-exposure. The claw re-ignites its whole cone every IgniteTick,
                // so refreshing here reset the 3s timer every ~0.25s for as long as
                // the trigger was held - fire spread (spread skips already-burning
                // prisms) but nothing the player actually aimed at ever burned out.
                // Only the STEAL upgrade may still be applied to a live burn.
                state.ConvertOnBurnout |= convertOnBurnout;
                if (convertOnBurnout) _view?.SetNoHoles(prism);
                return false;
            }

            if (_burning.Count >= maxConcurrentBurning) return false;

            float dur = seconds > 0f ? seconds : burnSeconds;
            var st = Rent();
            st.Igniter = igniter;
            st.IgniterDomain = igniterDomain;
            st.ExtinguishAt = Time.time + dur;
            st.NextSpreadAt = spread.CanSpread(generation) ? Time.time + Mathf.Max(spread.Interval, 0.05f) : float.PositiveInfinity;
            st.ConvertOnBurnout = convertOnBurnout;
            st.NetStrike = netStrike;
            st.Generation = generation;
            st.RootSeconds = generation == 0 ? dur : Mathf.Max(seconds, 0.05f);
            st.Spread = spread;
            st.Style = style;
            _burning[prism] = st;

            float seed = netStrike != 0 ? NoiseSeed(prism.transform.position) : -1f;
            prism.MakeDangerous();   // the prism's own burning look (what FAR tier and the crossfades show)
            _view?.Begin(prism, Time.time, st.ExtinguishAt, convertOnBurnout, seed, style, igniterDomain);
            if (netStrike != 0) NetIgnited?.Invoke(netStrike, prism, dur, seed);
            return true;
        }

        /// <summary>
        /// CLIENT: the server lit <paramref name="prism"/> <paramref name="ago"/> seconds ago for
        /// <paramref name="seconds"/>. Same look and timeline as the server's burn (same noise <paramref name="seed"/>);
        /// it never spreads (the server sends its spreads) and always ends in the quiet <see cref="Prism.BurnOut"/>
        /// - one disappearance, no debris, the same moment as on the server (a convert burn steals instead, as the
        /// server's does). True when it lit a new burn.
        /// </summary>
        /// <paramref name="style"/>: every networked burn today is a Fulmen burn, so LASER.
        public bool IgniteRemote(Prism prism, string igniter, Domains igniterDomain, bool convertOnBurnout, float seconds,
            float ago, float seed, PrismBurnStyle style = PrismBurnStyle.Laser)
        {
            if (prism == null || prism.destroyed || _burning.ContainsKey(prism)) return false;
            if (_burning.Count >= maxConcurrentBurning) return false;
            float now = Time.time;
            ago = Mathf.Clamp(ago, 0f, seconds);
            var st = Rent();
            st.Igniter = igniter;
            st.IgniterDomain = igniterDomain;
            st.ExtinguishAt = now + (seconds - ago);
            st.NextSpreadAt = float.PositiveInfinity;
            st.ConvertOnBurnout = convertOnBurnout;
            st.Remote = true;
            st.Spread = PrismBurnSpread.None;
            st.Style = style;
            _burning[prism] = st;
            prism.MakeDangerous();
            _view?.Begin(prism, now - ago, st.ExtinguishAt, convertOnBurnout, seed, style, igniterDomain);
            return true;
        }

        BurnState Rent()
        {
            var st = _statePool.Count > 0 ? _statePool.Pop() : new BurnState();
            st.Igniter = null; st.IgniterDomain = default; st.ExtinguishAt = st.NextSpreadAt = 0f;
            st.ConvertOnBurnout = false; st.NetStrike = 0; st.Remote = false;
            st.Generation = 0; st.Children = 0; st.RootSeconds = 0f; st.Spread = PrismBurnSpread.None; st.Style = PrismBurnStyle.Paper;
            return st;
        }

        void Return(BurnState st)
        {
            st.Igniter = null;
            _statePool.Push(st);
        }

        /// <summary>The burn's noise seed in [1, 62), from the prism's quantised position - the same on every peer.</summary>
        public static float NoiseSeed(Vector3 p)
        {
            unchecked
            {
                uint h = (uint)Mathf.RoundToInt(p.x * 2f) * 73856093u ^ (uint)Mathf.RoundToInt(p.y * 2f) * 19349663u
                       ^ (uint)Mathf.RoundToInt(p.z * 2f) * 83492791u;
                h ^= h >> 13; h *= 0x5bd1e995u; h ^= h >> 15;
                return 1f + (h & 0xff) * (60f / 255f);
            }
        }

        /// <summary>The byte <see cref="NoiseSeed"/> packs to (and back): what a burn batch carries.</summary>
        public static byte PackSeed(float seed) => (byte)Mathf.Clamp(Mathf.RoundToInt((seed - 1f) * (255f / 60f)), 0, 255);
        public static float UnpackSeed(byte b) => 1f + b * (60f / 255f);

        void Update()
        {
            _view?.Draw(Time.time);
            if (_burning.Count == 0) return;
            if (Time.time < _nextTick) return;
            _nextTick = Time.time + (tickHz > 0f ? 1f / tickHz : 0.25f);

            Tick();
        }

        void Tick()
        {
            float now = Time.time;
            int burnouts = 0;
            _toResolve.Clear();

            // Iterate a SNAPSHOT of the keys, never the live dictionary. TrySpread ignites
            // neighbours, and Ignite writes into _burning - so enumerating _burning directly
            // threw InvalidOperationException on the first spread of every tick, which
            // aborted the loop BEFORE the burnout block. Fire spread (the spread had already
            // happened) and nothing was ever destroyed, with the exception buried in the log.
            // Prisms ignited during this tick are picked up by the next one, which is correct:
            // their burn clock starts when they are lit.
            _tickOrder.Clear();
            foreach (var key in _burning.Keys)
                _tickOrder.Add(key);

            foreach (var prism in _tickOrder)
            {
                // A prism can be removed mid-tick (burnout, destruction), so re-resolve
                // rather than trusting the snapshot's pairing.
                if (!_burning.TryGetValue(prism, out var state))
                    continue;

                if (prism == null || prism.destroyed)
                {
                    _toResolve.Add(prism);
                    continue;
                }

                // Spread roll (never on a client's replicated burn: the server sends its spreads)
                if (!state.Remote && now >= state.NextSpreadAt)
                {
                    state.NextSpreadAt = state.Spread.CanSpread(state.Generation) && state.Children < state.Spread.MaxPerBurn
                        ? now + Mathf.Max(state.Spread.Interval, 0.05f)
                        : float.PositiveInfinity;   // out of generations / children: stop rolling
                    TrySpread(prism, state);
                }

                // Burnout
                if (now >= state.ExtinguishAt && burnouts < maxBurnoutsPerTick)
                {
                    burnouts++;
                    ResolveBurnout(prism, state);
                    _toResolve.Add(prism);
                }
            }

            foreach (var p in _toResolve)
            {
                if (_burning.Remove(p, out var done)) Return(done);
                _view?.End(p);
            }
            _toResolve.Clear();
        }

        void TrySpread(Prism source, BurnState state)
        {
            var index = PrismSpatialIndex.Instance;
            if (index == null) return;

            var spread = state.Spread;
            float chance = spread.ChanceAt(state.Generation);
            if (chance <= 0f || state.Children >= spread.MaxPerBurn) return;

            _scratch.Clear();
            index.QuerySphere(source.transform.position, spread.Radius, _scratch);

            // A legacy (unbounded) spread keeps the old "-1 = the manager's burn seconds"; a bounded one scales from
            // the root burn so the fire visibly dies down generation by generation.
            float childSeconds = spread.MaxGenerations == int.MaxValue ? -1f : spread.SecondsAt(state.RootSeconds, state.Generation + 1);
            foreach (var neighbor in _scratch)
            {
                if (neighbor == null || neighbor == source || neighbor.destroyed) continue;
                if (_burning.ContainsKey(neighbor)) continue;
                if (_burning.Count >= maxConcurrentBurning) break;
                if (Random.value > chance) continue;

                if (Ignite(neighbor, state.Igniter, state.IgniterDomain, state.ConvertOnBurnout, childSeconds, state.NetStrike,
                        spread, state.Style, state.Generation + 1))
                {
                    var child = _burning[neighbor];
                    child.RootSeconds = state.RootSeconds;
                    if (++state.Children >= spread.MaxPerBurn) break;
                }
            }
        }

        void ResolveBurnout(Prism prism, BurnState state)
        {
            if (prism == null || prism.destroyed) return;

            if (state.Remote && !state.ConvertOnBurnout)
            {
                // a client's copy of a server burn: the quiet burnout, on the server's timeline. Every peer simulates
                // destruction locally (there is no authoritative destroy sync for trail mass), and this is the ONE place
                // this peer destroys the prism - no Damage, so no second explosion.
                prism.BurnOut(Random.onUnitSphere * 0.5f, state.IgniterDomain, state.Igniter);
            }
            else if (state.ConvertOnBurnout)
            {
                // Mass-5 plasma claw: fire STEALS — the prism joins the igniter's team.
                // (Shielded prisms decay their shield instead — Steal's own semantics —
                // giving a natural two-stage burn for shielded volume.)
                prism.Steal(state.Igniter, state.IgniterDomain);
            }
            else
            {
                // A random unit-sphere puff, matching how a timed shield pop sheds. The
                // old vector was measured from THIS MANAGER's transform - a bare GameObject
                // parked at the world origin - so every burnout pushed radially away from
                // (0,0,0), which is a direction that means nothing to a burning prism.
                var impact = Random.onUnitSphere * 0.5f;
                if (_view != null && _view.SilentBurnout)
                    prism.BurnOut(impact, state.IgniterDomain, state.Igniter);
                else
                    prism.Damage(impact, state.IgniterDomain, state.Igniter);
            }
        }

        /// <summary>Clears all burn state (turn end / scene transitions).</summary>
        public void ResetAll()
        {
            foreach (var st in _burning.Values) Return(st);
            _burning.Clear();
            _view?.Clear();
            _scratch.Clear();
            _toResolve.Clear();
        }
    }
}
