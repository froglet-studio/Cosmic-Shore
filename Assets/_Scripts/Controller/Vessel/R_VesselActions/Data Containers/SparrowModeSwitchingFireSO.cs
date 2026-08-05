using System.Collections.Generic;
using Obvious.Soap;
using UnityEngine;
using CosmicShore.Gameplay;
namespace CosmicShore.Gameplay
{
    [CreateAssetMenu(fileName = "ModeSwitchingFire",
        menuName = "ScriptableObjects/Vessel Actions/Mode Switching Fire")]
    public class SparrowModeSwitchingFireSO : ShipActionSO
    {
        [Header("Actions")] [SerializeField] private ShipActionSO normalFire;
        [SerializeField] private ShipActionSO stationaryFire;

        [Header("Events")] [SerializeField] private ScriptableEventBool stationaryModeChanged;

        // ShipActionSO assets are SHARED across every vessel of the class (human vessels
        // are not given clones), so hold state on instance fields gets stomped by whichever
        // vessel pressed last — runaway fire loops / guns that die mid-hold with 2+ Sparrows.
        // State is keyed per vessel instead; entries are pruned if a vessel dies mid-hold.
        private sealed class HoldState
        {
            public ShipActionSO Active;
            public ActionExecutorRegistry Registry;
        }

        private readonly Dictionary<IVesselStatus, HoldState> _held = new();
        private bool _subscribed;

        public override void StartAction(ActionExecutorRegistry execs, IVesselStatus vs)
        {
            if (vs == null) return;
            PruneDestroyed();

            // Re-press while already held (input replay / RPC echo): stop the previous
            // action through its own registry so a running fire loop is never leaked.
            if (_held.TryGetValue(vs, out var stale))
                stale.Active?.StopAction(stale.Registry ? stale.Registry : execs, vs);

            var state = new HoldState
            {
                Registry = execs,
                Active = vs is { IsTranslationRestricted: true } ? stationaryFire : normalFire
            };
            _held[vs] = state;

            EnsureSubscribed();
            state.Active?.StartAction(execs, vs);
        }

        public override void StopAction(ActionExecutorRegistry execs, IVesselStatus vs)
        {
            if (vs != null && _held.TryGetValue(vs, out var state))
            {
                state.Active?.StopAction(state.Registry ? state.Registry : execs, vs);
                _held.Remove(vs);
            }

            PruneDestroyed();
            UnsubscribeIfIdle();
        }

        private void OnStationaryModeChanged(bool _)
        {
            // The event is global and carries no vessel identity — treat it purely as a
            // "re-evaluate" tick: each held vessel consults its OWN replicated
            // IsTranslationRestricted, so one vessel's stance toggle can never flip
            // another vessel's held fire mode.
            PruneDestroyed();
            foreach (var kvp in _held)
            {
                var vs = kvp.Key;
                var state = kvp.Value;
                var desired = vs is { IsTranslationRestricted: true } ? stationaryFire : normalFire;
                if (desired == state.Active) continue;

                state.Active?.StopAction(state.Registry, vs);
                state.Active = desired;
                state.Active?.StartAction(state.Registry, vs);
            }
        }

        private void EnsureSubscribed()
        {
            if (_subscribed) return;
            stationaryModeChanged.OnRaised += OnStationaryModeChanged;
            _subscribed = true;
        }

        // Only called from Start/Stop paths — never from inside the event handler, so the
        // subscriber list is not mutated mid-raise.
        private void UnsubscribeIfIdle()
        {
            if (!_subscribed || _held.Count > 0) return;
            stationaryModeChanged.OnRaised -= OnStationaryModeChanged;
            _subscribed = false;
        }

        private void PruneDestroyed()
        {
            List<IVesselStatus> dead = null;
            foreach (var kvp in _held)
            {
                if (kvp.Key is Object obj && obj) continue;
                (dead ??= new List<IVesselStatus>()).Add(kvp.Key);
            }

            if (dead == null) return;
            foreach (var vs in dead)
                _held.Remove(vs);
        }
    }
}
