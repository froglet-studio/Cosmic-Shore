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
        readonly List<Prism> _tickOrder = new();   // snapshot; see Tick()
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
                // A burn's clock starts when it is LIT and is NEVER extended by
                // re-exposure. The claw re-ignites its whole cone every IgniteTick,
                // so refreshing here reset the 3s timer every ~0.25s for as long as
                // the trigger was held - fire spread (spread skips already-burning
                // prisms) but nothing the player actually aimed at ever burned out.
                // Only the STEAL upgrade may still be applied to a live burn.
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
                _burning.Remove(p);
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
                // A random unit-sphere puff, matching how a timed shield pop sheds. The
                // old vector was measured from THIS MANAGER's transform - a bare GameObject
                // parked at the world origin - so every burnout pushed radially away from
                // (0,0,0), which is a direction that means nothing to a burning prism.
                var impact = Random.onUnitSphere * 0.5f;
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
