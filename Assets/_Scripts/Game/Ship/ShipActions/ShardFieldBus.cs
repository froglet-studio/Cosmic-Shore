using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Game
{
    [CreateAssetMenu(fileName = "ShardFieldBus", menuName = "CosmicShore/Buses/Shard Field Bus")]
    public class ShardFieldBus : ScriptableObject
    {
        // runtime-only, not serialized (scene objects)
        readonly HashSet<SnowChanger> _listeners = new HashSet<SnowChanger>();
        public void Register(SnowChanger changer)   { if (changer != null) _listeners.Add(changer); }
        public void Unregister(SnowChanger changer) { if (changer != null) _listeners.Remove(changer); }

        public void BroadcastPointAtPosition(Vector3 worldPos)
        {
            // foreach (var l in _listeners) l.PointAtPosition(worldPos);
        }
        
        public void BroadcastRestoreToCrystal()
        {
            // foreach (var l in _listeners) l.RestoreToCrystal();
        }
    }
}