using System;
using System.Collections.Generic;
using System.Threading;
using CosmicShore.Core;
using CosmicShore.Core.Visuals;
using CosmicShore.Utilities;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CosmicShore.Game
{
    public class VesselPrismController : MonoBehaviour
    {
        public delegate void BlockCreationHandler(float xShift, float wavelength, float scaleX, float scaleY, float scaleZ);
        public event BlockCreationHandler OnBlockCreated;

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

        protected IVesselStatus vesselStatus;

        // Scaling helpers
        float Xscale;
        public float XScaler = 1f;
        public float YScaler = 1f;
        public float ZScaler = 1f;

        // Cancellation
        CancellationTokenSource cts;
        
        bool     _dangerMode;
        Material _dangerMaterial;
        float    _dangerBlendSeconds; 
        bool     _dangerAppend;  

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
        /// Stops ALL ongoing async operations (spawn loop, delayed restarts, lerps)
        /// and disables further spawning until re-initialized or a new CTS is created.
        /// </summary>
        public void StopSpawn()
        {
            spawnerEnabled = false;

            if (cts == null) 
                return;
            
            cts.Cancel();
            cts.Dispose();
            cts = null;
        }

        public void ToggleBlockWaitTime(bool extended)
        {
            waitTime = extended ? defaultWaitTime * 3f : defaultWaitTime;
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
                if (spawnerEnabled && !vesselStatus.IsAttached && vesselStatus.Speed > 3f)
                {
                    if (Mathf.Approximately(Gap, 0f))
                    {
                        CreateBlock(ApplyBoostGap(0f), Trail);
                    }
                    else
                    {
                        CreateBlock(ApplyBoostGap(Gap * 0.5f), Trail);
                        CreateBlock(ApplyBoostGap(-Gap * 0.5f), Trail2);
                    }
                }

                float raw = vesselStatus.Speed > 0f ? wavelength / vesselStatus.Speed : defaultWaitTime;
                float clamped = float.IsNaN(raw) || float.IsInfinity(raw)
                    ? defaultWaitTime
                    : Mathf.Clamp(raw, 0f, 3f);

                float finalDelay = ApplyBoostSpawnDelay(clamped);
                await UniTask.Delay(TimeSpan.FromSeconds(finalDelay), cancellationToken: ct);
            }
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
            if (!_onPrismSpawnedEventChannel)
            {
                Debug.LogError("[PrismSpawner] Prism spawn event channel is not assigned.");
                return;
            }

            // --- Compute scale from BaseScale ---
            Vector3 scale = ApplyBoostScale(new Vector3(
                BaseScale.x * XScaler / 2f - Mathf.Abs(halfGap),
                BaseScale.y * YScaler,
                BaseScale.z * ZScaler
            ));

            // --- Position & Rotation ---
            float xShift = halfGap == 0 ? 0 : (scale.x / 2f + Mathf.Abs(halfGap)) * Mathf.Sign(halfGap);
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

            prism.ownerID = vesselStatus.PlayerName;

            // Team
            prism.ChangeTeam(vesselStatus.Domain);

            // Wait time (uses TrailZScale from BaseScale)
            prism.waitTime = waitTillOutsideSkimmer
                ? (skimmer.transform.localScale.z + TrailZScale) / vesselStatus.Speed
                : waitTime;

            if (_dangerMode)
            {
                try { prism.prismProperties.IsDangerous = true; } catch { /* ignore */ }

                if (_dangerMaterial && prism.TryGetComponent<Renderer>(out var rend))
                {
                    if (_dangerBlendSeconds > 0f)
                        MaterialBlendUtility.BeginBlend(rend, _dangerMaterial, _dangerBlendSeconds, _dangerAppend);
                    else
                        rend.sharedMaterial = _dangerMaterial; 
                }
            }

            
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

        public List<Prism> GetLastTwoBlocks()
        {
            if (Trail2.TrailList.Count > 0)
                return new List<Prism> { Trail.TrailList[^1], Trail2.TrailList[^1] };
            return null;
        }
        
        public void EnableDangerMode(Material dangerMat, Vector3 scaleMult, float lerpSeconds = 0f,
            float blendSeconds = 0f, bool append = true)
        {
            _dangerMode         = true;
            _dangerMaterial     = dangerMat;
            _dangerBlendSeconds = blendSeconds;
            _dangerAppend       = append;
            LerpScaleMultipliers(scaleMult, lerpSeconds);
        }

        public void DisableDangerMode(float lerpSeconds = 0f)
        {
            _dangerMode         = false;
            _dangerMaterial     = null;
            _dangerBlendSeconds = 0f;
            _dangerAppend       = true;
            LerpScaleMultipliers(Vector3.one, lerpSeconds);
        }


        async void LerpScaleMultipliers(Vector3 targetMult, float seconds)
        {
            float t = 0f;
            float dur = Mathf.Max(0f, seconds);
            float sx0 = XScaler, sy0 = YScaler, sz0 = ZScaler;
            float sx1 = Mathf.Max(0.0001f, targetMult.x);
            float sy1 = Mathf.Max(0.0001f, targetMult.y);
            float sz1 = Mathf.Max(0.0001f, targetMult.z);

            if (dur <= 0f)
            {
                XScaler = sx1; YScaler = sy1; ZScaler = sz1;
                return;
            }

            var ct = this.GetCancellationTokenOnDestroy();
            while (t < dur && !ct.IsCancellationRequested)
            {
                t += Time.deltaTime;
                float a = Mathf.Clamp01(t / dur);
                XScaler = Mathf.Lerp(sx0, sx1, a);
                YScaler = Mathf.Lerp(sy0, sy1, a);
                ZScaler = Mathf.Lerp(sz0, sz1, a);
                await UniTask.Yield(PlayerLoopTiming.Update, ct);
            }
            XScaler = sx1; YScaler = sy1; ZScaler = sz1;
        }

        public void ClearTrails()
        {
            Trail.Clear();
            Trail2.Clear();
        }

        protected virtual Vector3 ApplyBoostScale(Vector3 scale)
        {
            return scale;
        }
        
        protected virtual float ApplyBoostGap(float halfGap)
        {
            return halfGap;
        }
        
        protected virtual float ApplyBoostSpawnDelay(float delay)
        {
            return delay;
        }
    }
}