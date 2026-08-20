using System.Collections.Generic;
using CosmicShore.Data;
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
    /// v1 visual = the Danger prism state (MakeDangerous); a dedicated Burning
    /// material state needs ThemeManager assets and is deferred.
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
        [SerializeField] int maxBurnoutsPerTick = 8;
        [SerializeField, Tooltip("Main tick frequency (Hz).")]
        float tickHz = 4f;

        class BurnState
        {
            public string Igniter;
            public Domains IgniterDomain;
            public float ExtinguishAt;
            public float NextSpreadAt;
            public bool ConvertOnBurnout;
        }

        readonly Dictionary<Prism, BurnState> _burning = new();
        readonly List<Prism> _scratch = new();
        readonly List<Prism> _toResolve = new();
        float _nextTick;

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
        /// </summary>
        public void Ignite(Prism prism, string igniter, Domains igniterDomain, bool convertOnBurnout)
        {
            if (prism == null || prism.destroyed) return;
            if (prism.prismProperties != null && prism.prismProperties.IsSuperShielded) return;
            if (prism.Domain == igniterDomain) return;   // never burn your own volume

            if (_burning.TryGetValue(prism, out var state))
            {
                state.ExtinguishAt = Time.time + burnSeconds;
                state.ConvertOnBurnout |= convertOnBurnout;
                return;
            }

            if (_burning.Count >= maxConcurrentBurning) return;

            _burning[prism] = new BurnState
            {
                Igniter = igniter,
                IgniterDomain = igniterDomain,
                ExtinguishAt = Time.time + burnSeconds,
                NextSpreadAt = Time.time + spreadInterval,
                ConvertOnBurnout = convertOnBurnout,
            };

            prism.MakeDangerous();   // v1 burning visual: the danger state
        }

        void Update()
        {
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

            foreach (var kvp in _burning)
            {
                var prism = kvp.Key;
                var state = kvp.Value;

                if (prism == null || prism.destroyed)
                {
                    _toResolve.Add(prism);
                    continue;
                }

                // Spread roll
                if (now >= state.NextSpreadAt)
                {
                    state.NextSpreadAt = now + spreadInterval;
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
                if (p != null) _burning.Remove(p);
                else _burning.Remove(p);
            _toResolve.Clear();
        }

        void TrySpread(Prism source, BurnState state)
        {
            var index = PrismSpatialIndex.Instance;
            if (index == null) return;

            _scratch.Clear();
            index.QuerySphere(source.transform.position, spreadRadius, _scratch);

            foreach (var neighbor in _scratch)
            {
                if (neighbor == null || neighbor == source || neighbor.destroyed) continue;
                if (_burning.ContainsKey(neighbor)) continue;
                if (_burning.Count >= maxConcurrentBurning) break;
                if (Random.value > spreadChance) continue;

                Ignite(neighbor, state.Igniter, state.IgniterDomain, state.ConvertOnBurnout);
            }
        }

        void ResolveBurnout(Prism prism, BurnState state)
        {
            if (prism == null || prism.destroyed) return;

            if (state.ConvertOnBurnout)
            {
                // Mass-5 plasma claw: fire STEALS — the prism joins the igniter's team.
                // (Shielded prisms decay their shield instead — Steal's own semantics —
                // giving a natural two-stage burn for shielded volume.)
                prism.Steal(state.Igniter, state.IgniterDomain);
            }
            else
            {
                var impact = (prism.transform.position - transform.position).normalized * 0.5f;
                prism.Damage(impact, state.IgniterDomain, state.Igniter);
            }
        }

        /// <summary>Clears all burn state (turn end / scene transitions).</summary>
        public void ResetAll()
        {
            _burning.Clear();
            _scratch.Clear();
            _toResolve.Clear();
        }
    }
}
