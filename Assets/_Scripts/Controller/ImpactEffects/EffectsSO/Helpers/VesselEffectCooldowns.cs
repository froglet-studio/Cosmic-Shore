using System.Collections.Generic;
using CosmicShore.Data;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A per-object anti-spam table for an impact effect: the last time the effect applied to each
    /// key (a vessel's ResourceSystem, a crystal), with the one property a STATIC table needs: a
    /// key seen for the first time pays for a prune of every destroyed key, so the table only ever
    /// grows by live objects and a match's dead keys never outlive the next match. Destroyed is
    /// Unity's fake null (<c>key == null</c>), deliberately not ReferenceEquals.
    /// </summary>
    public class ObjectCooldowns<TKey> where TKey : UnityEngine.Object
    {
        readonly Dictionary<TKey, float> _lastEffectTime = new();
        readonly List<TKey> _dead = new();

        /// <summary>Keys currently tracked, live or not yet pruned.</summary>
        public int Count => _lastEffectTime.Count;

        /// <summary>
        /// True when the effect may apply to <paramref name="key"/> now, recording the time; false
        /// within <paramref name="cooldown"/> seconds of its last application.
        /// </summary>
        public bool TryBegin(TKey key, float now, float cooldown)
        {
            if (_lastEffectTime.TryGetValue(key, out var lastTime))
            {
                if (now - lastTime < cooldown) return false;
            }
            else
            {
                PruneDestroyed();
            }
            _lastEffectTime[key] = now;
            return true;
        }

        /// <summary>Drops every entry whose object has been destroyed. Allocation-free after the first call.</summary>
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

    /// <summary>
    /// The per-vessel table the four vessel debuff/overtake effects hold as a static, keyed by the
    /// victim's <see cref="ResourceSystem"/>. Each of those effects used to keep a never-pruned
    /// dictionary, every dead key holding its vessel and the vessel's trail: 11,874 retained prisms
    /// after 10 Skim Race matches for the overtake table alone, 16,886 for the danger-prism one
    /// (Port/docs/AI_TRAINING.md, "Long runs"); with the prune the scan's census fell from 27,587
    /// to 12,623 destroyed prisms reachable, none through these tables.
    /// </summary>
    public sealed class VesselEffectCooldowns : ObjectCooldowns<ResourceSystem>
    {
    }
}
