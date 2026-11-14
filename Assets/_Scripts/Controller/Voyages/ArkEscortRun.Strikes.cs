using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Wild strikes (Garrett, 2026-11-14, with the Jewel Wasp: "right trigger should animate the fauna with an attack animation
    /// against vessels (all the brittlestar's legs whip out). Fauna missing this should have this and use it against vessels
    /// when attacking an ark"). On a hostile world the escort has not pacified (the worlds whose fauna the lure already sends
    /// at the Ark, <see cref="TickChaos"/>), a wild creature within reach of a squad vessel plays its attack at it
    /// (<see cref="Fauna.PlayPossessionAttack"/>) and lands the escort's danger contact on it
    /// (<see cref="ArkEscortSO.FaunaStrikeEffect"/>): petals only, the burn rules' weight, the effect's own per-vessel
    /// cooldown on top of each creature's. A possessed creature never strikes on its own (its rider decides), nor a
    /// murmuration (it swarms instead), nor a creature this machine does not simulate.
    /// </summary>
    public sealed partial class ArkEscortRun
    {
        /// <summary>How often the escort looks for a creature close enough to strike (s).</summary>
        const float StrikeScanSeconds = 0.25f;

        float _nextStrikeScan;
        readonly Dictionary<Fauna, float> _struckAt = new();
        readonly HashSet<Fauna> _strikeSeen = new();

        void TickWildStrikes()
        {
            var fx = _escort ? _escort.FaunaStrikeEffect : null;
            var ark = Ark;
            if (!fx || !ark) return;
            var tier = _escort.TierFor(_difficulty);
            if (tier == null || tier.LureShare <= 0f) return;
            var squad = ark.Domain;
            CollectSeats();
            if (_seatScratch.Count == 0) return;
            float now = Time.time;
            float cooldown = _escort.FaunaStrikeCooldown;
            _strikeSeen.Clear();
            for (int leg = 0; leg < _cellOfLeg.Length; leg++)
            {
                var cell = _cellOfLeg[leg];
                if (!cell || LegHeld(leg, cell, squad)) continue;
                var live = cell.LiveFauna;
                for (int i = 0; i < live.Count; i++)
                {
                    var f = live[i] ? live[i].PossessionHost : null;   // a worm strikes once, with its head
                    if (!f || !_strikeSeen.Add(f)) continue;
                    if (f.IsPossessed || !f.IsSimAuthority || f.Domain == squad || f is SwarmFauna) continue;
                    if (_struckAt.TryGetValue(f, out float at) && now - at < cooldown) continue;
                    Vector3 from = f.PossessionNestPoint;
                    float reach = _escort.FaunaStrikeReach + f.PossessionBodyRadius;
                    float reachSqr = reach * reach;
                    for (int s = 0; s < _seatScratch.Count; s++)
                    {
                        var vessel = _seatScratch[s].status;
                        var hull = vessel?.Transform;
                        if (!hull || (hull.position - from).sqrMagnitude > reachSqr) continue;
                        _struckAt[f] = now;
                        f.PlayPossessionAttack(hull.position, _escort.FaunaStrikeSeconds);
                        fx.ApplyContact(vessel, f.Domain, _escort.FaunaStrikeWeight);
                        break;
                    }
                }
            }
            // forget the dead
            if (_struckAt.Count > 64)
            {
                s_strikeScratch.Clear();
                foreach (var kv in _struckAt) if (!kv.Key || now - kv.Value > cooldown) s_strikeScratch.Add(kv.Key);
                for (int i = 0; i < s_strikeScratch.Count; i++) _struckAt.Remove(s_strikeScratch[i]);
                s_strikeScratch.Clear();
            }
        }

        static readonly List<Fauna> s_strikeScratch = new();
    }
}
