using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Cleave's lances in the world (CLEAVE.md "Lances"): every peer lays <see cref="CleaveLances"/>' prisms when the
    /// countdown ends, after the arena is standing and before anyone has cut. A part whose spot the arena already
    /// fills is skipped (the cell's prism index, the same on every peer at that moment), so no lance prism passes
    /// through an arena prism. They are environment mass with the game's own prism rules: a blade cuts them, a
    /// danger tip punishes, a super-shielded seam binds a cold blade. Nothing here reads or writes a vessel.
    /// </summary>
    public partial class CleaveController
    {
        const string LanceOwnerId = "CleaveLances";

        [Header("Lances")]
        [Tooltip("The prism every lance is laid from: SpawnablePrism, the arenas' own.")]
        [SerializeField] Prism lancePrefab;

        readonly List<CleaveLancePart> _lanceParts = new();
        readonly List<bool> _lanceKeep = new();
        readonly List<Prism> _lancePrisms = new();
        Transform _lanceRoot;

        /// <summary>How many lance prisms this peer laid this match (the rest were where the arena stands).</summary>
        public int LaidLancePrisms => _lancePrisms.Count;

        [ClientRpc]
        void LayLances_ClientRpc(int intensity) => LayLances(intensity);

        void LayLances(int intensity)
        {
            ClearLances();
            if (!lancePrefab) return;
            Vector3 centre = arenaCell ? arenaCell.transform.position : Vector3.zero;
            CleaveLances.Lay(intensity, centre, _lanceParts);

            // Decide every part before laying any, so a lance never skips its own neighbour. A part is laid only where
            // its bounding sphere clears the bounding sphere of every prism already standing (the arena, identical on
            // every peer at the countdown). Conservative: a part is sometimes skipped where it would only have come
            // close. Two cheaper tests were tried and missed the arena (measured on I1, 2026-11-25): the prism index
            // had not filed it yet, and Physics.CheckBox found no colliders (arena colliders switch on near a vessel).
            var occupied = new Dictionary<Vector3Int, List<Vector4>>();
            float largest = 0f;
            foreach (var prism in FindObjectsByType<Prism>(FindObjectsSortMode.None))
            {
                if (!prism || !prism.gameObject.activeInHierarchy) continue;
                var t = prism.transform;
                float r = t.lossyScale.magnitude * 0.5f;
                // An arena prism is at most ~20 u across its diagonal; anything far bigger is not one.
                if (r > LanceHashCell) continue;
                largest = Mathf.Max(largest, r);
                var key = Vector3Int.FloorToInt(t.position / LanceHashCell);
                if (!occupied.TryGetValue(key, out var list)) occupied[key] = list = new List<Vector4>();
                list.Add(new Vector4(t.position.x, t.position.y, t.position.z, r));
            }
            int reach = Mathf.CeilToInt((largest + CleaveLances.PartRadius + CleaveLances.ArenaMargin) / LanceHashCell);
            _lanceKeep.Clear();
            for (int i = 0; i < _lanceParts.Count; i++)
                _lanceKeep.Add(!Touches(_lanceParts[i].Position, occupied, reach));

            _lanceRoot = new GameObject("CleaveLances").transform;
            var trail = new Trail();
            for (int i = 0; i < _lanceParts.Count; i++)
            {
                if (!_lanceKeep[i]) continue;
                var part = _lanceParts[i];
                var rotation = Quaternion.LookRotation(part.Forward, part.Up);
                var prism = PrismTrailBuilder.LayOne(lancePrefab,
                    new PrismLay(new SpawnPoint(part.Position, rotation, part.Scale), part.Domain, part.Kind),
                    _lanceRoot, trail, LanceOwnerId, admitAuthoredScale: true);
                if (prism) _lancePrisms.Add(prism);
            }
        }

        const float LanceHashCell = 64f;

        static bool Touches(Vector3 position, Dictionary<Vector3Int, List<Vector4>> occupied, int reach)
        {
            var key = Vector3Int.FloorToInt(position / LanceHashCell);
            for (int x = -reach; x <= reach; x++)
            for (int y = -reach; y <= reach; y++)
            for (int z = -reach; z <= reach; z++)
            {
                if (!occupied.TryGetValue(key + new Vector3Int(x, y, z), out var list)) continue;
                for (int i = 0; i < list.Count; i++)
                {
                    var o = list[i];
                    float clear = o.w + CleaveLances.PartRadius + CleaveLances.ArenaMargin;
                    if ((new Vector3(o.x, o.y, o.z) - position).sqrMagnitude < clear * clear) return true;
                }
            }
            return false;
        }

        void ClearLances()
        {
            for (int i = 0; i < _lancePrisms.Count; i++)
            {
                var prism = _lancePrisms[i];
                if (prism && prism.ownerID == LanceOwnerId && !EnvironmentPrismPool.TryRelease(prism))
                    Destroy(prism.gameObject);
            }
            _lancePrisms.Clear();
            _lanceParts.Clear();
            _lanceKeep.Clear();
            if (_lanceRoot) Destroy(_lanceRoot.gameObject);
            _lanceRoot = null;
        }
    }
}
