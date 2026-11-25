using System.Collections.Generic;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Spearline's race garden in the world (SPEARLINE.md §2b): every peer lays <see cref="SpearlineGarden"/>'s fuel
    /// hedges and pillars from the broadcast rings, in Blue (no pilot's domain, so every pilot meets them alike), once a
    /// course. A cut prism grows back <see cref="gardenRegrowSeconds"/> after it went, once no hull is near it, so the
    /// pack behind the leader still finds fuel and the pillars still stand. Nothing here reads or writes a vessel.
    /// </summary>
    public partial class SpearlineController
    {
        const string GardenOwnerId = "SpearlineGarden";

        [Header("Race garden (every rung)")]
        [Tooltip("A cut hedge or pillar prism grows back this long (s) after it went...")]
        [SerializeField, Min(0f)] float gardenRegrowSeconds = 4f;

        [Tooltip("...once no hull is within this distance (u) of it.")]
        [SerializeField, Min(0f)] float gardenRegrowClearance = 80f;

        [Tooltip("Garden prisms grown back a frame, at most.")]
        [SerializeField, Min(1)] int gardenRegrowPerFrame = 20;

        readonly List<SpearlineGardenPart> _gardenParts = new();
        readonly List<Prism> _gardenPrisms = new();
        readonly List<float> _gardenGoneAt = new();
        Transform _gardenRoot;
        int _gardenCursor;

        void LayGarden()
        {
            _gardenParts.Clear();
            SpearlineGarden.Lay(_course, SpearlineSeals.SealedRings(_course, Settings),
                                SpearlineGarden.ForIntensity(Intensity), _gardenParts);
            if (_gardenParts.Count == 0) return;
            _gardenRoot = new GameObject("SpearlineGarden").transform;
            for (int i = 0; i < _gardenParts.Count; i++)
            {
                _gardenPrisms.Add(LayGardenPrism(i, forReveal: true));
                _gardenGoneAt.Add(-1f);
            }
        }

        Prism LayGardenPrism(int i, bool forReveal)
        {
            var part = _gardenParts[i];
            return PrismTrailBuilder.LayOne(sealPrefab,
                new PrismLay(new SpawnPoint(part.Position, Quaternion.LookRotation(part.Forward, part.Up), part.Scale),
                             Domains.Blue, part.Kind),
                _gardenRoot, new Trail(), GardenOwnerId, admitAuthoredScale: true, watchForReveal: forReveal);
        }

        static bool GardenHolds(Prism prism) => prism && !prism.destroyed && prism.ownerID == GardenOwnerId;

        /// <summary>Every frame: grow back a few cut garden prisms whose time is up and that no hull is near.</summary>
        void TickGarden()
        {
            int count = _gardenPrisms.Count;
            if (count == 0 || !sealPrefab) return;
            float now = Time.time;
            int budget = gardenRegrowPerFrame;
            float clear = gardenRegrowClearance * gardenRegrowClearance;
            var players = gameData.Players;
            for (int n = 0; n < count && budget > 0; n++)
            {
                int i = (_gardenCursor + n) % count;
                if (GardenHolds(_gardenPrisms[i])) { _gardenGoneAt[i] = -1f; continue; }
                if (_gardenGoneAt[i] < 0f) { _gardenGoneAt[i] = now; continue; }
                if (now - _gardenGoneAt[i] < gardenRegrowSeconds) continue;
                bool near = false;
                Vector3 at = _gardenParts[i].Position;
                for (int p = 0; p < players.Count && !near; p++)
                {
                    var tf = players[p]?.Vessel?.Transform;
                    near = tf && (tf.position - at).sqrMagnitude < clear;
                }
                if (near) continue;
                if (_gardenPrisms[i] && _gardenPrisms[i].ownerID == GardenOwnerId) Release(_gardenPrisms[i]);
                _gardenPrisms[i] = LayGardenPrism(i, forReveal: false);
                _gardenGoneAt[i] = -1f;
                budget--;
            }
            _gardenCursor = (_gardenCursor + 1) % count;
        }

        void ClearGarden()
        {
            for (int i = 0; i < _gardenPrisms.Count; i++)
                if (_gardenPrisms[i] && _gardenPrisms[i].ownerID == GardenOwnerId) Release(_gardenPrisms[i]);
            _gardenPrisms.Clear();
            _gardenParts.Clear();
            _gardenGoneAt.Clear();
            _gardenCursor = 0;
            if (_gardenRoot) Destroy(_gardenRoot.gameObject);
            _gardenRoot = null;
        }
    }
}
