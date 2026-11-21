using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A vault at a vault field (Docs/Voyages/SLICE.md §1.6): a shell of super-shielded prisms with an artifact sealed
    /// inside. Super-shields fall only to a payload's crack (ARTIFACTS.md §2), so a vault is opened by bringing a payload
    /// from the Ark and releasing it on the shell. Once enough of the shell is cracked the artifact floats free
    /// (<see cref="ArtifactPickup"/>) for anyone to grab and stow.
    ///
    /// <para>The shell is neutral mass (the Blue sentinel, nobody's domain), so every squad's payload can crack it. Its
    /// prisms come from the environment pool and go back to it when the Ark sails on.</para>
    /// </summary>
    public sealed class VoyageVault : MonoBehaviour
    {
        const string OwnerId = "VoyageVault";
        const float CheckSeconds = 0.5f;

        readonly List<Prism> _shell = new();
        string _artifactId;
        float _openShare;
        float _nextCheck;
        bool _open;

        public bool IsOpen => _open;

        /// <summary>Stand a vault at <paramref name="centre"/> holding <paramref name="artifactId"/>.</summary>
        public static VoyageVault Create(Transform parent, Vector3 centre, string artifactId, Prism prismPrefab,
            float radius, int prisms, Vector3 prismScale, float openShare)
        {
            var go = new GameObject("VoyageVault");
            go.transform.SetParent(parent, false);
            go.transform.position = centre;
            var vault = go.AddComponent<VoyageVault>();
            vault._artifactId = artifactId;
            vault._openShare = openShare;
            vault.Lay(prismPrefab, radius, prisms, prismScale);
            return vault;
        }

        void Lay(Prism prefab, float radius, int count, Vector3 scale)
        {
            if (!prefab)
            {
                CSDebug.LogWarning("[Voyage] No prism prefab for a vault shell - the artifact floats free.");
                Open();
                return;
            }
            var trail = new Trail();
            for (int i = 0; i < count; i++)
            {
                var dir = ArkFall.SpillOffset(i, count, 1f);
                if (dir.sqrMagnitude < 1e-4f) dir = Vector3.up;
                var pose = new SpawnPoint(dir.normalized * radius, Quaternion.LookRotation(dir.normalized), scale);
                var prism = PrismTrailBuilder.LayOne(prefab, new PrismLay(pose, Domains.Blue), transform, trail, OwnerId,
                    watchForReveal: false);
                if (!prism) continue;
                prism.ActivateSuperShield();
                _shell.Add(prism);
            }
        }

        void Update()
        {
            if (_open || Time.time < _nextCheck) return;
            _nextCheck = Time.time + CheckSeconds;
            int cracked = 0;
            for (int i = 0; i < _shell.Count; i++)
            {
                var p = _shell[i];
                if (!p || p.destroyed || p.prismProperties is not { IsSuperShielded: true }) cracked++;
            }
            if (_shell.Count == 0 || cracked >= Mathf.CeilToInt(_shell.Count * _openShare)) Open();
        }

        void Open()
        {
            if (_open) return;
            _open = true;
            ArtifactPickup.Spill(_artifactId, transform.position);
        }

        /// <summary>The Ark sails on: the shell goes back to the pool.</summary>
        public void Retire()
        {
            for (int i = 0; i < _shell.Count; i++)
            {
                var p = _shell[i];
                if (!p || p.destroyed) continue;
                p.transform.SetParent(null, true);
                if (!EnvironmentPrismPool.TryRelease(p)) Destroy(p.gameObject);
            }
            _shell.Clear();
            Destroy(gameObject);
        }
    }
}
