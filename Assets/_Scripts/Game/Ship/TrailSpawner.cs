using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CosmicShore.Core;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CosmicShore.Game
{
    [RequireComponent(typeof(ShipStatus))]
    public class TrailSpawner : MonoBehaviour
    {
        public delegate void BlockCreationHandler(float xShift, float wavelength, float scaleX, float scaleY, float scaleZ);
        public event BlockCreationHandler OnBlockCreated;

        const string playerNamePropertyKey = "playerName";

        [Header("References")]
        [SerializeField] TrailBlock trailBlock;
        [SerializeField] Skimmer skimmer;

        [Header("Wave Settings")]
        [SerializeField] float initialWavelength = 4f;
        [SerializeField] float minWavelength = 1f;
        [SerializeField] float defaultWaitTime = 0.5f;
        float wavelength;

        [Header("Block Scaling")]
        [SerializeField] int MaxNearbyBlockCount = 10;
        [SerializeField] float minBlockScale = 1f;
        [SerializeField] float maxBlockScale = 1f;

        [Header("Runtime Toggles")]
        [SerializeField] bool waitTillOutsideSkimmer = true;
        [SerializeField] bool shielded = false;

        [Header("Gap Settings")]
        public float offset;
        public float Gap;
        public float MinimumGap = 1f;
        public Vector3 TargetScale;

        [Header("Spawner Control")]
        [SerializeField] bool spawnerEnabled = true;
        float waitTime;
        [SerializeField] float startDelay = 2.1f;
        ushort spawnedTrailCount;

        // Trails
        public Trail Trail = new Trail();
        readonly Trail Trail2 = new Trail();

        IShipStatus _shipStatus;
        string ownerId;

        // Scaling helpers
        float Xscale;
        public float XScaler = 1f;
        public float YScaler = 1f;
        public float ZScaler = 1f;

        // Charm
        bool isCharmed;
        IShipStatus tempShip;

        // Cancellation
        CancellationTokenSource cts;

        // Static container
        public static GameObject TrailContainer;

        // Properties
        public float MinWaveLength => minWavelength;
        public ushort TrailLength => (ushort)Trail.TrailList.Count;
        public bool SpawnerEnabled => spawnerEnabled;
        public float TrailZScale => trailBlock.transform.localScale.z;

        private void Awake()
        {
            // Ensure ownerId is never null: default to empty
            ownerId = string.Empty;
        }

        private void OnEnable()
        {
            cts = new CancellationTokenSource();
            GameManager.OnGameOver += RestartAITrailSpawnerAfterDelay;
        }

        private void OnDisable()
        {
            cts.Cancel();
            GameManager.OnGameOver -= RestartAITrailSpawnerAfterDelay;
        }

        /// <summary>
        /// Initializes and starts spawning.
        /// </summary>
        public void Initialize(IShipStatus shipStatus)
        {
            _shipStatus = shipStatus;

            // this.ship = ship;
            waitTime = defaultWaitTime;
            wavelength = initialWavelength;
            ownerId = _shipStatus.Player.PlayerUUID;
            XScaler = minBlockScale;

            EnsureContainer();
            spawnerEnabled = true;
            spawnedTrailCount = 0;

            // Start spawn loop
            _ = SpawnLoopAsync(cts.Token);
        }

        /// <summary>Toggle between normal and extended wait time.</summary>
        public void ToggleBlockWaitTime(bool extended)
        {
            waitTime = extended ? defaultWaitTime * 3f : defaultWaitTime;
        }

        /// <summary>Pause spawning until restarted.</summary>
        public void PauseTrailSpawner() => spawnerEnabled = false;

        /// <summary>Force restart after default delay.</summary>
        public void ForceStartSpawningTrail()
        {
            spawnerEnabled = true;
            _ = SpawnLoopAsync(cts.Token);
        }

        /// <summary>Restart spawner after delay (player & AI).</summary>
        public void RestartTrailSpawnerAfterDelay(float delay)
        {
            _ = RestartAsync(delay, cts.Token);
        }

        // Game over restart for AI only
        void RestartAITrailSpawnerAfterDelay()
        {
            if (!CompareTag("Player_Ship"))
                _ = RestartAsync(waitTime, cts.Token);
        }

        /// <summary>Temporarily charm this spawner.</summary>
        public void Charm(IShipStatus other, float duration)
        {
            tempShip = other;
            isCharmed = true;
            _ = CharmAsync(duration, cts.Token);
        }

        /// <summary>Assign normalized X-scale (0..1).</summary>
        public void SetNormalizedXScale(float normalized)
        {
            if (Mathf.Approximately(Xscale, normalized)) return;
            Xscale = Mathf.Min(normalized, 1f);
            float newScale = Mathf.Max(minBlockScale, maxBlockScale * Xscale);
            // start lerp via UniTask
            _ = LerpXScalerAsync(XScaler, newScale, 1.5f, cts.Token);
        }

        /// <summary>Adjust Z-scale and wavelength based on dot product.</summary>
        public void SetDotProduct(float amount)
        {
            ZScaler = Mathf.Max(minBlockScale, maxBlockScale * (1f - Mathf.Abs(amount)));
            wavelength = Mathf.Max(minWavelength, initialWavelength * Mathf.Abs(amount));
        }

        /// <summary>Main spawn loop using UniTask.</summary>
        async UniTaskVoid SpawnLoopAsync(CancellationToken ct)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(startDelay), cancellationToken: ct);

            while (!ct.IsCancellationRequested)
            {
                if (spawnerEnabled && !_shipStatus.Attached && _shipStatus.Speed > 3f)
                {
                    if (Mathf.Approximately(Gap, 0f))
                    {
                        CreateBlock(0f, Trail);
                    }
                    else
                    {
                        CreateBlock(Gap * 0.5f, Trail);
                        CreateBlock(-Gap * 0.5f, Trail2);
                    }
                }

                // Ensure no NaN or Infinity for delay
                float raw = _shipStatus.Speed > 0f ? wavelength / _shipStatus.Speed : defaultWaitTime;
                float clamped = float.IsNaN(raw) || float.IsInfinity(raw)
                    ? defaultWaitTime
                    : Mathf.Clamp(raw, 0f, 3f);

                await UniTask.Delay(TimeSpan.FromSeconds(clamped), cancellationToken: ct);
            }
        }

        /// <summary>Restart after delay.</summary>
        async UniTaskVoid RestartAsync(float delay, CancellationToken ct)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: ct);
            spawnerEnabled = true;
        }

        /// <summary>Charm duration handling.</summary>
        async UniTaskVoid CharmAsync(float duration, CancellationToken ct)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(duration), cancellationToken: ct);
            isCharmed = false;
        }

        /// <summary>Lerp XScaler over time using UniTask.</summary>
        async UniTask LerpXScalerAsync(float from, float to, float duration, CancellationToken ct)
        {
            float elapsed = 0f;
            while (elapsed < duration && !ct.IsCancellationRequested)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                XScaler = Mathf.Lerp(from, to, t);
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
            XScaler = to;
        }

        /// <summary>Creates a block at offset.</summary>
        void CreateBlock(float halfGap, Trail trail)
        {
            var block = Instantiate(trailBlock);
            EnsureContainer();

            // scale
            Vector3 baseScale = trailBlock.transform.localScale;
            var scale = new Vector3(
                baseScale.x * XScaler / 2f - Mathf.Abs(halfGap),
                baseScale.y * YScaler,
                baseScale.z * ZScaler
            );
            block.TargetScale = scale;

            // position & rotation
            float xShift = (scale.x / 2f + Mathf.Abs(halfGap)) * Mathf.Sign(halfGap);
            Vector3 pos = transform.position - _shipStatus.Course * offset + _shipStatus.ShipTransform.right * xShift;
            block.transform.SetPositionAndRotation(pos, _shipStatus.blockRotation);
            block.transform.parent = TrailContainer.transform;

            // owner & player
            bool charm = isCharmed && tempShip != null;
            string creatorId = charm ? _shipStatus.Player.PlayerUUID : ownerId;
            if (string.IsNullOrEmpty(creatorId))
                creatorId = _shipStatus.Player?.PlayerUUID ?? string.Empty;
            block.ownerID = creatorId;
            block.PlayerName = charm ? _shipStatus.PlayerName : _shipStatus.Team.ToString();
            block.ChangeTeam(charm ? _shipStatus.Team : _shipStatus.Team);

            // waitTime
            block.waitTime = waitTillOutsideSkimmer
                ? (skimmer.transform.localScale.z + TrailZScale) / _shipStatus.Speed
                : waitTime;

            // shield
            if (shielded)
                block.TrailBlockProperties.IsShielded = true;

            // add to trail
            trail.Add(block);
            block.TrailBlockProperties.Index = (ushort)trail.TrailList.IndexOf(block);

            // event
            OnBlockCreated?.Invoke(xShift, wavelength, scale.x, scale.y, scale.z);
            spawnedTrailCount++;
        }

        public List<TrailBlock> GetLastTwoBlocks()
        {
            if (Trail2.TrailList.Count > 0)
                return new List<TrailBlock>
                {
                    Trail.TrailList[^1],
                    Trail2.TrailList[^1]
                };
            return null;
        }

        void EnsureContainer()
        {
            if (TrailContainer == null)
                TrailContainer = new GameObject("TrailContainer");
        }

        public static void NukeTheTrails()
        {
            if (TrailContainer == null) return;
            foreach (Transform child in TrailContainer.transform)
                Destroy(child.gameObject);
        }
    }
}
