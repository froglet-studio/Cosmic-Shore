using System;
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

        [Header("Prism Status")] 
        public bool destroyed;
        public bool devastated;
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

        /// <summary>
        /// Index into PrismSpatialIndex's contiguous NativeArray — the canonical
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

        private void Awake()
        {
            materialAnimator = GetComponent<MaterialPropertyAnimator>();
            scaleAnimator = GetComponent<PrismScaleAnimator>();
            teamManager = GetComponent<PrismTeamManager>();
            stateManager = GetComponent<PrismStateManager>();
            meshRenderer = GetComponent<MeshRenderer>();
            meshFilter = GetComponent<MeshFilter>();
            blockCollider = GetComponent<BoxCollider>();

            scaleAnimator.GrowthRate = growthRate;
            InitializePrismProperties();

            // Keep the cell's per-domain grids consistent when this prism changes
            // hands (steal / ChangeTeam): the cell re-files it under the new domain.
            if (teamManager)
                teamManager.OnTeamChanged += HandleTeamChangedForCell;
        }

        // --- Cell density-grid domain forwarding -------------------------------
        // (Registration itself lives in PrismSpatialIndex since Phase 3 — the
        // index binds/releases the cell grids at Register/MarkDestroyed/
        // MarkRestored/Unregister, so the fine and coarse views share one stream.)

        void HandleTeamChangedForCell(Domains oldDomain, Domains newDomain)
        {
            // The spatial index owns the cell density-grid binding (Phase 3 — see
            // Docs/SPATIAL_INDEX.md): forward the steal / ChangeTeam so the bound
            // cell re-files this prism under the new domain. No-op while
            // unregistered (spawn window) or unbound (fauna body, open space).
            if (SpatialIndexId >= 0)
                PrismSpatialIndex.Instance?.ForwardDomainChangeToCell(SpatialIndexId);
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
                PrismRenderService.SetVisible(in RenderHandle, show);
            }
            else
            {
                if (PrismRenderService.IsHandleUsable(in RenderHandle))
                    PrismRenderService.SetVisible(in RenderHandle, false);
                if (meshRenderer) meshRenderer.enabled = _renderVisible;
            }
        }

        void EnsureRenderEntity()
        {
            if (PrismRenderService.IsHandleUsable(in RenderHandle)) return;
            if (!meshRenderer || !meshFilter) return;
            if (meshFilter.sharedMesh == null || meshRenderer.sharedMaterial == null) return;
            RenderHandle = PrismRenderService.Create(
                meshFilter.sharedMesh, meshRenderer.sharedMaterial,
                transform.localToWorldMatrix, gameObject.layer);
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
            // Unregister from the spatial index — drops the AOE entry, the
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
            _lodCulled = false; // pool reuse: Initialize owns the collider again
            IsSmallest = false;
            IsLargest = false;
            
            // Clear trail renderer to prevent visual artifacts across the map
            if (Trail != null && Trail.TrailRenderer != null)
            {
                Trail.TrailRenderer.Clear();
            }
            
            // Ensure physics/rendering are off until Coroutine enables them
            if (blockCollider) blockCollider.enabled = false;
            SetRenderVisible(false);
            
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
            gameObject.layer = LayerMask.NameToLayer(prismProperties.DefaultLayerName);

            prismProperties.volume = 1f;
        }

        private IEnumerator CreateBlockCoroutine(Vector3 authoredTargetScale)
        {
            yield return new WaitForSeconds(waitTime);

            // Destroyed before creation completed (e.g. AOE within waitTime of spawn) —
            // don't resurrect the renderer/collider or register a dead prism with the
            // AOE registry and cell grids.
            if (destroyed) yield break;

            SetRenderVisible(true);
            blockCollider.enabled = true;

            if (scaleAnimator.TargetScale == Vector3.zero)
                scaleAnimator.SetTargetScale(authoredTargetScale);

            prismProperties.volume = scaleAnimator.GetCurrentVolume();

            scaleAnimator.BeginGrowthAnimation();

            _onTrailBlockCreatedEventChannel.Raise(new PrismStats
            {
                OwnName = PlayerName,
                Volume = prismProperties.volume,
            });

            // Register with the spatial index — one registration, every view:
            // cache-friendly batch AOE processing, growth occupancy (consumes the
            // TryReserve claim that protected this site through the
            // disabled-collider window), neighborhood queries, and the containing
            // cell's density grids. The cell binding is what makes trail mass
            // visible to fauna anti-domain targeting and the cell's phase system;
            // fauna bodies are excluded from that view inside the index.
            var spatialIndex = PrismSpatialIndex.EnsureInstance();
            if (spatialIndex != null && spatialIndex.IsAvailable)
                SpatialIndexId = spatialIndex.Register(this);

            // The LOD sweep is transition-based — it must be told about colliders
            // that come online between ticks, or a prism born far from every focus
            // keeps its collider until a bubble boundary happens to cross it.
            PrismColliderLodManager.NotifyPrismActivated(this);
        }

        /// <summary>
        /// This prism's LIVE volume (world-scale product), the unit of mass the
        /// ecosystem runs on — "volume is the spine" (CLAUDE.md ▸ Ecosystem Design
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

        /// <summary>
        /// Called by <c>PrismColliderLodManager</c> ONLY. Culls/restores this prism's
        /// collider by vessel/projectile proximity. Idempotent; destruction and the
        /// spawn window own the collider outright (a culled prism that gets destroyed
        /// stays collider-off; Restore re-enables directly and the next LOD tick
        /// re-evaluates).
        /// </summary>
        public void SetColliderCulledByLod(bool culled)
        {
            if (_lodCulled == culled) return;
            if (!blockCollider) return;
            if (culled)
            {
                _colliderBeforeLodCull = blockCollider.enabled;
                blockCollider.enabled = false;
            }
            else if (!destroyed && _colliderBeforeLodCull)
            {
                blockCollider.enabled = true;
            }
            _lodCulled = culled;
        }

        /// <summary>True while this prism's collider is enabled (LOD telemetry).</summary>
        public bool ColliderEnabled => blockCollider && blockCollider.enabled;

        public float CurrentVolume
        {
            get
            {
                if (destroyed) return 0f;
                // GetCurrentVolume reads the live transform but returns 0 while the
                // animator component is disabled — which Restore() leaves it as. Fall
                // back to the bookkept volume (stamped at create/destroy) so restored
                // mass still weighs what it did, rather than vanishing from the sums.
                float v = scaleAnimator ? scaleAnimator.GetCurrentVolume() : 0f;
                if (v > 0f) return v;
                return Mathf.Max(prismProperties?.volume ?? 0f, 0f);
            }
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
            SetRenderVisible(false);

            prismProperties.volume = Mathf.Max(scaleAnimator ? scaleAnimator.GetCurrentVolume() : 1f, 1f);

            destroyed = true;
            devastated = devastate;

            // Mark destroyed in the spatial index: the AOE Burst job skips this
            // prism, its occupancy bucket frees so growth can fill the site, and
            // it leaves the cell's density grids — destroyed mass must stop
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

        // Explosion Methods
        protected virtual void Explode(Vector3 impactVector, Domains domain, string playerName, bool devastate = false)
        {
            SetupDestruction(domain, playerName, devastate);
            AudioSystem.Instance?.PlayGameplaySFX(GameplaySFXCategory.BlockDestroy, transform.position);

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
            AudioSystem.Instance?.PlayGameplaySFX(GameplaySFXCategory.BlockDestroy, transform.position);

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
        // that has to play out its full duration in PrismEffectsManager — that's the
        // accumulating "garbage" the user observes (fauna swarm-eat trail blocks → 64-128
        // concurrent implosions). Once destroyed, hits become no-ops until Restore /
        // ResetState clears the flag.
        public void Damage(Vector3 impactVector, Domains domain, string playerName, bool devastate = false)
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
                Explode(impactVector, domain, playerName, devastate);
        }

        public void Consume(Transform target, Domains domain, string playerName, bool devastate = false)
        {
            if (destroyed) return;
            if (prismProperties.IsSuperShielded) return;
            if (prismProperties.IsShielded && !devastate)
                DeactivateShields();
            else
                Implode(target, domain, playerName, devastate);
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

            gameObject.layer = LayerMask.NameToLayer(prismProperties.DefaultLayerName);
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
                // killed inside its spawn window never registered at all —
                // CreateBlockCoroutine bailed before Register — so restoring one
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

                blockCollider.enabled = true;
                SetRenderVisible(true);
                destroyed = false;

                // Same contract as the spawn window: the transition-based LOD
                // sweep must classify this just-restored collider.
                PrismColliderLodManager.NotifyPrismActivated(this);
            }
        }

        /// <summary>
        /// Movers (gyroid bonding steering a block into a bond site) must call this
        /// so the spatial index's stored position — read by AOE damage queries and
        /// growth occupancy probes — tracks the transform. Cheap when the occupancy
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
            // next reuse — Unregister drops every view, including the cell
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
            // reuse. (The next Initialize re-establishes visibility.)
            if (PrismRenderService.IsHandleUsable(in RenderHandle))
                PrismRenderService.SetVisible(in RenderHandle, false);
        }

        private void OnDestroy()
        {
            // No material cleanup needed — we use sharedMaterial exclusively,
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