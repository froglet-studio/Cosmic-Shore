using System.Linq;
using System.Threading;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Friction's Intensity-4 hazard: periodic telegraphed lightning strikes within the
    /// arena. A direct hit is an immediate elimination — it bypasses RoundStats.Lives
    /// entirely (unlike a normal hunter hit), matching the design doc's "direct hit
    /// results in immediate elimination" for the extreme storm level.
    ///
    /// Server-authoritative: the server picks strike points and applies elimination;
    /// all clients receive the telegraph/strike VFX via ClientRpc for feedback.
    /// </summary>
    public class FrictionLightningStrikeController : NetworkBehaviour
    {
        [Header("Arena")]
        [Tooltip("Strike points are sampled randomly within this collider's bounds.")]
        [SerializeField] private Collider arenaBounds;

        [Header("Timing")]
        [SerializeField] private Vector2 strikeIntervalSecondsRange = new Vector2(4f, 8f);
        [SerializeField] private float telegraphSeconds = 1.5f;

        [Header("Strike")]
        [SerializeField] private float strikeRadius = 15f;
        [SerializeField] private LayerMask vesselLayerMask = ~0;

        [Header("VFX")]
        [SerializeField] private GameObject telegraphVfxPrefab;
        [SerializeField] private GameObject strikeVfxPrefab;

        [Header("Game Data")]
        [SerializeField] private GameDataSO gameData;

        private CancellationTokenSource _cts;
        private bool _active;

        public void Activate()
        {
            if (_active) return;
            _active = true;

            if (IsServer)
            {
                _cts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
                StrikeLoopAsync(_cts.Token).Forget();
            }
        }

        public void Deactivate()
        {
            _active = false;
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        public override void OnNetworkDespawn()
        {
            Deactivate();
            base.OnNetworkDespawn();
        }

        private async UniTaskVoid StrikeLoopAsync(CancellationToken ct)
        {
            try
            {
                while (_active)
                {
                    float wait = Random.Range(strikeIntervalSecondsRange.x, strikeIntervalSecondsRange.y);
                    await UniTask.Delay((int)(wait * 1000), DelayType.UnscaledDeltaTime, cancellationToken: ct);

                    Vector3 strikePos = SampleArenaPoint();

                    TelegraphStrike_ClientRpc(strikePos);
                    await UniTask.Delay((int)(telegraphSeconds * 1000), DelayType.UnscaledDeltaTime, cancellationToken: ct);

                    ExecuteStrike(strikePos);
                    ExecuteStrikeVfx_ClientRpc(strikePos);
                }
            }
            catch (System.OperationCanceledException)
            {
                // expected on Deactivate/despawn
            }
        }

        private Vector3 SampleArenaPoint()
        {
            if (!arenaBounds) return transform.position;

            var b = arenaBounds.bounds;
            return new Vector3(
                Random.Range(b.min.x, b.max.x),
                Random.Range(b.min.y, b.max.y),
                Random.Range(b.min.z, b.max.z));
        }

        private void ExecuteStrike(Vector3 strikePos)
        {
            var hits = Physics.OverlapSphere(strikePos, strikeRadius, vesselLayerMask);
            foreach (var hit in hits)
            {
                var vesselImpactor = hit.GetComponentInParent<VesselImpactor>();
                if (vesselImpactor == null || vesselImpactor.Vessel == null) continue;

                // Hunters are immune to their own storm.
                if (vesselImpactor.Vessel.Transform.GetComponent<FrictionHunterTag>() != null) continue;

                var victimStats = gameData.RoundStatsList
                    .FirstOrDefault(s => s.Name == vesselImpactor.Vessel.VesselStatus.PlayerName);
                if (victimStats == null || victimStats.IsEliminated) continue;

                victimStats.Lives = 0;
                victimStats.IsEliminated = true;
            }
        }

        [ClientRpc]
        private void TelegraphStrike_ClientRpc(Vector3 position)
        {
            if (telegraphVfxPrefab)
                Instantiate(telegraphVfxPrefab, position, Quaternion.identity);
        }

        [ClientRpc]
        private void ExecuteStrikeVfx_ClientRpc(Vector3 position)
        {
            if (strikeVfxPrefab)
                Instantiate(strikeVfxPrefab, position, Quaternion.identity);
        }
    }
}
