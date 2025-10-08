using System;
using System.Collections.Generic;
using System.Threading;
using CosmicShore.Core;
using CosmicShore.Utilities;
using Cysharp.Threading.Tasks;
using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.Game
{
    public class VesselPrismController : MonoBehaviour
    {
        public delegate void BlockCreationHandler(float xShift, float wavelength, float scaleX, float scaleY, float scaleZ);
        public event BlockCreationHandler OnBlockCreated;

        const string playerNamePropertyKey = "playerName";

        [SerializeField] private PrismEventChannelWithReturnSO _onPrismSpawnedEventChannel;

        [Header("References")]
        [SerializeField] Skimmer skimmer;

        [Header("Base Scale (used instead of prefab)")]
        [SerializeField] private Vector3 BaseScale = new Vector3(10f, 5f, 5f);

        [SerializeField] private PrismType prismType;

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

        // Trails
        public Trail Trail = new Trail();
        readonly Trail Trail2 = new Trail();

        IVesselStatus vesselStatus;
        string ownerId;

        // Scaling helpers
        float Xscale;
        public float XScaler = 1f;
        public float YScaler = 1f;
        public float ZScaler = 1f;

        // Charm
        bool isCharmed;
        IVesselStatus tempVessel;

        // Cancellation
        CancellationTokenSource cts;

        // Properties
        public float MinWaveLength => minWavelength;
        public ushort TrailLength => (ushort)Trail.TrailList.Count;
        public float TrailZScale => BaseScale.z; // <- from BaseScale now
        public event Action<Prism> OnBlockSpawned;

        private void OnDisable()
        {
            StopSpawn();
            ClearTrails();
        }

        /// <summary>Initializes and starts spawning.</summary>
        public void Initialize(IVesselStatus vesselStatus)
        {
            this.vesselStatus = vesselStatus;

            waitTime = defaultWaitTime;
            wavelength = initialWavelength;
            ownerId = this.vesselStatus.Player.PlayerUUID;
            XScaler = minBlockScale;
        }

        public void StartSpawn()
        {
            if (cts != null)
                StopSpawn();
            
            cts = new CancellationTokenSource();
            spawnerEnabled = true;
            
            _ = SpawnLoopAsync(cts.Token);
        }
        
        /// <summary>
        /// Stops ALL ongoing async operations (spawn loop, delayed restarts, lerps, charms)
        /// and disables further spawning until re-initialized or a new CTS is created.
        /// </summary>
        public void StopSpawn()
        {
            spawnerEnabled = false;

            if (cts != null)
            {
                cts.Cancel();
                cts.Dispose();
                cts = null;
            }
        }

        public void ToggleBlockWaitTime(bool extended)
        {
            waitTime = extended ? defaultWaitTime * 3f : defaultWaitTime;
        }

        public void PauseTrailSpawner() => spawnerEnabled = false;

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

        public void Charm(IVesselStatus other, float duration)
        {
            tempVessel = other;
            isCharmed = true;
            _ = CharmAsync(duration, cts.Token);
        }

        public void SetNormalizedXScale(float normalized)
        {
            if (Mathf.Approximately(Xscale, normalized)) return;
            Xscale = Mathf.Min(normalized, 1f);
            float newScale = Mathf.Max(minBlockScale, maxBlockScale * Xscale);
            
            if (cts != null)
            _ = LerpXScalerAsync(XScaler, newScale, 1.5f, cts.Token);
        }

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
                if (spawnerEnabled && !vesselStatus.Attached && vesselStatus.Speed > 3f)
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

                float raw = vesselStatus.Speed > 0f ? wavelength / vesselStatus.Speed : defaultWaitTime;
                float clamped = float.IsNaN(raw) || float.IsInfinity(raw)
                    ? defaultWaitTime
                    : Mathf.Clamp(raw, 0f, 3f);

                await UniTask.Delay(TimeSpan.FromSeconds(clamped), cancellationToken: ct);
            }
        }

        async UniTaskVoid RestartAsync(float delay, CancellationToken ct)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(delay), cancellationToken: ct);
            spawnerEnabled = true;
        }

        async UniTaskVoid CharmAsync(float duration, CancellationToken ct)
        {
            await UniTask.Delay(TimeSpan.FromSeconds(duration), cancellationToken: ct);
            isCharmed = false;
        }

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

        /// <summary>Creates a block at offset using PrismFactory via event channel.</summary>
        void CreateBlock(float halfGap, Trail trail)
        {
            if (_onPrismSpawnedEventChannel == null)
            {
                Debug.LogError("[PrismSpawner] Prism spawn event channel is not assigned.");
                return;
            }

            // --- Compute scale from BaseScale ---
            var scale = new Vector3(
                BaseScale.x * XScaler / 2f - Mathf.Abs(halfGap),
                BaseScale.y * YScaler,
                BaseScale.z * ZScaler
            );

            // --- Position & Rotation ---
            float xShift = (scale.x / 2f + Mathf.Abs(halfGap)) * Mathf.Sign(halfGap);
            Vector3 pos = transform.position - vesselStatus.Course * offset + vesselStatus.ShipTransform.right * xShift;
            Quaternion rot = vesselStatus.blockRotation;

            // --- Ask factory to spawn Interactive prism (pooled) ---
            var ret = _onPrismSpawnedEventChannel.RaiseEvent(new PrismEventData
            {
                ownDomain     = vesselStatus.Domain,
                Rotation      = rot,
                SpawnPosition = pos,
                Scale         = scale,
                PrismType     = prismType
            });

            if (!ret.SpawnedObject || !ret.SpawnedObject.TryGetComponent(out Prism prism))
            {
                Debug.LogError("[PrismSpawner] Factory returned null or missing Prism component.");
                return;
            }

            // Target scale (also sent in event; set here for gameplay logic)
            prism.TargetScale = scale;

            // Owner / charm
            bool charm = isCharmed && tempVessel != null;
            string creatorId = charm ? vesselStatus.Player.PlayerUUID : ownerId;
            if (string.IsNullOrEmpty(creatorId))
                creatorId = vesselStatus.Player?.PlayerUUID ?? string.Empty;
            prism.ownerID = creatorId;

            // Team
            prism.ChangeTeam(vesselStatus.Domain);

            // Wait time (uses TrailZScale from BaseScale)
            prism.waitTime = waitTillOutsideSkimmer
                ? (skimmer.transform.localScale.z + TrailZScale) / vesselStatus.Speed
                : waitTime;

            // Shield
            if (shielded)
                prism.prismProperties.IsShielded = true;

            // Add to trail & initialize
            trail.Add(prism);
            prism.prismProperties.Index = (ushort)trail.TrailList.IndexOf(prism);
            prism.Initialize(vesselStatus.PlayerName);

            // Events
            OnBlockCreated?.Invoke(xShift, wavelength, scale.x, scale.y, scale.z);
            OnBlockSpawned?.Invoke(prism);
        }

        void CreateBlockWithGrow(float halfGap, Trail prisms)
        {
            if (_onPrismSpawnedEventChannel == null)
            {
                Debug.LogError("[PrismSpawner] Prism spawn event channel is not assigned.");
                return;
            }

            // --- Scale from BaseScale ---
            Vector3 scale = new Vector3(
                BaseScale.x * XScaler / 2f - Mathf.Abs(halfGap),
                BaseScale.y * YScaler,
                BaseScale.z * ZScaler
            );

            // --- Position & Rotation ---
            float xShift = (scale.x / 2f + Mathf.Abs(halfGap)) * Mathf.Sign(halfGap);
            Vector3 pos = transform.position - vesselStatus.Course * offset + vesselStatus.ShipTransform.right * xShift;
            Quaternion rot = vesselStatus.blockRotation;

            // --- When grow completes, swap to Interactive prism via factory ---
            void OnGrowCompleted()
            {
                var ret2 = _onPrismSpawnedEventChannel.RaiseEvent(new PrismEventData
                {
                    ownDomain     = vesselStatus.Domain,
                    Rotation      = rot,
                    SpawnPosition = pos,
                    Scale         = scale,
                    PrismType     = prismType
                });

                if (!ret2.SpawnedObject || !ret2.SpawnedObject.TryGetComponent(out Prism prism))
                {
                    Debug.LogError("[PrismSpawner] Grow completion spawn missing Prism component.");
                    return;
                }

                prism.TargetScale = scale;

                bool charm = isCharmed && tempVessel != null;
                string creatorId = charm ? vesselStatus.Player.PlayerUUID : ownerId;
                if (string.IsNullOrEmpty(creatorId)) creatorId = vesselStatus.Player?.PlayerUUID ?? string.Empty;

                prism.ownerID = creatorId;
                prism.ChangeTeam(vesselStatus.Domain);

                prism.waitTime = waitTillOutsideSkimmer
                    ? (skimmer.transform.localScale.z + TrailZScale) / vesselStatus.Speed
                    : waitTime;

                if (shielded) prism.prismProperties.IsShielded = true;

                prisms.Add(prism);
                prism.prismProperties.Index = (ushort)prisms.TrailList.IndexOf(prism);
                prism.Initialize(vesselStatus.PlayerName);

                OnBlockCreated?.Invoke(xShift, wavelength, scale.x, scale.y, scale.z);
                OnBlockSpawned?.Invoke(prism);
            }

            // --- Raise Grow prism (dummy visual first) ---
            var eventData = new PrismEventData
            {
                ownDomain       = vesselStatus.Domain,
                Rotation        = rot,
                SpawnPosition   = pos,
                Scale           = scale,
                TargetTransform = transform, // current jet transform
                PrismType       = PrismType.Grow,
                OnGrowCompleted = OnGrowCompleted
            };

            _onPrismSpawnedEventChannel.RaiseEvent(eventData);
        }

        public List<Prism> GetLastTwoBlocks()
        {
            if (Trail2.TrailList.Count > 0)
                return new List<Prism> { Trail.TrailList[^1], Trail2.TrailList[^1] };
            return null;
        }

        public void ClearTrails()
        {
            Trail.Clear();
            Trail2.Clear();
        }
    }
}
