using System.Collections.Generic;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A per-vessel anti-spam table for an impact effect: the last time the effect applied to each
    /// vessel, keyed by its <see cref="ResourceSystem"/>. The four vessel debuff/overtake effects
    /// each hold one as a static, so a match's worth of vessels used to stay in the table for the
    /// whole process after they were destroyed, every dead key holding its vessel and the vessel's
    /// trail: 11,874 retained prisms after 10 Skim Race matches (Port/docs/AI_TRAINING.md,
    /// "Long runs"). A vessel seen for the first time now pays for a prune of every destroyed key,
    /// so the table only ever grows by live vessels and a match's dead keys never outlive the next.
    /// Destroyed is Unity's fake null (<c>key == null</c>), deliberately not ReferenceEquals.
    /// </summary>
    public sealed class VesselEffectCooldowns
    {
        readonly Dictionary<ResourceSystem, float> _lastEffectTime = new();
        readonly List<ResourceSystem> _dead = new();

        /// <summary>Vessels currently tracked, live or not yet pruned.</summary>
        public int Count => _lastEffectTime.Count;

        /// <summary>
        /// True when the effect may apply to <paramref name="vessel"/> now, recording the time;
        /// false within <paramref name="cooldown"/> seconds of its last application.
        /// </summary>
        public bool TryBegin(ResourceSystem vessel, float now, float cooldown)
        {
            if (_lastEffectTime.TryGetValue(vessel, out var lastTime))
            {
                if (now - lastTime < cooldown) return false;
            }
            else
            {
                PruneDestroyed();
            }
            _lastEffectTime[vessel] = now;
            return true;
        }

        /// <summary>Drops every entry whose vessel has been destroyed. Allocation-free after the first call.</summary>
        public void PruneDestroyed()
        {
            foreach (var key in _lastEffectTime.Keys)
                if (key == null) _dead.Add(key);
            foreach (var key in _dead) _lastEffectTime.Remove(key);
            _dead.Clear();
        }

        public void Clear()
        {
            _lastEffectTime.Clear();
            _dead.Clear();
        }
    }
}
