using System;
using System.Collections.Generic;
using System.Threading;
using CosmicShore.Utility;
using CosmicShore.Gameplay;
using Cysharp.Threading.Tasks;
using UnityEngine;
using CosmicShore.ScriptableObjects;
using CosmicShore.Data;
using System.Linq;

namespace CosmicShore.Gameplay
{
    public class VesselPrismController : MonoBehaviour
    {
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

        [Header("Elemental (per-vessel, authored on the prefab)")]
        [Tooltip("MASS -> trail prism VOLUME multiplier (evaluated live each spawn). Authored as " +
                 "an ElementalFloat on the vessel prefab (the Squirrel maps it to Mass, 1 -> 2.5); " +
                 "applied as the cube root per axis so prism volume scales linearly with the level. " +
                 "Disabled (1x) on vessels that don't map Mass to their trail.")]
        [SerializeField] ElementalFloat trailVolume = new(1f);

        [Tooltip("MASS level-5 'Heavy Trail': when enabled on this vessel, its trail prisms " +
                 "arrive shielded while the Mass elemental upgrade is active (regular shield, " +
                 "never SuperShield - fauna keep their devastate sink).")]
        [SerializeField] bool massUpgradeShieldsTrail = false;

        [Header("Gap Settings")]
        public float offset;
        public float Gap;
        public float MinimumGap = 1f;
        public Vector3 TargetScale;

        [Header("Spawner Control")]
        [SerializeField] bool spawnerEnabled = true;
        bool trailPenUp; // painting pen-up - independent of spawnerEnabled (see SetSpawnerPaused)
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

        // Properties
        /// <summary>The prism type this vessel lays (read by the painting toy's capture/restore).</summary>
        public PrismType SpawnPrismType => prismType;
        /// <summary>The factory channel this vessel spawns through (read by the painting toy's restore).</summary>
        public PrismEventChannelWithReturnSO PrismSpawnChannel => _onPrismSpawnedEventChannel;
        public float MinWaveLength => minWavelength;
        public ushort TrailLength => (ushort)Trail.TrailList.Count;

        /// <summary>
        /// The SECOND ribbon. Vessels whose spawn pattern lays a double trail put every other
        /// prism here (see <see cref="CreateBlock"/>), so anything reasoning about "the vessel's
        /// whole trail" has to walk both — reading only <see cref="Trail"/> silently misses half
        /// the mass. Read-only on purpose: the controller owns what goes in.
        /// </summary>
        public Trail SecondaryTrail => Trail2;
        /// <summary>
        /// The AUTHORED z extent of a trail prism — <see cref="BaseScale"/>.z alone.
        ///
        /// **This is not the length a prism is actually laid at.** `CreateBlock` multiplies it by
        /// `ZScaler`, the boost scale, and the cube-rooted MASS volume multiplier before spawning,
        /// so on an upgraded vessel the real prism is materially longer than this. Sizing anything
        /// geometric off this property is how the `waitTillOutsideSkimmer` clearance delay came to
        /// turn a prism's collider on while it was still inside the ship; that call site now
        /// measures the local `scale.z` instead, leaving this with no consumer in the controller.
        /// Kept as public surface, but reach for the spawn-time `scale` if you need real geometry.
        /// </summary>
        public float TrailZScale => BaseScale.z; // <- from BaseScale now

        // Guards on the waitTillOutsideSkimmer clearance delay (see CreateBlock). Neither is a
        // tuning dial: the speed floor stops a stationary or near-stationary vessel dividing its
        // way to an infinite delay, and the ceiling stops a slow lay leaving a prism collider-less
        // (and therefore un-hittable by ANYONE, since this delay is not owner-scoped) for longer
        // than the self-trail grace would have covered anyway.
        const float MinClearanceSpeed = 1f;
        const float MaxClearanceWaitSeconds = 2f;

        public event Action<Prism> OnBlockSpawned;
        /// <summary>Static event: fired each time a danger block is created during overheat. Param = owner player name.</summary>
        public static event Action<string> OnDangerBlockCreated;

        private void OnDisable()
        {
            StopSpawn();

            // Deliberately NO ClearTrails() here. The wake OUTLIVES its vessel - mass is
            // conserved, the prisms stay in the world, and a rider must still be able to ride
            // them - so the Trail containers must stay live too. Clearing on disable emptied
            // the lists while every laid prism still pointed at them, and "member of an empty
            // container" reads as a one-block prismscape: the topology routed riders of any
            // DESPAWNED vessel's trail (a swapped-away Squirrel's, always) onto the SURFACE
            // follower, whose along-z "normal" on trail prisms flung the hull everywhere and
            // whose nearest-ground search hopped it between both ribbons. The Trail objects
            // are plain C# state kept alive by the prisms that reference them; they die with
            // their last prism, which is the correct lifetime. Explicit resets that MEAN to
            // drop the bookkeeping (a game-mode turn reset, the cell-swap drain) still call
            // ClearTrails() themselves - and Clear() now un-stamps membership so even those
            // leave honest container-less prisms behind, never members of an empty list.
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
            
            cts = CancellationTokenSource.CreateLinkedTokenSource(this.GetCancellationTokenOnDestroy());
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

        /// <summary>
        /// Pen-up / pen-down for systems that sculpt with the trail (e.g. the fly-by-numbers
        /// painting toy). Deliberately an INDEPENDENT axis from <see cref="spawnerEnabled"/> /
        /// <see cref="StopSpawn"/>/<see cref="StartSpawn"/> (which gameplay vessel actions own):
        /// pen-up only gates block creation, so it never resurrects a spawn loop an ability
        /// stopped, an ability's StartSpawn never overrides a held pen, and pen-down resumes
        /// instantly (no <see cref="startDelay"/>) when the loop is alive.
        /// </summary>
        public void SetSpawnerPaused(bool paused) => trailPenUp = paused;

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
                if (spawnerEnabled && !trailPenUp && !vesselStatus.IsAttached && vesselStatus.Speed > 3f)
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
                CSDebug.LogError("[PrismSpawner] Prism spawn event channel is not assigned.");
                return;
            }

            // --- Compute scale from BaseScale ---
            Vector3 scale = ApplyBoostScale(new Vector3(
                BaseScale.x * XScaler / 2f - Mathf.Abs(halfGap),
                BaseScale.y * YScaler,
                BaseScale.z * ZScaler
            ));

            // MASS -> trail prism volume: live per-spawn read of the prefab-authored
            // ElementalFloat (1x when disabled). Cube root per axis so VOLUME scales
            // linearly with the element level; the mass still flows through the normal
            // conserved spawn channel.
            float volumeMult = trailVolume.EvaluateLive(vesselStatus);
            if (volumeMult > 0f && !Mathf.Approximately(volumeMult, 1f))
                scale *= Mathf.Pow(volumeMult, 1f / 3f);

            // --- Position & Rotation ---
            float xShift = halfGap == 0 ? 0 : (scale.x / 2f + Mathf.Abs(halfGap)) * Mathf.Sign(halfGap);
            Vector3 pos = transform.position - vesselStatus.Course * offset
                        + vesselStatus.ShipTransform.right * xShift;
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
                CSDebug.LogError("[PrismSpawner] Factory returned null or missing Prism component.");
                return;
            }

            // Target scale (also sent in event; set here for gameplay logic)
            prism.TargetScale = scale;

            prism.ownerID = vesselStatus.PlayerName;

            // Team
            prism.ChangeTeam(vesselStatus.Domain);

            // Wait time — how long this prism's collider stays off so the vessel that laid it can
            // get clear. Measured against `scale.z`, the length this prism is ACTUALLY being laid
            // at, not the authored `TrailZScale` (= BaseScale.z) it used to use: BaseScale.z omits
            // both ZScaler and the MASS volume multiplier applied above, so an upgraded vessel
            // laying stretched mass had its collider come on while the prism was still inside the
            // ship. Un-upgraded vessels are unchanged — with ZScaler 1 and no boost/volume scaling
            // `scale.z` IS `BaseScale.z`, so this only ever lengthens the delay when the prism is
            // genuinely longer.
            //
            // Note this delay hides the prism from EVERYONE, which is why it stays a geometry
            // correction and is not the lever for self-trail contact: that is owner-scoped and
            // lives in SelfTrailContactConfigSO.
            prism.waitTime = waitTillOutsideSkimmer
                ? Mathf.Min((skimmer.transform.localScale.z + scale.z) /
                            Mathf.Max(vesselStatus.Speed, MinClearanceSpeed),
                            MaxClearanceWaitSeconds)
                : waitTime;

            if (_dangerMode)
            {
                // The flag is the whole job: Prism.Initialize sees IsDangerous and runs
                // MakeDangerous() through the real pipeline — per-domain theme danger
                // material + the one color-transition engine (clock-material or legacy;
                // Docs/PRISM_ANIMATION.md). The former direct blend/sharedMaterial write
                // here cloned materials, fought that pipeline, and was invisible on the
                // instanced render path (disabled MeshRenderer).
                try { prism.prismProperties.IsDangerous = true; } catch { /* ignore */ }

                OnDangerBlockCreated?.Invoke(vesselStatus.PlayerName);
            }

            
            // Shield. MASS level-5 'Heavy Trail': trail prisms arrive shielded ONLY while
            // DRIFTING with the Mass upgrade active (per-spawn snapshot; regular shield only).
            // Straight-line trail stays unshielded - the armor is the drift line's reward.
            if (shielded || (massUpgradeShieldsTrail
                             && vesselStatus is { IsDrifting: true }
                             && vesselStatus.ElementalAbilityHandler?.IsUpgradeActive(Element.Mass) == true))
                prism.prismProperties.IsShielded = true;

            // Add to trail & initialize
            trail.Add(prism);
            prism.prismProperties.Index = (ushort)trail.TrailList.IndexOf(prism);
            prism.Initialize(vesselStatus.PlayerName);

            // AFTER Initialize (pool-reuse reset clears membership - AssignTrail's contract).
            // This stamp is what makes a wake block a member of ITS ribbon: without it every
            // wake prism either had NO container (fresh instance - the attach gate refused it)
            // or a STALE one from a previous pooled life (the gate passed against the wrong
            // ribbon and the ride followed garbage). The twin trails were always two separate
            // Trail objects; this is what finally lets a rider see that.
            prism.AssignTrail(trail);

            // Events
            OnBlockSpawned?.Invoke(prism);
        }

        public List<Prism> GetLastTwoBlocks()
        {
            if (Trail2.TrailList.Count > 0)
                return new List<Prism> { Trail.TrailList[^1], Trail2.TrailList[^1] };
            return null;
        }
        
        /// <param name="dangerMat">LEGACY — ignored. The danger paint comes from the
        /// state pipeline (per-domain theme danger material via IsDangerous →
        /// Prism.Initialize → MakeDangerous), never a direct renderer write
        /// (Docs/PRISM_ANIMATION.md §3.8). Parameter kept for caller compatibility.</param>
        public void EnableDangerMode(Material dangerMat, Vector3 scaleMult, float lerpSeconds = 0f,
            float blendSeconds = 0f, bool append = true)
        {
            _dangerMode = true;
            LerpScaleMultipliers(scaleMult, lerpSeconds);
        }

        public void DisableDangerMode(float lerpSeconds = 0f)
        {
            _dangerMode = false;
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