using System;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "OverchargeCountEvent", menuName = "HUD/Channels/Overcharge Count Event")]
    public class OverchargeEventSO : ScriptableObject
    {
        public event Action<IShipStatus, int, int> OnRaised;

        private readonly Dictionary<IShipStatus, (int current, int max)> _latest = new();

        public void Raise(IShipStatus ship, int current, int max)
        {
            if (ship == null) return;
            _latest[ship] = (current, max);
            OnRaised?.Invoke(ship, current, max);
        }

        public bool TryGetLatest(IShipStatus ship, out int current, out int max)
        {
            if (_latest.TryGetValue(ship, out var t)) { current = t.current; max = t.max; return true; }
            current = 0; max = 0; return false;
        }
    }
}