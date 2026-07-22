using System;
using Unity.Profiling;
using UnityEngine;
using System.Collections;
using CosmicShore.Core;
using CosmicShore.Utility;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using UnityEngine.Serialization;
using CosmicShore.Data;
using CosmicShore.ECS;
using System.Linq;
namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Analytic containment test for an engaged prism shield. Implemented by
    /// <see cref="PrismOctahedronShield"/> (octahedron, L1 test) and
    /// <see cref="PrismStellatedOctahedronShield"/> (stellation, 4-linear-form
    /// tetrahedral-union test). While a shield is engaged, its PhysX trigger is
    /// the shield's box AABB proxy (see <see cref="Prism.SetShieldColliderState"/>);
    /// this gate is the narrowphase that rejects contacts landing in the AABB's
    /// corner/notch regions outside the true shield surface
    /// (<see cref="ImpactorBase.OnTriggerEnter"/>).
    /// </summary>
    public interface IShieldContainmentGate
    {
        bool ContainsWorldPoint(Vector3 worldPoint);
    }

    /// <summary>
    /// Which collider represents the prism to PhysX, as driven by the shield
    /// components. Explicit values — Unity serialization drift rule.
    /// </summary>
    public enum ShieldColliderState
    {
        None = 0,       // unshielded: the authored BoxCollider is the trigger
        Morphing = 1,   // engage bloom in flight: no collider (matches legacy morph window)
        Engaged = 2,    // shield settled: the AABB proxy BoxCollider is the trigger
    }

    [RequireComponent(typeof(MaterialPropertyAnimator))]
    [RequireComponent(typeof(PrismScaleAnimator))]
    [RequireComponent(typeof(PrismTeamManager))]
    [RequireComponent(typeof(PrismStateManager))]
    public class Prism : MonoBehaviour
    {
        protected const string DEFAULT_PLAYER_NAME = "DefaultPlayer";

        [Header("Prism Properties")]
        [SerializeField] public PrismProperties prismProperties;
        public GameObject ParticleEffect;
        public Trail Trail;

        [Header("Prism Growth")]
        public Vector3 GrowthVector = new Vector3(0, 2, 0);
        public float growthRate = 0.01f;
        public float waitTime = 0.6f;

        // One yield token per prism life, reused across pool reuses — a fresh
        // WaitForSeconds per spawn was a managed alloc for every prism ever laid,
        // steady food for the mid-session GC.Collect spikes.
        WaitForSeconds _spawnWait;
        float _spawnWaitSeconds = -1f;

        [Header("Prism Status")] 
        public bool destroyed;
        public bool devastated;

        // Set transiently by Damage/Consume right before destruction so the destruction
        // SFX can tell a creature (fauna) kill from a generic block destroy. Reset on pool reuse.
        bool _destroyedByCreature;
        public bool IsSmallest;
        public bool IsLargest;
        
        [Header("Team Ownership")] 
        public string ownerID;

        [Header("Event Channels")]
        [SerializeField] ScriptableEventPrismStats _onTrailBlockCreatedEventChannel;
        [SerializeField] ScriptableEventPrismStats _onTrailBlockDestroyedEventChannel;
        [SerializeField] ScriptableEventPrismStats _onTrailBlockRestoredEventChannel;
        [SerializeField] internal PrismEventChannelWithReturnSO OnBlockImpactedEventChannel;

        public Action<Prism> OnReturnToPool;
        private Vector3 _lastDestructionScale = Vector3.one;

        // Authored BoxCollider size, cached in Awake so HoldColliderAtFullSize can compensate the
        // collider against the animated transform scale and ResetState can restore it on pool reuse.
        Vector3 _authoredColliderSize = Vector3.one;
        bool _authoredColliderSizeCached;

        /// <summary>
        /// Index into PrismSpatialIndex's contiguous NativeArray - the canonical
        /// spatial index of all live prism mass (AOE damage queries, growth
        /// occupancy, neighborhood queries, AND the cell density-grid binding;
        /// see Docs/SPATIAL_INDEX.md). Used for O(1) updates to
        /// cache-line-packed spatial data. -1 means not registered.
        /// </summary>
        internal int SpatialIndexId = -1;


        public Domains Domain
        {
            get => teamManager?.Domain ?? Domains.Blue;
            set
            {
                if (teamManager) teamManager.SetInitialTeam(value);
            }
        }

        string _playerName;
        public string PlayerName { get; internal set; }

        /// <summary>
        /// True for environment/structure prisms (e.g. the HexRace track, spawnable shapes)
        /// that are not laid by a player vessel. These keep the default owner because they
        /// are spawned via <see cref="SpawnableBase.SpawnPrismTrail"/> with no player name,
        /// and they survive vessel contact (no destructible player trail). Player-laid trail
        /// prisms carry the owning vessel's player name instead.
        /// </summary>
        public bool IsEnvironmentOwned =>
            string.IsNullOrEmpty(PlayerName) || PlayerName == DEFAULT_PLAYER_NAME;

        // Component references
        private MaterialPropertyAnimator materialAnimator;
        private PrismScaleAnimator scaleAnimator;
        private PrismTeamManager teamManager;
        private PrismStateManager stateManager;
        private MeshRenderer meshRenderer;
        private MeshFilter meshFilter;
        private BoxCollider blockCollider;

        // --- Instanced rendering (Entities Graphics companion entity) -----------
        // While the instanced path is active the MeshRenderer stays disabled and a
        // companion entity draws in its place (PrismRenderService — see
        // Docs/PRISM_ECS_MIGRATION.md). _renderVisible is the single visibility
        // truth for BOTH paths; _exoticVisualActive forces the GameObject renderer
        // for per-prism-unique geometry (octahedron shield morph/shatter).
        internal PrismRenderHandle RenderHandle;
        bool _renderVisible;
        bool _exoticVisualActive;

        /// <summary>True when per-frame color animation should sink into the
        /// companion entity instead of a MaterialPropertyBlock.</summary>
        internal bool UsesEntityColorSink =>
            !_exoticVisualActive && PrismRenderService.IsHandleUsable(in RenderHandle);

        public Vector3 TargetScale
        {
            get => scaleAnimator?.TargetScale ?? transform.localScale;
            set
            {
                scaleAnimator?.SetTargetScale(value);
                scaleAnimator?.BeginGrowthAnimation();
            }
        }

        public float Volume => scaleAnimator?.GetCurrentVolume() ?? .001f;
        public BlockState CurrentState => stateManager?.CurrentState ?? BlockState.Normal;

        public Vector3 MaxScale
        {
            get => scaleAnimator?.MaxScale ?? Vector3.one * 10f;
            set
            {
                if (scaleAnimator is not null) scaleAnimator.MaxScale = value;
            }
        }

        public void ChangeSize()
        {
            if (scaleAnimator is not null)
            {
                scaleAnimator.SetTargetScale(TargetScale);
                scaleAnimator.BeginGrowthAnimation();
            }
        }

        /// <summary>
        /// Overrides the grow-in speed at runtime. The <see cref="growthRate"/> field is cached
        /// onto the scale animator in <see cref="Awake"/>, so a pooled prism won't honour a later
        /// field write on its own - this pushes the value through to the animator too. Used by the
        /// boost-prism pool (fast bloom).
        /// </summary>
        public void SetGrowthRate(float rate)
        {
            growthRate = rate;
            if (scaleAnimator is not null) scaleAnimator.GrowthRate = rate;
        }

        /// <summary>
        /// Guarantees this prism's collider covers its FULL target world size from the frame of the
        /// call, while the visual still blooms in from (near) zero - the continuity law is visual;
        /// the physics footprint doesn't have to grow with it. The collider is enabled immediately
        /// and its BoxCollider.size is compensated inversely against the animated transform scale
        /// each frame, then restored to the authored size once growth completes. Call AFTER
        /// <see cref="Initialize"/> (ResetState would stop the coroutine).
        ///
        /// Used by <c>BoostRingBuilder</c> (omnicrystal ring / joust ring / Squirrel tube) so a
        /// skimmer collides with a just-spawned ring deterministically no matter how fast the
        /// vessel arrives.
        /// </summary>
        /// <param name="onGrown">Invoked once the visual reaches its target scale (skipped if the
        /// prism is destroyed or recycled first). Lets callers defer state that swaps colliders -
        /// e.g. shield engage, whose octahedron MeshCollider scales with the transform and would
        /// defeat the full-size hold during the bloom.</param>
        public void HoldColliderAtFullSize(Action onGrown = null)
        {
            if (!blockCollider || !_authoredColliderSizeCached)
            {
                onGrown?.Invoke();
                return;
            }
            StartCoroutine(HoldColliderAtFullSizeCoroutine(onGrown));
        }

        private IEnumerator HoldColliderAtFullSizeCoroutine(Action onGrown)
        {
            // The full-size hold replaces the spawn window (callers defer shield
            // engage to onGrown, so the shield state is None for its duration).
            _colliderLifecycleReady = true;
            blockCollider.enabled = true;

            while (!destroyed && blockCollider)
            {
                Vector3 target = TargetScale;
                if (target.x <= 0f || target.y <= 0f || target.z <= 0f)
                {
                    // Target not set yet - wait, a zero target has no meaningful footprint.
                    yield return null;
                    continue;
                }

                Vector3 current = transform.localScale;
                if ((current - target).sqrMagnitude <= 1e-4f)
                    break;

                // A zero transform scale zeroes the collider's world footprint no matter how large
                // its size is - floor the bloom at 1% of target so the compensation below can
                // always reconstruct the full-size world box (world = size * scale = target).
                current = new Vector3(
                    Mathf.Max(current.x, target.x * 0.01f),
                    Mathf.Max(current.y, target.y * 0.01f),
                    Mathf.Max(current.z, target.z * 0.01f));
                transform.localScale = current;

                blockCollider.size = new Vector3(
                    _authoredColliderSize.x * target.x / current.x,
                    _authoredColliderSize.y * target.y / current.y,
                    _authoredColliderSize.z * target.z / current.z);

                yield return null;
            }

            if (blockCollider)
                blockCollider.size = _authoredColliderSize;

            if (!destroyed)
                onGrown?.Invoke();
        }

        private void Awake()
        {
            materialAnimator = GetComponent<MaterialPropertyAnimator>();
            scaleAnimator = GetComponent<PrismScaleAnimator>();
            teamManager = GetComponent<PrismTeamManager>();
            stateManager = GetComponent<PrismStateManager>();
            meshRenderer = GetComponent<MeshRenderer>();
            meshFilter = GetComponent<MeshFilter>();
            blockCollider = GetComponent<BoxCollider>();

            if (blockCollider)
            {
                _authoredColliderSize = blockCollider.size;
                _authoredColliderSizeCached = true;
            }

            scaleAnimator.GrowthRate = growthRate;
            InitializePrismProperties();

            // Keep the cell's per-domain grids consistent when this prism changes
            // hands (steal / ChangeTeam): the cell re-files it under the new domain.
            if (teamManager)
                teamManager.OnTeamChanged += HandleTeamChangedForCell;
        }

        // --- Cell density-grid domain forwarding -------------------------------
        // (Registration itself lives in PrismSpatialIndex since Phase 3 - the
        // index binds/releases the cell grids at Register/MarkDestroyed/
        // MarkRestored/Unregister, so the fine and coarse views share one stream.)

        void HandleTeamChangedForCell(Domains oldDomain, Domains newDomain)
        {
            // The spatial index owns the cell density-grid binding (Phase 3 - see
            // Docs/SPATIAL_INDEX.md): forward the steal / ChangeTeam so the bound
            // cell re-files this prism under the new domain. No-op while
            // unregistered (spawn window) or unbound (fauna body, open space).
            if (SpatialIndexId >= 0)
            {
                PrismSpatialIndex.Instance?.ForwardDomainChangeToCell(SpatialIndexId);

                // Also refresh the AOE cold-data domain. It was only written at Register,
                // so stolen prisms kept their old domain in explosion queries — the
                // documented stale-steal gap (Docs/SPATIAL_INDEX.md): explosions could
                // destroy prisms recently stolen INTO the shooter's domain, and the
                // Charge-5 'spare own domain' unlock needs "own" to be live.
                PrismSpatialIndex.Instance?.UpdateDomain(SpatialIndexId, (int)newDomain);
            }
        }

        // --- Instanced rendering routing ----------------------------------------

        /// <summary>
        /// Single visibility entry point for both render paths. Replaces direct
        /// meshRenderer.enabled writes throughout the lifecycle so the companion
        /// entity and the MeshRenderer can never both draw.
        /// </summary>
        void SetRenderVisible(bool visible)
        {
            _renderVisible = visible;
            ApplyRenderPath();
        }

        void ApplyRenderPath()
        {
            // An inactive GameObject must never show its entity: during pooled
            // release, component OnDisable order is undefined — the octahedron
            // shield's Disengage can route through here AFTER Prism.OnDisable
            // already hid the entity, and re-showing it would orphan a visible
            // entity until pool reuse.
            bool show = _renderVisible && gameObject.activeInHierarchy;

            if (show && !_exoticVisualActive && PrismRenderService.Enabled)
                EnsureRenderEntity();

            bool entityPath = !_exoticVisualActive && PrismRenderService.IsHandleUsable(in RenderHandle);
            if (entityPath)
            {
                if (meshRenderer && meshRenderer.enabled) meshRenderer.enabled = false;
                if (show)
                {
                    SyncRenderMaterial();
                    SyncRenderTransform();
                }
                // Batched: applied in one structural change per direction at
                // LateUpdate (same frame, before rendering). Per-prism toggles were
                // the dominant creation-tick cost (Prism.Create.Visibility).
                PrismRenderService.QueueVisible(in RenderHandle, show);
            }
            else
            {
                // Immediate: the GameObject renderer takes over THIS frame (exotic
                // shield morph / legacy fallback) — a queued hide would double-draw
                // entity + MeshRenderer for the rest of the frame.
                if (PrismRenderService.IsHandleUsable(in RenderHandle))
                    PrismRenderService.SetVisible(in RenderHandle, false);
                if (meshRenderer) meshRenderer.enabled = _renderVisible;
            }
        }

        // While set, the companion entity renders this mesh (a cache-shared settled-shield
        // octahedron) instead of meshFilter.sharedMesh. This keeps SETTLED shielded prisms
        // on the instanced path — batched with every same-geometry shielded prism — while
        // the GameObject renderer handles only the brief per-prism morph/shatter animations.
        Mesh _renderMeshOverride;

        void EnsureRenderEntity()
        {
            if (PrismRenderService.IsHandleUsable(in RenderHandle)) return;
            if (!meshRenderer || !meshFilter) return;
            Mesh renderMesh = _renderMeshOverride != null ? _renderMeshOverride : meshFilter.sharedMesh;
            if (renderMesh == null || meshRenderer.sharedMaterial == null) return;
            RenderHandle = PrismRenderService.Create(
                renderMesh, meshRenderer.sharedMaterial,
                transform.localToWorldMatrix, gameObject.layer);
        }

        /// <summary>Renders the companion entity with a shared mesh (settled octahedron
        /// shield) in place of the prism's own mesh. Pair with SetExoticVisualActive(false)
        /// so the entity path re-engages.</summary>
        internal void SetRenderMeshOverride(Mesh sharedMesh)
        {
            _renderMeshOverride = sharedMesh;
            if (sharedMesh != null && PrismRenderService.IsHandleUsable(in RenderHandle))
                PrismRenderService.SetMesh(in RenderHandle, sharedMesh);
        }

        /// <summary>Returns the companion entity to the prism's own mesh (disengage /
        /// pool reuse). Safe to call when no override is active.</summary>
        internal void ClearRenderMeshOverride()
        {
            if (_renderMeshOverride == null) return;
            _renderMeshOverride = null;
            if (PrismRenderService.IsHandleUsable(in RenderHandle) && meshFilter != null && meshFilter.sharedMesh != null)
            {
                PrismRenderService.SetMesh(in RenderHandle, meshFilter.sharedMesh);
                SyncRenderMaterial();
                SyncRenderTransform();
            }
        }

        /// <summary>Pushes the live transform to the companion entity. Called on
        /// show, by PrismScaleManager during growth, and by movers via
        /// NotifyPositionChanged.</summary>
        internal void SyncRenderTransform()
        {
            if (_exoticVisualActive) return;
            if (!PrismRenderService.IsHandleUsable(in RenderHandle)) return;
            PrismRenderService.SetTransform(in RenderHandle, transform.localToWorldMatrix);
        }

        /// <summary>Re-syncs the entity's base material from the MeshRenderer after
        /// any sharedMaterial swap (domain / state / transparency changes).</summary>
        internal void SyncRenderMaterial()
        {
            if (meshRenderer == null) return;
            if (!PrismRenderService.IsHandleUsable(in RenderHandle))
            {
                // The material may only now have arrived (a prism shown before its
                // domain material was assigned skipped entity creation) — give the
                // instanced path another chance to claim rendering. EnsureRenderEntity
                // is a no-op on failure, so this cannot recurse.
                if (_renderVisible && !_exoticVisualActive && PrismRenderService.Enabled
                    && gameObject.activeInHierarchy)
                {
                    EnsureRenderEntity();
                    if (PrismRenderService.IsHandleUsable(in RenderHandle))
                        ApplyRenderPath();
                }
                return;
            }
            bool refreshColors = materialAnimator == null || !materialAnimator.IsAnimating;
            PrismRenderService.SetMaterial(in RenderHandle, meshRenderer.sharedMaterial, refreshColors);
        }

        /// <summary>
        /// Octahedron shield (and any future per-prism-unique geometry) forces the
        /// GameObject renderer while active; rendering returns to the instanced
        /// path on release. Visibility state carries across the handoff.
        /// </summary>
        internal void SetExoticVisualActive(bool active)
        {
            if (_exoticVisualActive == active) return;
            _exoticVisualActive = active;
            ApplyRenderPath();

            // Color continuity: the displayed colors live on the outgoing path —
            // push them onto the incoming one so the handoff frame can't flash a
            // stale MaterialPropertyBlock (engage) or pre-engage entity colors
            // (disengage).
            if (active)
                materialAnimator?.FlushDisplayedColorsToRenderer();
            else
                SyncRenderColorsFromAnimator();
        }

        /// <summary>Pushes the animator's in-flight colors onto the companion
        /// entity (used when the entity path resumes mid-animation).</summary>
        internal void SyncRenderColorsFromAnimator()
        {
            if (materialAnimator == null || !materialAnimator.IsAnimating) return;
            if (!PrismRenderService.IsHandleUsable(in RenderHandle)) return;
            PrismRenderService.SetColors(in RenderHandle,
                PrismRenderService.ToFloat4(materialAnimator.CurrentBrightColor),
                PrismRenderService.ToFloat4(materialAnimator.CurrentDarkColor),
                PrismRenderService.ToFloat3(materialAnimator.CurrentSpread));
        }

        /// <summary>
        /// Called when spawning from pool. Resets state and starts growth.
        /// </summary>
        public virtual void Initialize(string playerName = DEFAULT_PLAYER_NAME)
        {
            // [Fix] Always clean up previous state when coming from pool
            ResetState();
            ClearRenderMeshOverride(); // pooled reuse: the entity must not keep a prior life's shield mesh

            PlayerName = playerName;
            blockCollider.enabled = false;
            SetRenderVisible(false);

            var authoredTargetScale = scaleAnimator ? scaleAnimator.TargetScale : transform.localScale;
            if (authoredTargetScale == Vector3.zero)
                authoredTargetScale = transform.localScale;

            scaleAnimator.Initialize();
            scaleAnimator.SetTargetScale(authoredTargetScale);
            StartCoroutine(CreateBlockCoroutine(authoredTargetScale));

            if (prismProperties.IsShielded) ActivateShield();
            if (prismProperties.IsDangerous) MakeDangerous();
        }

        private void ResetState()
        {
            // Unregister from the spatial index - drops the AOE entry, the
            // occupancy bucket, AND the previous cell's density-grid binding.
            // Pool-reuse safety: a prism re-initialized without going through
            // SetupDestruction (e.g. trail clear) must not leave stale entries
            // in any view.
            if (SpatialIndexId >= 0)
            {
                PrismSpatialIndex.Instance?.Unregister(SpatialIndexId);
                SpatialIndexId = -1;
            }

            destroyed = false;
            devastated = false;
            _destroyedByCreature = false; // pool reuse: clear stale creature-kill flag
            _lodCulled = false; // pool reuse: Initialize owns the collider again
            CachedVolume = 0f;  // stale from the previous life; reseeded at CreateBlock
            IsSmallest = false;
            IsLargest = false;

            // SetupDestruction disables the scale animator (destroyed mass must stop scaling and
            // weighing). Re-arm it on pool-reuse creation so the re-minted prism grows in from
            // zero (continuity law) and GetCurrentVolume tracks again (volume is the spine).
            // Gated on !enabled so fresh spawns and never-destroyed pooled reuse are untouched;
            // Restore() never routes through here and keeps its bookkept-volume fallback.
            if (scaleAnimator && !scaleAnimator.enabled)
            {
                scaleAnimator.enabled = true;
                transform.localScale = Vector3.zero;
            }

            // Clear trail renderer to prevent visual artifacts across the map
            if (Trail != null && Trail.TrailRenderer != null)
            {
                Trail.TrailRenderer.Clear();
            }
            
            // Ensure physics/rendering are off until Coroutine enables them
            if (blockCollider) blockCollider.enabled = false;
            SetRenderVisible(false);

            // Pool-reuse safety: the shield components snap to unshielded in their
            // OnDisable, but a prism re-initialized without a disable cycle must not
            // carry a previous life's shield collider state or narrowphase gate.
            _shieldColliderState = ShieldColliderState.None;
            ActiveShieldGate = null;
            if (_shieldProxyCollider) _shieldProxyCollider.enabled = false;
            _colliderLifecycleReady = false; // spawn window owns the collider again

            // Pool-reuse safety: a HoldColliderAtFullSize bloom interrupted by recycle (deactivate
            // stops its coroutine before the restore step) must not leak an inflated collider size
            // into the next use of this instance.
            if (blockCollider && _authoredColliderSizeCached) blockCollider.size = _authoredColliderSize;

            StopAllCoroutines();
        }

        /// <summary>
        /// Public method to immediately return this instance to the pool.
        /// </summary>
        public void ReturnToPool()
        {
            OnReturnToPool?.Invoke(this);
        }

        private void InitializePrismProperties()
        {
            if (prismProperties == null) return;
 
            prismProperties.position = transform.position;
            prismProperties.prism = this;
            prismProperties.Trail = Trail;
            prismProperties.TimeCreated = Time.time;
            int defaultLayer = LayerMask.NameToLayer(prismProperties.DefaultLayerName);
            if (defaultLayer >= 0)
                gameObject.layer = defaultLayer;
            else
                Debug.LogWarning($"[Prism] '{name}' has an invalid PrismProperties.DefaultLayerName '{prismProperties.DefaultLayerName}' — keeping layer '{LayerMask.LayerToName(gameObject.layer)}'.", this);

            prismProperties.volume = 1f;
        }

        // Creation-completion budget. Simultaneous spawns (a pooled ring detonation,
        // a flora regrow wave, a trail burst) all sleep the same waitTime, so their
        // creation ticks land phase-locked on one frame — N inline SOAP raises,
        // spatial-index registrations, and render activations at once (the
        // CreateBlockCoroutine ×12 profiler burst). At most this many prisms finish
        // creation per frame; the rest retry next frame. On top of the 0.6s spawn
        // window the extra frames are invisible, and nothing is ever skipped.
        const int MaxCreationCompletionsPerFrame = 6;
        static int s_creationCompletionsThisFrame;
        static int s_creationBudgetFrame = -1;

        // Split attribution for the ~0.5ms creation-completion tick: which of the
        // three suspects dominates decides the fix (enableable-component render flag
        // vs SOAP listener work vs spatial bind). See
        // Docs/PERFORMANCE_OPTIMIZATION.md Task 4.
        static readonly ProfilerMarker s_createVisibilityMarker = new("Prism.Create.Visibility");
        static readonly ProfilerMarker s_createSoapMarker = new("Prism.Create.SOAPRaise");
        static readonly ProfilerMarker s_createSpatialMarker = new("Prism.Create.SpatialBind");

        private IEnumerator CreateBlockCoroutine(Vector3 authoredTargetScale)
        {
            if (_spawnWait == null || _spawnWaitSeconds != waitTime)
            {
                _spawnWait = new WaitForSeconds(waitTime);
                _spawnWaitSeconds = waitTime;
            }
            yield return _spawnWait;

            // Destroyed before creation completed (e.g. AOE within waitTime of spawn) -
            // don't resurrect the renderer/collider or register a dead prism with the
            // AOE registry and cell grids.
            if (destroyed) yield break;

            while (true)
            {
                if (s_creationBudgetFrame != Time.frameCount)
                {
                    s_creationBudgetFrame = Time.frameCount;
                    s_creationCompletionsThisFrame = 0;
                }
                if (s_creationCompletionsThisFrame < MaxCreationCompletionsPerFrame)
                    break;

                yield return null;
                if (destroyed) yield break; // killed while waiting for budget
            }
            s_creationCompletionsThisFrame++;

            using (s_createVisibilityMarker.Auto())
            {
                SetRenderVisible(true);
                // Spawn window over — hand the collider to the lifecycle, routed to
                // whichever collider the shield state designates (a pooled prism
                // re-initialized with IsShielded may have settled its shield during
                // the window; previously that re-enabled the box UNDER the engaged
                // shield collider, double-triggering).
                _colliderLifecycleReady = true;
                ApplyShieldDesignatedCollider();
            }

            if (scaleAnimator.TargetScale == Vector3.zero)
                scaleAnimator.SetTargetScale(authoredTargetScale);

            prismProperties.volume = scaleAnimator.GetCurrentVolume();

            scaleAnimator.BeginGrowthAnimation();

            using (s_createSoapMarker.Auto())
            {
                _onTrailBlockCreatedEventChannel.Raise(new PrismStats
                {
                    OwnName = PlayerName,
                    Volume = prismProperties.volume,
                });
            }

            // Register with the spatial index - one registration, every view:
            // cache-friendly batch AOE processing, growth occupancy (consumes the
            // TryReserve claim that protected this site through the
            // disabled-collider window), neighborhood queries, and the containing
            // cell's density grids. The cell binding is what makes trail mass
            // visible to fauna anti-domain targeting and the cell's phase system;
            // fauna bodies are excluded from that view inside the index.
            // Seed the volume cache before the cell starts aggregating this prism
            // (its summation-view slot binds via the Register → BindCell path
            // below), so the first volume recompute reads a real value, not the
            // default 0.
            using (s_createSpatialMarker.Auto())
            {
                RefreshVolumeCache();

                var spatialIndex = PrismSpatialIndex.EnsureInstance();
                if (spatialIndex != null && spatialIndex.IsAvailable)
                    SpatialIndexId = spatialIndex.Register(this);

                // The LOD sweep is transition-based — it must be told about colliders
                // that come online between ticks, or a prism born far from every focus
                // keeps its collider until a bubble boundary happens to cross it.
                PrismColliderLodManager.NotifyPrismActivated(this);
            }
        }

        /// <summary>
        /// This prism's LIVE volume (world-scale product), the unit of mass the
        /// ecosystem runs on - "volume is the spine" (CLAUDE.md ▸ Ecosystem Design
        /// Principles). Tracks growth/shrink in real time via the scale animator;
        /// destroyed mass contributes nothing. Read by Cell's per-domain volume sums
        /// (phase ladder, dominant domain, HUD).
        /// </summary>
        // --- Proximity collider-LOD (PrismColliderLodManager) ------------------
        // Prism colliders only matter near the things that physically touch prisms
        // (vessels, projectiles); fauna senses, AOE damage, and growth occupancy all
        // ride PrismSpatialIndex and need no collider. The LOD manager culls far
        // colliders; these fields remember the pre-cull state so unculling restores
        // exactly what the lifecycle/shield systems had set, never fighting them.
        bool _lodCulled;
        bool _colliderBeforeLodCull;

        // (Near/far transition memory now lives in the spatial index's per-slot
        // LodNear flag bit, maintained by the Burst classification pass — the old
        // per-prism sweep stamp is gone with the managed sliced sweep.)

        /// <summary>
        /// Called by <c>PrismColliderLodManager</c> ONLY. Culls/restores this prism's
        /// collider by vessel/projectile proximity. Idempotent; destruction and the
        /// spawn window own the collider outright (a culled prism that gets destroyed
        /// stays collider-off; Restore re-enables directly and the next LOD tick
        /// re-evaluates). Covers BOTH trigger colliders — the authored box and, while
        /// a shield is engaged, its AABB proxy — so shielded prisms are LOD-cullable
        /// (they were not while shields swapped in an always-on convex MeshCollider).
        /// </summary>
        public void SetColliderCulledByLod(bool culled)
        {
            if (_lodCulled == culled) return;
            if (!blockCollider) return;
            if (culled)
            {
                // One snapshot for "any trigger was on" — restore routes to whichever
                // collider the CURRENT shield state designates, so a shield engage or
                // disengage that happened while far (e.g. AOE) is honored on un-cull.
                _colliderBeforeLodCull = blockCollider.enabled ||
                    (_shieldProxyCollider && _shieldProxyCollider.enabled);
                blockCollider.enabled = false;
                if (_shieldProxyCollider) _shieldProxyCollider.enabled = false;
            }
            else if (!destroyed && _colliderBeforeLodCull)
            {
                ApplyShieldDesignatedCollider();
            }
            _lodCulled = culled;
        }

        /// <summary>True while this prism's collider is enabled (LOD telemetry).</summary>
        public bool ColliderEnabled =>
            (blockCollider && blockCollider.enabled) ||
            (_shieldProxyCollider && _shieldProxyCollider.enabled);

        // --- Shield collider proxy + narrowphase gate ---------------------------
        // Engaged shields (octahedron / stellation) no longer swap in a convex
        // MeshCollider. Their PhysX trigger is a second BoxCollider sized to the
        // shield's AABB (±s·a, ±s·b, ±s·c) — for the stellation this is exactly the
        // convex hull PhysX computed anyway, and for the octahedron the over-cover
        // in the AABB corners is rejected by the analytic narrowphase gate
        // (ImpactorBase.OnTriggerEnter → ActiveShieldGate). One proxy serves both
        // shield tiers (same shieldScale, same AABB); it composes with the LOD cull
        // and destruction overlays above instead of being exempt from them.
        BoxCollider _shieldProxyCollider;
        ShieldColliderState _shieldColliderState = ShieldColliderState.None;

        // True once the spawn window (CreateBlockCoroutine) or a full-size hold /
        // Restore has handed the collider to the lifecycle. Defaults true so
        // scene-placed prisms that never run Initialize keep working; Initialize
        // resets it for the pooled path.
        bool _colliderLifecycleReady = true;

        /// <summary>
        /// The analytic containment test of the currently ENGAGED shield, or null
        /// when unshielded/morphing. Read by <see cref="ImpactorBase.OnTriggerEnter"/>
        /// as the narrowphase over the AABB proxy trigger.
        /// </summary>
        public IShieldContainmentGate ActiveShieldGate { get; private set; }

        /// <summary>The authored trigger BoxCollider (never the shield proxy).</summary>
        public BoxCollider AuthoredCollider => blockCollider;

        /// <summary>
        /// Called by the shield components ONLY. Selects which collider represents
        /// the prism (authored box / none while morphing / AABB proxy) and installs
        /// the narrowphase gate. Composes with destruction, the LOD cull, and the
        /// spawn window — an overlay that currently owns the collider keeps every
        /// trigger off, and the un-cull / spawn-window-end / Restore paths route to
        /// the collider designated here.
        /// </summary>
        public void SetShieldColliderState(ShieldColliderState state, IShieldContainmentGate gate, float shieldScale)
        {
            _shieldColliderState = state;
            ActiveShieldGate = state == ShieldColliderState.Engaged ? gate : null;

            if (state == ShieldColliderState.Engaged)
                EnsureShieldProxyCollider(shieldScale);

            bool overlayOwnsCollider = destroyed || _lodCulled || !_colliderLifecycleReady;

            if (blockCollider)
                blockCollider.enabled = !overlayOwnsCollider && state == ShieldColliderState.None;
            if (_shieldProxyCollider)
                _shieldProxyCollider.enabled = !overlayOwnsCollider && state == ShieldColliderState.Engaged;
        }

        /// <summary>
        /// Enables whichever trigger collider the shield state designates (box when
        /// unshielded, proxy when engaged, none while morphing). Used by the paths
        /// that hand the collider back to the lifecycle: spawn-window end, LOD
        /// un-cull, and Restore.
        /// </summary>
        void ApplyShieldDesignatedCollider()
        {
            switch (_shieldColliderState)
            {
                case ShieldColliderState.None:
                    if (blockCollider) blockCollider.enabled = true;
                    break;
                case ShieldColliderState.Engaged:
                    if (_shieldProxyCollider) _shieldProxyCollider.enabled = true;
                    break;
                // Morphing: the shield owns the window; both stay off.
            }
        }

        void EnsureShieldProxyCollider(float shieldScale)
        {
            if (_shieldProxyCollider || !blockCollider) return;
            var proxy = gameObject.AddComponent<BoxCollider>();
            proxy.center = blockCollider.center;
            // Authored size — blockCollider.size is animated by HoldColliderAtFullSize.
            var baseSize = _authoredColliderSizeCached ? _authoredColliderSize : blockCollider.size;
            proxy.size = baseSize * shieldScale;
            proxy.isTrigger = blockCollider.isTrigger;
            proxy.enabled = false;
            _shieldProxyCollider = proxy;
        }

        public float CurrentVolume
        {
            get
            {
                if (destroyed) return 0f;
                // GetCurrentVolume reads the live transform but returns 0 while the
                // animator component is disabled - which Restore() leaves it as. Fall
                // back to the bookkept volume (stamped at create/destroy) so restored
                // mass still weighs what it did, rather than vanishing from the sums.
                float v = scaleAnimator ? scaleAnimator.GetCurrentVolume() : 0f;
                if (v > 0f) return v;
                return Mathf.Max(prismProperties?.volume ?? 0f, 0f);
            }
        }

        /// <summary>
        /// Cached copy of <see cref="CurrentVolume"/>, refreshed ONLY when this prism's
        /// scale actually changes (PrismScaleManager during growth + on settle, plus
        /// create / restore). Cell.EnsureVolumeFresh reads THIS instead of CurrentVolume
        /// so the per-domain volume aggregation over the whole prism population stops
        /// doing a transform.lossyScale parent-walk per prism per recompute — the
        /// dominant main-thread cost at high prism counts (a single recompute of ~9k
        /// prisms was ~23 ms). Value is identical to CurrentVolume for settled prisms
        /// (which never change scale) and lags by at most one frame for the handful
        /// actively growing, so "volume is the spine" semantics are preserved — only the
        /// compute cost moves from O(all prisms)/recompute to O(growing)/frame.
        /// </summary>
        internal float CachedVolume { get; private set; }

        /// <summary>
        /// Recompute <see cref="CachedVolume"/> from the live transform. Mirrors
        /// <see cref="CurrentVolume"/> exactly. Cheap per call — the whole point is to
        /// call it O(growing) times per frame instead of O(all prisms) per volume
        /// recompute.
        /// </summary>
        internal void RefreshVolumeCache()
        {
            if (destroyed)
            {
                CachedVolume = 0f;
            }
            else
            {
                float v = scaleAnimator ? scaleAnimator.GetCurrentVolume() : 0f;
                CachedVolume = v > 0f ? v : Mathf.Max(prismProperties?.volume ?? 0f, 0f);
            }

            // Mirror into the spatial index's cell-volume summation view so the
            // cell's Burst recompute (PrismSpatialIndex.SumCellVolumes) reads live
            // volumes — same O(growing)/frame cadence as this cache itself. No-op
            // during the spawn window (Register seeds the slot from CachedVolume).
            if (SpatialIndexId >= 0)
                PrismSpatialIndex.Instance?.UpdateCellVolume(SpatialIndexId, CachedVolume);
        }

        // Growth Methods
        public void Grow(float amount = 1) => scaleAnimator.Grow(amount);

        // Collision Handling
        protected void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent(out CellItem cellItem))
            {
                if (!prismProperties.IsShielded)
                    ActivateShield();
            }
        }

        protected void OnTriggerExit(Collider other)
        {
            if (other.gameObject.IsLayer("Crystals"))
                ActivateShield(2.0f);
        }

        protected virtual GameObject SetupDestruction(Domains domain, string attackerPlayerName, bool devastate = false)
        {
            var destructionScale = transform.lossyScale; // Use lossyScale to get the actual world scale, which accounts for parent scaling

            if (scaleAnimator)
            {
                scaleAnimator.IsScaling = false;      
                scaleAnimator.enabled = false;       
            }

            blockCollider.enabled = false;
            if (_shieldProxyCollider) _shieldProxyCollider.enabled = false;
            SetRenderVisible(false);

            prismProperties.volume = Mathf.Max(scaleAnimator ? scaleAnimator.GetCurrentVolume() : 1f, 1f);

            destroyed = true;
            devastated = devastate;

            // Destroyed mass weighs nothing in the per-domain sums — drop it from the
            // cache immediately (the cell may aggregate before the index unbinds it).
            CachedVolume = 0f;

            // Mark destroyed in the spatial index: the AOE Burst job skips this
            // prism, its occupancy bucket frees so growth can fill the site, and
            // it leaves the cell's density grids - destroyed mass must stop
            // attracting fauna, and the cell's LiveBlockCount must fall so the
            // phase system can descend (the consumption half of the oscillation).
            if (SpatialIndexId >= 0)
                PrismSpatialIndex.Instance?.MarkDestroyed(SpatialIndexId);

            _onTrailBlockDestroyedEventChannel.Raise(new PrismStats
            {
                OwnName = PlayerName,
                Volume = prismProperties.volume,
                AttackerName = attackerPlayerName,
            });

            _lastDestructionScale = destructionScale;
            return null;
        }

        /// <summary>
        /// The gameplay SFX category played when this prism is destroyed (exploded or imploded).
        /// Defaults to <see cref="GameplaySFXCategory.BlockDestroy"/>; subclasses override to
        /// substitute a dedicated sound (e.g. flora health prisms play FloraCollision).
        /// </summary>
        protected virtual GameplaySFXCategory DestructionSFX => GameplaySFXCategory.BlockDestroy;

        /// <summary>
        /// Plays the destruction one-shot for this prism. Uses <see cref="DestructionSFX"/>
        /// (BlockDestroy, or a subclass's specialized sound such as flora's FloraCollision),
        /// but substitutes CreatureBlockHit when a creature (fauna) caused the destruction AND
        /// the prism would otherwise play the generic BlockDestroy. This makes specialized
        /// sounds (flora) win over the creature sound, while plain blocks eaten by creatures
        /// get the creature sound.
        /// </summary>
        void PlayDestructionSFX()
        {
            var sfx = DestructionSFX;
            if (_destroyedByCreature && sfx == GameplaySFXCategory.BlockDestroy)
                sfx = GameplaySFXCategory.CreatureBlockHit;
            AudioSystem.Instance?.PlayGameplaySFX(sfx, transform.position);
        }

        // Explosion Methods
        protected virtual void Explode(Vector3 impactVector, Domains domain, string playerName, bool devastate = false)
        {
            SetupDestruction(domain, playerName, devastate);
            PlayDestructionSFX();

            var returnData = OnBlockImpactedEventChannel.RaiseEvent(new PrismEventData
            {
                ownDomain = Domain,
                SpawnPosition = transform.position,
                Rotation = transform.rotation,
                Scale = _lastDestructionScale,
                Velocity = impactVector / prismProperties.volume,
                PrismType = PrismType.Explosion
            });
        }

        // Implosion Methods
        protected virtual void Implode(Transform targetTransform, Domains domain, string playerName, bool devastate = false)
        {
            SetupDestruction(domain, playerName, devastate);
            PlayDestructionSFX();

            var returnData = OnBlockImpactedEventChannel.RaiseEvent(new PrismEventData
            {
                ownDomain = Domain,
                SpawnPosition = transform.position,
                Rotation = transform.rotation,
                Scale = _lastDestructionScale,
                TargetTransform = targetTransform,
                Volume = prismProperties.volume,
                PrismType = PrismType.Implosion
            });
        }

        // Idempotency guard for Damage/Consume. Multiple destruction sources can resolve
        // against the same prism in a single frame: two fauna iterating cached
        // OverlapSphere snapshots, AOE damage, and trail collisions all hit the same
        // prism before the disabled collider drops out of the next physics tick. Without
        // this gate, the second hit re-runs SetupDestruction (re-raising the destroyed
        // event, bumping AOE registry state) and Implode/Explode spawns a duplicate VFX
        // that has to play out its full duration in PrismEffectsManager - that's the
        // accumulating "garbage" the user observes (fauna swarm-eat trail blocks → 64-128
        // concurrent implosions). Once destroyed, hits become no-ops until Restore /
        // ResetState clears the flag.
        public void Damage(Vector3 impactVector, Domains domain, string playerName, bool devastate = false, bool byCreature = false)
        {
            if (destroyed) return;
            // Super-shielded prisms are fully invulnerable. No damage source
            // currently breaks them; ways to break them will be added later.
            // The impactor's other effect SOs (sparks, sound) still fire on
            // OnTriggerEnter, so the hit reads visually without state change.
            if (prismProperties.IsSuperShielded) return;
            if (prismProperties.IsShielded && !devastate)
                DeactivateShields();
            else
            {
                _destroyedByCreature = byCreature;
                Explode(impactVector, domain, playerName, devastate);
            }
        }

        public void Consume(Transform target, Domains domain, string playerName, bool devastate = false, bool byCreature = false)
        {
            if (destroyed) return;
            if (prismProperties.IsSuperShielded) return;
            if (prismProperties.IsShielded && !devastate)
                DeactivateShields();
            else
            {
                _destroyedByCreature = byCreature;
                Implode(target, domain, playerName, devastate);
            }
        }

        // State Management Methods
        public void MakeDangerous() => stateManager?.MakeDangerous();
        public void DeactivateShields() => stateManager?.DeactivateShields();
        public void ActivateShield() => stateManager?.ActivateShield();
        public void ActivateShield(float duration) => stateManager?.ActivateShield(duration);
        public void ActivateSuperShield() => stateManager?.ActivateSuperShield();
        public void SetTransparency(bool transparent) => materialAnimator?.SetTransparency(transparent);

        // Team Management Methods
        public void Steal(string playerName, Domains domain, bool superSteal = false) =>
            teamManager.Steal(playerName, domain, superSteal);
        public void ChangeTeam(Domains domain) => teamManager?.ChangeTeam(domain);
        
        public void RegisterProjectileCreated(string playerName)
        {
            if (string.IsNullOrEmpty(playerName))
                playerName = DEFAULT_PLAYER_NAME;

            PlayerName = playerName;
            ownerID    = playerName;

            prismProperties.position = transform.position;
            prismProperties.prism    = this;
            prismProperties.Trail    = Trail;
            prismProperties.TimeCreated = Time.time;
            prismProperties.volume   = Mathf.Max(scaleAnimator ? scaleAnimator.GetCurrentVolume() : 1f, 1f);

            int defaultLayer = LayerMask.NameToLayer(prismProperties.DefaultLayerName);
            if (defaultLayer >= 0)
                gameObject.layer = defaultLayer;
            else
                Debug.LogWarning($"[Prism] '{name}' has an invalid PrismProperties.DefaultLayerName '{prismProperties.DefaultLayerName}' — keeping layer '{LayerMask.LayerToName(gameObject.layer)}'.", this);
            _onTrailBlockCreatedEventChannel.Raise(new PrismStats
            {
                OwnName = PlayerName,
                Volume     = prismProperties.volume,
            });
        }

        // Restoration
        public void Restore()
        {
            if (!devastated)
            {
                _onTrailBlockRestoredEventChannel.Raise(new PrismStats
                {
                    OwnName = PlayerName,
                    Volume = prismProperties.volume,
                    AttackerName = prismProperties.prism.PlayerName,
                });

                // Re-enter the spatial index: batch AOE damage, growth occupancy,
                // and the cell density grids all resume seeing this mass.
                // (Pre-unification bug: Restore never told the AOE registry,
                // leaving restored prisms permanently invisible to it.) A prism
                // killed inside its spawn window never registered at all -
                // CreateBlockCoroutine bailed before Register - so restoring one
                // does a full registration instead, keeping every view consistent.
                if (SpatialIndexId >= 0)
                {
                    PrismSpatialIndex.Instance?.MarkRestored(SpatialIndexId);
                }
                else
                {
                    var spatialIndex = PrismSpatialIndex.EnsureInstance();
                    if (spatialIndex != null && spatialIndex.IsAvailable)
                        SpatialIndexId = spatialIndex.Register(this);
                }

                // Restoration owns the collider again — clear any stale LOD-cull
                // bookkeeping from the pre-destruction life, or NotifyPrismActivated's
                // cull below would early-out on _lodCulled==true (and a later restore
                // would reinstate a stale pre-cull snapshot).
                _lodCulled = false;
                _colliderLifecycleReady = true;
                ApplyShieldDesignatedCollider(); // box, or the shield AABB proxy if still engaged
                SetRenderVisible(true);
                destroyed = false;

                // Restored mass weighs again — re-seed the cache (destroyed=false now).
                RefreshVolumeCache();

                // Same contract as the spawn window: the transition-based LOD
                // sweep must classify this just-restored collider.
                PrismColliderLodManager.NotifyPrismActivated(this);
            }
        }

        /// <summary>
        /// Movers (gyroid bonding steering a block into a bond site) must call this
        /// so the spatial index's stored position - read by AOE damage queries and
        /// growth occupancy probes - tracks the transform. Cheap when the occupancy
        /// bucket is unchanged.
        /// </summary>
        public void NotifyPositionChanged()
        {
            if (SpatialIndexId >= 0)
                PrismSpatialIndex.Instance?.UpdatePosition(SpatialIndexId, transform.position);

            // Movers (gyroid steering, fauna body prisms) must also keep the
            // companion render entity's matrix honest — same contract as the
            // spatial index position.
            SyncRenderTransform();
        }

        private void OnDisable()
        {
            // Pool return / deactivation: a pooled-but-not-yet-reused prism must
            // not keep taking AOE damage, blocking growth at its stale position,
            // attracting fauna, or holding up the cell's LiveBlockCount until its
            // next reuse - Unregister drops every view, including the cell
            // density-grid binding. (Pre-unification, cleanup waited for the next
            // Initialize → ResetState, leaving a live-looking entry behind for
            // the whole pool dwell time.)
            if (SpatialIndexId >= 0)
            {
                PrismSpatialIndex.Instance?.Unregister(SpatialIndexId);
                SpatialIndexId = -1;
            }

            // The companion entity is not tied to GameObject activation — hide it
            // explicitly so a pooled prism can't keep drawing while it waits for
            // reuse. (The next Initialize re-establishes visibility.) Queued: the
            // batch flush applies it this frame at LateUpdate, before rendering.
            if (PrismRenderService.IsHandleUsable(in RenderHandle))
                PrismRenderService.QueueVisible(in RenderHandle, false);
        }

        private void OnDestroy()
        {
            // No material cleanup needed - we use sharedMaterial exclusively,
            // so no per-instance material clones are created.

            if (teamManager)
                teamManager.OnTeamChanged -= HandleTeamChangedForCell;

            // Scene teardown / explicit Destroy: don't leave a stale entry in any
            // index view (AOE, occupancy, cell grids and LiveBlockCount).
            if (SpatialIndexId >= 0)
            {
                PrismSpatialIndex.Instance?.Unregister(SpatialIndexId);
                SpatialIndexId = -1;
            }

            // Companion render entity dies with its prism.
            PrismRenderService.Destroy(ref RenderHandle);
        }
    }
}