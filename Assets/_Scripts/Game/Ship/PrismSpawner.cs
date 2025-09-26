using System;
using System.Collections.Generic;
using System.Threading;
using CosmicShore.Core;
using CosmicShore.Utilities;
using Cysharp.Threading.Tasks;
using Obvious.Soap;
using UnityEngine;
using UnityEngine.Serialization;

namespace CosmicShore.Game
{
    public class PrismSpawner : MonoBehaviour
    {
        public delegate void BlockCreationHandler(float xShift, float wavelength, float scaleX, float scaleY, float scaleZ);
        public event BlockCreationHandler OnBlockCreated;

        const string playerNamePropertyKey = "playerName";

        [SerializeField] private PrismEventChannelWithReturnSO _onPrismSpawnedEventChannel;
        
        [Header("References")]
        [SerializeField] Prism prismPrefab;
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

        // Static container
        public static GameObject TrailContainer;

        // Properties
        public float MinWaveLength => minWavelength;
        public ushort TrailLength => (ushort)Trail.TrailList.Count;
        public float TrailZScale => prismPrefab.transform.localScale.z;
        public event Action<Prism> OnBlockSpawned;

        private void Awake()
        {
            // Ensure ownerId is never null: default to empty
            ownerId = string.Empty;
        }

        private void OnEnable()
        {
            cts = new CancellationTokenSource();
        }

        private void OnDisable()
        {
            cts.Cancel();
        }

        /// <summary>
        /// Initializes and starts spawning.
        /// </summary>
        public void Initialize(IVesselStatus vesselStatus)
        {
            this.vesselStatus = vesselStatus;

            // this.vessel = vessel;
            waitTime = defaultWaitTime;
            wavelength = initialWavelength;
            ownerId = this.vesselStatus.Player.PlayerUUID;
            XScaler = minBlockScale;

            EnsureContainer();
            spawnerEnabled = true;

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
        public void Charm(IVesselStatus other, float duration)
        {
            tempVessel = other;
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

                // Ensure no NaN or Infinity for delay
                float raw = vesselStatus.Speed > 0f ? wavelength / vesselStatus.Speed : defaultWaitTime;
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
        void CreateBlock(float halfGap, Trail prisms)
        {
            var prism = Instantiate(prismPrefab);
            EnsureContainer();

            // scale
            Vector3 baseScale = prismPrefab.transform.localScale;
            var scale = new Vector3(
                baseScale.x * XScaler / 2f - Mathf.Abs(halfGap),
                baseScale.y * YScaler,
                baseScale.z * ZScaler
            );
            prism.TargetScale = scale;

            // position & rotation
            float xShift = (scale.x / 2f + Mathf.Abs(halfGap)) * Mathf.Sign(halfGap);
            Vector3 pos = transform.position - vesselStatus.Course * offset + vesselStatus.ShipTransform.right * xShift;
            prism.transform.SetPositionAndRotation(pos, vesselStatus.blockRotation);
            prism.transform.parent = TrailContainer.transform;

            // owner & player
            bool charm = isCharmed && tempVessel != null;
            string creatorId = charm ? vesselStatus.Player.PlayerUUID : ownerId;
            if (string.IsNullOrEmpty(creatorId))
                creatorId = vesselStatus.Player?.PlayerUUID ?? string.Empty;
            prism.ownerID = creatorId;

            prism.PlayerName = vesselStatus.PlayerName;
            prism.ChangeTeam(vesselStatus.Domain);
            // prism.PlayerName = charm ? VesselStatus.PlayerName : VesselStatus.Team.ToString();
            // prism.ChangeTeam(charm ? VesselStatus.Team : VesselStatus.Team);

            // waitTime
            prism.waitTime = waitTillOutsideSkimmer
                ? (skimmer.transform.localScale.z + TrailZScale) / vesselStatus.Speed
                : waitTime;

            // shield
            if (shielded)
                prism.prismProperties.IsShielded = true;

            // add to trail
            prisms.Add(prism);
            prism.prismProperties.Index = (ushort)prisms.TrailList.IndexOf(prism);

            // event
            OnBlockCreated?.Invoke(xShift, wavelength, scale.x, scale.y, scale.z);
            OnBlockSpawned?.Invoke(prism);
        }
        
        void CreateBlockWithGrow(float halfGap, Trail prisms)
        {
            EnsureContainer();

            // --- Scale ---
            Vector3 baseScale = prismPrefab.transform.localScale;
            Vector3 scale = new Vector3(
                baseScale.x * XScaler / 2f - Mathf.Abs(halfGap),
                baseScale.y * YScaler,
                baseScale.z * ZScaler
            );

            // --- Position & Rotation ---
            float xShift = (scale.x / 2f + Mathf.Abs(halfGap)) * Mathf.Sign(halfGap);
            Vector3 pos = transform.position
                          - vesselStatus.Course * offset
                          + vesselStatus.ShipTransform.right * xShift;
            Quaternion rot = vesselStatus.blockRotation;

            // --- Setup Grow Callback ---
            void OnGrowCompleted()
            {
                // Instantiate real TrailBlock
                Prism prism = Instantiate(prismPrefab, pos, rot, TrailContainer.transform);
                prism.TargetScale = scale;

                // --- Ownership & Team ---
                bool charm = isCharmed && tempVessel != null;
                string creatorId = charm ? vesselStatus.Player.PlayerUUID : ownerId;
                if (string.IsNullOrEmpty(creatorId)) creatorId = vesselStatus.Player?.PlayerUUID ?? string.Empty;

                prism.ownerID = creatorId;
                prism.PlayerName = vesselStatus.PlayerName;
                prism.ChangeTeam(vesselStatus.Domain);

                // --- Wait Time ---
                prism.waitTime = waitTillOutsideSkimmer
                    ? (skimmer.transform.localScale.z + TrailZScale) / vesselStatus.Speed
                    : waitTime;

                // --- Shield ---
                if (shielded) prism.prismProperties.IsShielded = true;

                // --- Add to Trail ---
                prisms.Add(prism);
                prism.prismProperties.Index = (ushort)prisms.TrailList.IndexOf(prism);

                // --- Events ---
                OnBlockCreated?.Invoke(xShift, wavelength, scale.x, scale.y, scale.z);
                OnBlockSpawned?.Invoke(prism);
            }

            // --- Raise Grow Prism Event (dummy visual effect first) ---
            var eventData = new PrismEventData
            {
                ownDomain = vesselStatus.Domain,
                Rotation = rot,
                SpawnPosition = pos,
                Scale = scale,
                TargetTransform = transform, // current jet transform
                PrismType = PrismType.Grow,  // Grow uses same shader reversed
                OnGrowCompleted = OnGrowCompleted // << callback injected here
            };

            _onPrismSpawnedEventChannel.RaiseEvent(eventData);
        }


        public List<Prism> GetLastTwoBlocks()
        {
            if (Trail2.TrailList.Count > 0)
                return new List<Prism>
                {
                    Trail.TrailList[^1],
                    Trail2.TrailList[^1]
                };
            return null;
        }

        void EnsureContainer()
        {
            if (!TrailContainer)
                TrailContainer = new GameObject("TrailContainer");
        }

        public static void NukeTheTrails()
        {
            if (!TrailContainer) return;
            foreach (Transform child in TrailContainer.transform)
                Destroy(child.gameObject);
        }
    }
}
