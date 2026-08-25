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
        // DO NOT shorten without claim-before-spawn on trail/env lays:
        // PrismSpatialIndex.TryReserve is growth/assembler-only today — PrismTrailBuilder.LayOne
        // and PrismFactory still rely on this disable window as the sole spawn-site protection.
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

        // Projectile-immunity window (Time.time deadline): while it is in the future,
        // NO projectile impacts this prism (Projectile.DisallowImpactOnPrism). The
        // Sparrow turret stamps it on each fired prism — covering the flight (the prism
        // is parked live at the anchor the whole way) plus a short settle after
        // placement — so a shot cannot destroy its own delivery and a spray does not
        // erase its own freshest output. 0 = no immunity. Non-serialized; cleared on
        // pool reuse in ResetState.
        public float ProjectileImmuneUntil { get; set; }

        [Header("Event Channels")]
        [SerializeField] ScriptableEventPrismStats _onTrailBlockCreatedEventChannel;
        [SerializeField] ScriptableEventPrismStats _onTrailBlockDestroyedEventChannel;
        [SerializeField] ScriptableEventPrismStats _onTrailBlockRestoredEventChannel;
        [SerializeField] internal PrismEventChannelWithReturnSO OnBlockImpactedEventChannel;

        public Action<Prism> OnReturnToPool;
        private Vector3 _lastDestructionScale = Vector3.one;

        // Authored BoxCollider size, cached in Awake so ResetState can restore it on
        // pool reuse (a prior life must not leak an inflated size into the next).
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

        // Shell-geometry sources for the spatial index's shell view (the analytic
        // shielded-collision tier). The octahedron shield is auto-added to every
        // prism by PrismStateManager.Awake; the stellated shield appears lazily at
        // the first super-shield engage, so the lookup re-runs when super-shielded
        // with no cached stella component.
        PrismOctahedronShield _octaShellSource;
        PrismStellatedOctahedronShield _stellaShellSource;
        bool _shellSourcesLookedUp;

        /// <summary>
        /// Local-space shell geometry for the spatial index's shell view: the
        /// engaged shell's center (authored BoxCollider center) and semi-axes
        /// (shieldScale × authored half-extents), in the prism's LOCAL frame —
        /// the index applies the live world transform. Reads the shield
        /// components' Awake-cached geometry (never the live BoxCollider.size,
        /// which must stay at the authored size for shell geometry).
        /// </summary>
        internal bool TryGetShellGeometry(out Vector3 centerLocal, out Vector3 semiAxesLocal)
        {
            bool super = prismProperties is { IsSuperShielded: true };
            if (!_shellSourcesLookedUp || (super && _stellaShellSource == null))
            {
                _shellSourcesLookedUp = true;
                TryGetComponent(out _octaShellSource);
                TryGetComponent(out _stellaShellSource);
            }

            if (super && _stellaShellSource != null)
            {
                centerLocal = _stellaShellSource.ShellCenterLocal;
                semiAxesLocal = _stellaShellSource.ShellSemiAxesLocal;
                return true;
            }

            if (_octaShellSource != null)
            {
                centerLocal = _octaShellSource.ShellCenterLocal;
                semiAxesLocal = _octaShellSource.ShellSemiAxesLocal;
                return true;
            }

            if (_authoredColliderSizeCached)
            {
                centerLocal = blockCollider != null ? blockCollider.center : Vector3.zero;
                semiAxesLocal = _authoredColliderSize * (0.5f * OctahedronMeshGenerator.CIRCUMSCRIBING_SCALE);
                return true;
            }

            centerLocal = default;
            semiAxesLocal = default;
            return false;
        }


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

        /// <summary>True while per-prism-unique geometry (a shield engage morph, a shatter
        /// overlay) is drawn by this GameObject's MeshRenderer instead of the companion
        /// entity. A one-shot clock stamp made in this window is spent invisibly, so stamp
        /// sites that can defer should check this ALONE — <see cref="UsesEntityColorSink"/>
        /// bundles it with handle usability and is the wrong test for that question.</summary>
        internal bool ExoticVisualActive => _exoticVisualActive;

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

        /// <summary>
        /// True while this prism's grow-in animation is still running (scale has not settled at
        /// TargetScale). A deactivated prism reports false — pooled/consumed prisms must never
        /// wedge a caller waiting on growth (PrismTrailBuilder's arena-ready gate sweeps on this).
        /// </summary>
        public bool IsGrowing => isActiveAndEnabled && scaleAnimator != null && scaleAnimator.IsVisuallyGrowing;

        /// <summary>
        /// True once CreateBlockCoroutine has finished this life's creation — renderer visible,
        /// collider on, spatial index registered. Until then the prism EXISTS but cannot be seen
        /// (creation completions are budgeted per frame to de-spike simultaneous spawns), which
        /// is why scale alone can never prove a prism is on screen.
        /// </summary>
        public bool IsCreationComplete { get; private set; }

        /// <summary>
        /// True when this prism is exactly what the player will see for the rest of the match:
        /// created (visible), not animating, and settled at its target scale — or dead, which
        /// can never pop in later. THE per-prism predicate behind PrismTrailBuilder's
        /// arena-ready gate; anything short of this can still visibly appear or change after
        /// the connecting screen drops.
        /// </summary>
        public bool IsSettledForReveal =>
            destroyed ||
            scaleAnimator == null ||
            (IsCreationComplete && scaleAnimator.IsVisuallySettled);

        /// <summary>
        /// Snap this prism's grow-in to its final scale NOW (loading-screen / emergency use —
        /// the world must be covered). The arena-ready gate no longer force-snaps; it waits on
        /// <see cref="IsSettledForReveal"/> / <see cref="AnalyticGrowSettleTime"/>. No-op until
        /// creation completes: CreateBlockCoroutine owns the pre-visibility state and must not
        /// be raced.
        /// </summary>
        public void CompleteGrowthImmediately()
        {
            if (destroyed || !IsCreationComplete) return;
            scaleAnimator?.CompleteImmediately();
        }

        /// <summary>
        /// Analytic <see cref="PrismClock"/> time when the GPU grow bloom settles, or 0 when no
        /// clock stamp is active. Used by the arena-ready gate to wait without force-snapping.
        /// </summary>
        public float AnalyticGrowSettleTime =>
            scaleAnimator != null ? scaleAnimator.AnalyticSettleTime : 0f;

        public Vector3 MaxScale
        {
            get => scaleAnimator?.MaxScale ?? Vector3.one * 10f;
            set
            {
                if (scaleAnimator is not null) scaleAnimator.MaxScale = value;
            }
        }

        /// <summary>Widens this prism's scale-constraint window so an AUTHORED size survives
        /// <see cref="TargetScale"/>'s per-axis clamp. See PrismScaleAnimator.AdmitTargetScale.</summary>
        public void AdmitTargetScale(Vector3 target)
        {
            if (scaleAnimator is not null) scaleAnimator.AdmitTargetScale(target);
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

        private void Awake()
        {
            materialAnimator = GetComponent<MaterialPropertyAnimator>();
            scaleAnimator = GetComponent<PrismScaleAnimator>();
            teamManager = GetComponent<PrismTeamManager>();
            stateManager = GetComponent<PrismStateManager>();
            meshRenderer = GetComponent<MeshRenderer>();
            meshFilter = GetComponent<MeshFilter>();
            blockCollider = GetComponent<BoxCollider>();

            // The prism's stable render identity — see _authoredMesh. Captured before
            // any shield component can swap the MeshFilter to morph geometry.
            if (meshFilter) _authoredMesh = meshFilter.sharedMesh;

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

            // Entity EXISTENCE is deliberately independent of which path currently
            // DRAWS. Clock stamps are one-shot initial-conditions writes
            // (Docs/PRISM_ANIMATION.md §4): a prism with no companion entity at the
            // instant it is stamped loses that animation permanently. Gating creation
            // on !_exoticVisualActive meant any prism whose shield engage-morph
            // straddled its creation reveal (every shielded/super-shielded
            // environment-laid prism — the C13 repro) had nothing to stamp and snapped.
            // The entity is simply created hidden while the exotic visual draws, and
            // queued visible when rendering returns to the instanced path.
            if (show && PrismRenderService.Enabled)
                EnsureRenderEntity();

            bool entityPath = !_exoticVisualActive && PrismRenderService.IsHandleUsable(in RenderHandle);
            if (entityPath)
            {
                if (meshRenderer && meshRenderer.enabled) meshRenderer.enabled = false;
                if (show)
                {
                    // Re-assert geometry first: an entity created during an exotic
                    // window holds the authored mesh, and a shield that disengages
                    // without ever setting an override leaves ClearRenderMeshOverride
                    // with nothing to push.
                    SyncRenderMesh();
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

        // The prefab's own mesh, cached at Awake. This is the prism's STABLE render
        // identity: while an exotic visual is animating, meshFilter.sharedMesh holds a
        // per-prism morph mesh, and registering that with Entities Graphics would mint
        // a unique BatchMeshID per prism — a draw-call storm plus a registration leak.
        Mesh _authoredMesh;

        // Why the last EnsureRenderEntity declined. Assigned from const literals only
        // (the decline path runs per prism whenever the instanced path is off, so it
        // must not allocate); the service StatusLine is composed only when a
        // diagnostic actually asks. Null = no decline recorded.
        string _renderEntityDecline;

        // The mesh the companion entity currently holds — lets SyncRenderMesh skip the
        // ECS write (the overwhelmingly common case) instead of re-pushing every show.
        Mesh _renderEntityMesh;

        /// <summary>
        /// The prism's batchable render geometry: the settled shield override wins;
        /// otherwise the live prism mesh — except while an exotic visual owns rendering,
        /// where <c>meshFilter.sharedMesh</c> is transient per-prism morph geometry that
        /// must never reach Entities Graphics (see <see cref="_authoredMesh"/>).
        ///
        /// Internal rather than private because it is also the right answer for anything
        /// sizing a clock animation's <c>RenderBounds</c> envelope: a shielded prism must
        /// be measured against its octahedron, not the box it would otherwise report.
        /// </summary>
        internal Mesh EffectiveRenderMesh()
        {
            if (_renderMeshOverride != null) return _renderMeshOverride;
            if (_exoticVisualActive) return _authoredMesh;
            var live = meshFilter != null ? meshFilter.sharedMesh : null;
            return live != null ? live : _authoredMesh;
        }

        void EnsureRenderEntity()
        {
            if (PrismRenderService.IsHandleUsable(in RenderHandle)) { _renderEntityDecline = null; return; }
            if (!meshRenderer || !meshFilter) { _renderEntityDecline = "the prism has no MeshRenderer/MeshFilter"; return; }

            var renderMesh = EffectiveRenderMesh();
            if (renderMesh == null) { _renderEntityDecline = "no mesh to register (MeshFilter.sharedMesh and the authored mesh are both null)"; return; }
            var renderMaterial = meshRenderer.sharedMaterial;
            if (renderMaterial == null) { _renderEntityDecline = "MeshRenderer.sharedMaterial is null"; return; }

            RenderHandle = PrismRenderService.Create(
                renderMesh, renderMaterial,
                transform.localToWorldMatrix, gameObject.layer);

            if (PrismRenderService.IsHandleUsable(in RenderHandle))
            {
                _renderEntityDecline = null;
                _renderEntityMesh = renderMesh;
            }
            else
            {
                _renderEntityDecline = "PrismRenderService.Create declined (no ECS world / EntitiesGraphicsSystem, or the master toggle is off)";
                _renderEntityMesh = null;
            }
        }

        /// <summary>
        /// Last-chance companion-entity creation from a clock STAMP site. Clock stamps
        /// are one-shot initial-conditions writes (Docs/PRISM_ANIMATION.md §4) — miss
        /// the instant and that animation is gone for this life — so every stamp site
        /// gets exactly one self-heal before the strict-mode diagnostics fire.
        /// Returns true when a usable entity exists afterwards.
        /// </summary>
        internal bool TryEnsureRenderEntityForStamp()
        {
            if (PrismRenderService.IsHandleUsable(in RenderHandle)) return true;
            if (!PrismRenderService.Enabled || !gameObject.activeInHierarchy) return false;

            EnsureRenderEntity();
            if (!PrismRenderService.IsHandleUsable(in RenderHandle)) return false;

            // A freshly minted entity is born hidden — re-run the path so it inherits
            // this prism's current visibility/mesh/material/transform immediately
            // instead of waiting for the next lifecycle event.
            ApplyRenderPath();
            return true;
        }

        /// <summary>
        /// One-line, allocation-on-demand diagnosis of this prism's companion-entity
        /// state — quoted by the strict-mode stamp diagnostics so a single repro run
        /// names the exact broken gate instead of listing suspects.
        /// </summary>
        internal string DescribeRenderEntityState()
        {
            if (PrismRenderService.IsHandleUsable(in RenderHandle)) return "companion entity is live";
            if (!PrismRenderService.Enabled)
                return $"instanced render path is OFF [service: {PrismRenderService.StatusLine()}]";
            if (!gameObject.activeInHierarchy) return "the prism GameObject is inactive in the hierarchy";
            if (_renderEntityDecline != null)
                return $"EnsureRenderEntity declined: {_renderEntityDecline} [service: {PrismRenderService.StatusLine()}]";
            return $"EnsureRenderEntity was never reached (renderVisible={_renderVisible}, " +
                   $"exoticVisual={_exoticVisualActive}) [service: {PrismRenderService.StatusLine()}]";
        }

        /// <summary>Renders the companion entity with a shared mesh (settled octahedron
        /// shield) in place of the prism's own mesh. Pair with SetExoticVisualActive(false)
        /// so the entity path re-engages.</summary>
        internal void SetRenderMeshOverride(Mesh sharedMesh)
        {
            _renderMeshOverride = sharedMesh;
            SyncRenderMesh();
        }

        /// <summary>Returns the companion entity to the prism's own mesh (disengage /
        /// pool reuse). Safe to call when no override is active.</summary>
        internal void ClearRenderMeshOverride()
        {
            if (_renderMeshOverride == null) return;
            _renderMeshOverride = null;
            if (!PrismRenderService.IsHandleUsable(in RenderHandle)) return;
            SyncRenderMesh();
            SyncRenderMaterial();
            SyncRenderTransform();
        }

        /// <summary>
        /// Re-asserts the entity's mesh from this prism's STABLE geometry (settled
        /// shield override, else the live prism mesh, else the authored mesh). Called
        /// when the instanced path (re)engages: an entity created during an exotic
        /// window carries the authored mesh, and <see cref="ClearRenderMeshOverride"/>
        /// early-outs when no override was ever set — so without this a shield that
        /// engaged before the prism's first show could leave the entity drawing the
        /// wrong geometry.
        /// </summary>
        internal void SyncRenderMesh()
        {
            if (!PrismRenderService.IsHandleUsable(in RenderHandle)) return;
            var mesh = EffectiveRenderMesh();
            if (mesh == null || ReferenceEquals(mesh, _renderEntityMesh)) return;
            PrismRenderService.SetMesh(in RenderHandle, mesh);
            _renderEntityMesh = mesh;
        }

        /// <summary>Pushes the live transform to the companion entity. Called on
        /// show, at the growth stamp (transform goes final there), and by movers
        /// via NotifyPositionChanged.</summary>
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
            // Fail loud once per material if this prism cannot be dissolved by the
            // camera↔vessel occlusion corridor. Every material a prism ever binds passes
            // through here, so this is the enforcement point for §4.7's platform law —
            // an unfadeable prism is an invisible hole in the corridor, and silence is
            // exactly how the previous opt-in system stayed broken for so long.
            PrismOcclusionDiagnostics.VerifyCorridorCapable(meshRenderer.sharedMaterial, this);
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
            // Always refresh: clock color transitions bind the end-state material
            // at the stamp, and its authored values ARE the lerp targets.
            PrismRenderService.SetMaterial(in RenderHandle, meshRenderer.sharedMaterial, refreshColors: true);
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

            // Color continuity: on engage the displayed colors live on the outgoing
            // entity path — pin them onto the GameObject renderer so the handoff
            // frame can't flash a stale MaterialPropertyBlock. On disengage nothing
            // is needed: the entity's clock stamps kept ticking while the exotic
            // visual was shown, so it resumes at the analytically-correct colors.
            if (active)
                materialAnimator?.FlushDisplayedColorsToRenderer();
        }

        /// <summary>
        /// Drops this prism out of sight and out of collision for a BULK TRANSPORT (the Wanderway
        /// conveyor relocating a whole microscene's conserved stock). Not a death and not a
        /// despawn: the prism keeps its spatial-index registration and its mass, and re-enters
        /// through the standard creation bloom when the transport re-poses it.
        ///
        /// Only legitimate where the mass is provably unseen — the conveyor recycles a scene only
        /// once it is wholly outside the camera frustum, so nothing the player can watch vanishes
        /// (CLAUDE.md ▸ continuity of existence). Do NOT reach for this to hide live mass.
        /// </summary>
        public void HideForTransport()
        {
            if (blockCollider) blockCollider.enabled = false;
            SetRenderVisible(false);
        }

        /// <summary>
        /// Called when spawning from pool. Resets state and starts growth.
        /// </summary>
        public virtual void Initialize(string playerName = DEFAULT_PLAYER_NAME)
        {
            // [Fix] Always clean up previous state when coming from pool
            ResetState();
            ClearRenderMeshOverride(); // pooled reuse: the entity must not keep a prior life's shield mesh
            PrismRenderService.ClearPrismStamps(in RenderHandle); // nor a prior life's clock-animation stamps

            PlayerName = playerName;
            blockCollider.enabled = false;
            SetRenderVisible(false);
            IsCreationComplete = false; // this life is invisible until CreateBlockCoroutine finishes

            var authoredTargetScale = scaleAnimator ? scaleAnimator.TargetScale : transform.localScale;
            if (authoredTargetScale == Vector3.zero)
                authoredTargetScale = transform.localScale;

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
            // Pool reuse: a prism whose scale window was widened for an AUTHORED size
            // (AdmitTargetScale) must not carry that ceiling into its next life.
            scaleAnimator?.RestoreAuthoredScaleWindow();
            ProjectileImmuneUntil = 0f;   // pool reuse: immunity never survives into a new life

            // Pool-reuse safety: trail MEMBERSHIP never survives into a new life. A reused
            // prism kept its previous container here for years, and the consequences were
            // structural, not cosmetic: a vessel's wake block could wear a dead spawnable's
            // Trail, so the attach effect's Trail gate passed against the WRONG ribbon,
            // GetBlockIndex said "not a member" (-1) and refused the ride, and
            // PrismscapeTopology read a stale container's dimension. Every layer that puts a
            // prism IN a trail stamps it explicitly AFTER Initialize (AssignTrail) - the
            // builder and the vessel spawner both do.
            Trail = null;
            if (prismProperties != null) prismProperties.Trail = null;

            // Pool-reuse safety: no spawner requests super-shield via prismProperties
            // before Initialize (it's engaged post-spawn via ActivateSuperShield /
            // SegmentSpawner), so a set flag here is always a leak from the previous
            // life. Left set, this life registers as super-shielded in the spatial
            // index - invulnerable, and it kills any AOE explosion that touches it.
            // IsShielded/IsDangerous are NOT cleared: spawners set those pre-Initialize
            // as the requested state for this life.
            if (prismProperties != null) prismProperties.IsSuperShielded = false;
            // Pool reuse: this is also what INVALIDATES any super-shield deflection settle
            // still scheduled from the previous life. That callback compares its captured
            // stamp time against LastSuperShieldJiggleTime and no-ops on a mismatch, so
            // resetting here is what stops it resetting THIS life's culling envelope.
            _lastSuperShieldJiggleTime = float.NegativeInfinity;
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

            // Clock-material reuse safety: the previous life's stamp state must not
            // leak into this life's IsVisuallyGrowing / settle predicates.
            scaleAnimator?.ResetClockState();

            // Clear trail renderer to prevent visual artifacts across the map
            if (Trail != null && Trail.TrailRenderer != null)
            {
                Trail.TrailRenderer.Clear();
            }
            
            // Ensure physics/rendering are off until Coroutine enables them
            if (blockCollider) blockCollider.enabled = false;
            SetRenderVisible(false);

            // Pool-reuse safety: restore authored BoxCollider.size so a prior life cannot leak
            // an inflated size into the next instance.
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

        // Creation budget while the loading gate holds the connecting screen. At the gameplay
        // cap a 25k-prism arena would drain 6/frame for 60+ seconds AFTER the match starts —
        // the "prisms load in batches during play" bug. Behind the covered screen the de-spike
        // rationale is void (there is no visible frame to protect), so the queue drains in a
        // handful of frames instead. Gameplay frames keep the authored cap untouched.
        const int LoadGateCreationCompletionsPerFrame = 512;

        // Creation budget while a BULK TRANSPORT of already-existing mass is in flight (the
        // Wanderway conveyor re-posing a whole microscene's conserved stock into a fresh
        // arrangement). At the gameplay cap a 1,500-prism scene would trickle back into
        // existence over ~4 seconds — the player flies to the arrival point in less than that
        // and watches it assemble. This is NOT a load gate: frames are live, so the tier sits
        // an order of magnitude below the covered-screen budget and stays a slice, not a dump.
        // Bracketed by BeginBulkTransport/EndBulkTransport; the arrival is far away
        // (ConveyorConfig.MinPlacementDistance) so the faster drain is invisible either way.
        const int BulkTransportCreationCompletionsPerFrame = 64;
        static int s_bulkTransportsInFlight;

        // A play exit mid-transport skips the caller's finally, pinning the raised budget on
        // forever once domain reload no longer clears it.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetBulkTransportStatics() => s_bulkTransportsInFlight = 0;

        /// <summary>Open a bulk-transport bracket (raises the per-frame creation-completion
        /// budget). ALWAYS pair with <see cref="EndBulkTransport"/> in a finally.</summary>
        public static void BeginBulkTransport() => s_bulkTransportsInFlight++;

        /// <summary>Close a bulk-transport bracket.</summary>
        public static void EndBulkTransport() => s_bulkTransportsInFlight = Mathf.Max(0, s_bulkTransportsInFlight - 1);

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
            // While the load gate holds the connecting screen the world is covered and nothing is
            // playing, so the spawn-stagger wait is pure dead time on the critical path: it
            // delays every prism's creation completion by waitTime and leaves a tail after the
            // last prism is laid. Skipping it there lets creation drain WHILE laying continues
            // (the growth occupancy claim, not this timer, is what protects the spawn site).
            if (PrismTrailBuilder.IsLoadGateHolding)
            {
                yield return null;
            }
            else
            {
                if (_spawnWait == null || _spawnWaitSeconds != waitTime)
                {
                    _spawnWait = new WaitForSeconds(waitTime);
                    _spawnWaitSeconds = waitTime;
                }
                yield return _spawnWait;
            }

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
                int creationBudget = PrismTrailBuilder.IsLoadGateHolding
                    ? LoadGateCreationCompletionsPerFrame
                    : s_bulkTransportsInFlight > 0
                        ? BulkTransportCreationCompletionsPerFrame
                        : MaxCreationCompletionsPerFrame;
                if (s_creationCompletionsThisFrame < creationBudget)
                    break;

                yield return null;
                if (destroyed) yield break; // killed while waiting for budget
            }
            s_creationCompletionsThisFrame++;

            using (s_createVisibilityMarker.Auto())
            {
                SetRenderVisible(true);
                blockCollider.enabled = true;
            }
            IsCreationComplete = true; // visible from here — the arena-ready gate may now count this prism

            if (scaleAnimator.TargetScale == Vector3.zero)
                scaleAnimator.SetTargetScale(authoredTargetScale);

            prismProperties.volume = scaleAnimator.GetCurrentVolume();

            // Capture BEFORE BeginGrowthAnimation: on the clock path the stamp runs
            // the completion side effects at the start (the law), which writes the
            // FINAL volume into prismProperties and raises the volume-delta SOAP —
            // raising the created event with the mutated value would double-count
            // the mass (created=final + delta=final). The local preserves the
            // legacy accounting split (created≈0 + delta≈final) on both paths.
            float createdVolume = prismProperties.volume;

            scaleAnimator.BeginGrowthAnimation();

            using (s_createSoapMarker.Auto())
            {
                _onTrailBlockCreatedEventChannel.Raise(new PrismStats
                {
                    OwnName = PlayerName,
                    Volume = createdVolume,
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
        /// scale actually changes (the growth stamp — transform is final there — plus
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
            {
                var index = PrismSpatialIndex.Instance;
                if (index != null)
                {
                    index.UpdateCellVolume(SpatialIndexId, CachedVolume);
                    // A shielded prism whose scale just changed (growth stamp)
                    // changes its world shell too - re-capture it on the same
                    // cadence (single byte read no-op for the unshielded majority).
                    index.UpdateShellTransform(SpatialIndexId);
                }
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

        // Death-path split (Docs/PRISM_ANIMATION.md §4.6). AOE.ResolveDamage wraps a
        // whole drain, so everything a death does landed in ONE self-time bucket and
        // could not be attributed. These five name the phases a mass-death burst
        // actually spends its frame in (Setup NESTS SpatialIndex and StatRaise, so its
        // self time is the prism-local work); they sit on the per-death path
        // deliberately —
        // a disabled profiler makes each Begin/End a predicted branch, and without
        // them the only honest statement about the burst is "it costs something".
        static readonly ProfilerMarker s_destroySetupMarker = new("Prism.Destroy.Setup");
        static readonly ProfilerMarker s_destroySpatialMarker = new("Prism.Destroy.SpatialIndex");
        static readonly ProfilerMarker s_destroyStatMarker = new("Prism.Destroy.StatRaise");
        static readonly ProfilerMarker s_destroySfxMarker = new("Prism.Destroy.SFX");
        static readonly ProfilerMarker s_destroyEffectMarker = new("Prism.Destroy.EffectRequest");

        // Pose captured ONCE per death and reused by the effect request and the SFX.
        // transform.position/rotation/lossyScale are native property calls (lossyScale
        // walks the parent chain); a death read them four times over for one instant
        // that cannot change in between.
        private Vector3 _lastDestructionPosition;
        private Quaternion _lastDestructionRotation = Quaternion.identity;

        protected virtual GameObject SetupDestruction(Domains domain, string attackerPlayerName, bool devastate = false)
        {
            using var setupScope = s_destroySetupMarker.Auto();

            // lossyScale gets the actual world scale, which accounts for parent scaling.
            var destructionScale = transform.lossyScale;
            _lastDestructionPosition = transform.position;
            _lastDestructionRotation = transform.rotation;

            if (scaleAnimator)
            {
                scaleAnimator.enabled = false;
            }

            blockCollider.enabled = false;
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
            {
                using var spatialScope = s_destroySpatialMarker.Auto();
                PrismSpatialIndex.Instance?.MarkDestroyed(SpatialIndexId);
            }

            using (s_destroyStatMarker.Auto())
            {
                _onTrailBlockDestroyedEventChannel.Raise(new PrismStats
                {
                    OwnName = PlayerName,
                    Volume = prismProperties.volume,
                    AttackerName = attackerPlayerName,
                    OwnDomain = Domain,
                });
            }

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
            using var sfxScope = s_destroySfxMarker.Auto();
            var sfx = DestructionSFX;
            if (_destroyedByCreature && sfx == GameplaySFXCategory.BlockDestroy)
                sfx = GameplaySFXCategory.CreatureBlockHit;
            // Reuse the pose SetupDestruction just captured — this is the same instant.
            AudioSystem.Instance?.PlayGameplaySFX(sfx, _lastDestructionPosition);
        }

        // Explosion Methods
        protected virtual void Explode(Vector3 impactVector, Domains domain, string playerName, bool devastate = false,
                                       float debrisSpeedLimit = 0f)
        {
            // Read the tier BEFORE the destruction pass so the debris is guaranteed to carry the
            // state the prism was actually wearing on screen, whatever a subclass override or a
            // future SetupDestruction step does to the flags.
            var kind = PrismKinds.Of(this);

            SetupDestruction(domain, playerName, devastate);
            PlayDestructionSFX();

            // A supplied debrisSpeedLimit marks impactVector as a TRUE velocity (see
            // PrismEffectHelper.DamageProportional) - it is already the speed the debris
            // should leave at, so it passes through untouched.
            //
            // The legacy branch's divisor is worth understanding before trusting it:
            // SetupDestruction has already run, and it stands the scale animator down
            // BEFORE reading the volume. GetCurrentVolume() gates on `enabled` and reports
            // 0 once it is off, so Max(0, 1) makes prismProperties.volume exactly 1 for
            // EVERY prism regardless of size. The divide is therefore a no-op today and the
            // legacy gain is just `inertia`. Do not pre-multiply by a volume expecting it to
            // cancel here - it will not, and the result is a straight volume multiplier.
            using var effectScope = s_destroyEffectMarker.Auto();
            OnBlockImpactedEventChannel.RaiseEvent(new PrismEventData
            {
                ownDomain = Domain,
                SpawnPosition = _lastDestructionPosition,
                Rotation = _lastDestructionRotation,
                Scale = _lastDestructionScale,
                Velocity = debrisSpeedLimit > 0f ? impactVector : impactVector / prismProperties.volume,
                DebrisSpeedLimit = debrisSpeedLimit,
                Kind = kind,
                PrismType = PrismType.Explosion
            });
        }

        // Implosion Methods
        protected virtual void Implode(Transform targetTransform, Domains domain, string playerName, bool devastate = false)
        {
            // See Explode: captured pre-destruction so the suction wears the prism's own tier.
            var kind = PrismKinds.Of(this);

            SetupDestruction(domain, playerName, devastate);
            PlayDestructionSFX();

            using var effectScope = s_destroyEffectMarker.Auto();
            OnBlockImpactedEventChannel.RaiseEvent(new PrismEventData
            {
                ownDomain = Domain,
                SpawnPosition = _lastDestructionPosition,
                Rotation = _lastDestructionRotation,
                Scale = _lastDestructionScale,
                TargetTransform = targetTransform,
                Volume = prismProperties.volume,
                Kind = kind,
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
        /// <param name="debrisSpeedLimit">
        /// Optional per-impact ceiling on the resulting debris speed, overriding the explosion
        /// prefab's own. 0 keeps the prefab value. Pass one only alongside a TRUE-velocity
        /// impact vector (see <see cref="PrismEffectHelper.DamageProportional"/>) - the prefab
        /// ceiling is sized for the legacy inertia/volume gain, not for real speeds.
        /// </param>
        public void Damage(Vector3 impactVector, Domains domain, string playerName, bool devastate = false, bool byCreature = false,
                           float debrisSpeedLimit = 0f)
        {
            if (destroyed) return;
            // Super-shielded prisms are invulnerable to Damage itself. A source that may
            // break them (the Rhino energy sword, arena teardowns) must call
            // DeactivateShields() first, then Damage(devastate: true) — the sanctioned
            // animated sequence (see AstroLeagueArena.ClearEdgeLining / RHINO_ENERGY_SWORD.md).
            // That sequence clears the flag before it gets here, so a BREAKING hit never
            // reaches this gate; only an unbreaking one does, and it now deflects (below).
            if (AbsorbSuperShieldHit(impactVector.magnitude)) return;
            // The shield pops instead of the prism, AS the prism explosion: the shed shards
            // are ordinary explosion debris on the shield's own mesh, handed the same impact
            // vector and ceiling Explode would have received — so the armour being knocked
            // off looks exactly like mass coming apart, because it is the same effect
            // (Docs/PRISM_ANIMATION.md §4.8.1).
            if (prismProperties.IsShielded && !devastate)
                DeactivateShields(impactVector, debrisSpeedLimit);
            else
            {
                _destroyedByCreature = byCreature;
                Explode(impactVector, domain, playerName, devastate, debrisSpeedLimit);
            }
        }

        public void Consume(Transform target, Domains domain, string playerName, bool devastate = false, bool byCreature = false)
        {
            // A consume carries no impact vector — only the suction sink — so the deflection
            // is stamped at the floor magnitude rather than derived from a direction that
            // describes where the mass was being pulled, not how hard it was struck.
            if (destroyed) return;
            if (AbsorbSuperShieldHit(0f)) return;
            // Impact-less on purpose, for the same reason the deflection above is stamped
            // at the floor: a consume carries no impact vector, only a suction sink, and
            // grazing armour off is not a blow. The debris path degrades a zero vector to
            // the same quiet minimum-speed puff an impactless prism death gets.
            if (prismProperties.IsShielded && !devastate)
                DeactivateShields();
            else
            {
                _destroyedByCreature = byCreature;
                Implode(target, domain, playerName, devastate);
            }
        }

        // Per-prism rate-limit slot for the deflection jiggle, owned here because the state is
        // one float per prism and PrismSuperShieldJiggle would otherwise need a dictionary
        // keyed by instance id. Absolute clock time; a pooled reuse inherits a value far in
        // the past, which correctly reads as "no recent deflection".
        float _lastSuperShieldJiggleTime = float.NegativeInfinity;

        /// <summary>Clock time of this prism's most recent deflection stamp, or
        /// <see cref="float.NegativeInfinity"/> if it has none in this life. A scheduled
        /// settle carries the value it stamped and compares it here, so a re-stamp or a pool
        /// reuse invalidates the older callback without an O(n) scan of the timer list.</summary>
        internal float LastSuperShieldJiggleTime => _lastSuperShieldJiggleTime;

        /// <summary>
        /// THE super-shield invulnerability gate. Returns true when this prism absorbs the hit
        /// — the caller must then do nothing else to it.
        ///
        /// Super-shielded prisms are invulnerable to damage itself. A source that BREAKS one
        /// (the Rhino energy sword, arena teardowns) calls <see cref="DeactivateShields"/>
        /// first and only then <c>Damage(devastate: true)</c> — the sanctioned animated
        /// sequence — which clears the flag, so a breaking hit never arrives here. Everything
        /// that does arrive is by definition a hit the prism SURVIVED. The impactor's other
        /// effect SOs (sparks, sound) still fire, and since this method also stamps the
        /// deflection wobble, such a hit now reads as a DEFLECTION rather than as a miss —
        /// with no state change of any kind (Docs/PRISM_ANIMATION.md §5 C14).
        ///
        /// This exists as ONE method because the check previously existed as four independent
        /// copies — <see cref="Damage"/>, <see cref="Consume"/>,
        /// <c>PrismSpatialIndex.ResolveExplosionHit</c> and
        /// <c>ExplosionImpactor.ExecuteCommonPrismCommands</c> — and a per-call-site copy is a
        /// rule you can forget to apply at the next damage source. Route every new one here.
        /// </summary>
        /// <param name="impactSpeed">
        /// Magnitude of the impact in world units/second, used only to size the wobble. Pass 0
        /// where the hit site cannot describe its own magnitude; the deflection still reads, at
        /// the configured floor.
        /// </param>
        public bool AbsorbSuperShieldHit(float impactSpeed)
        {
            if (prismProperties is not { IsSuperShielded: true }) return false;
            PrismSuperShieldJiggle.TryStamp(this, impactSpeed, ref _lastSuperShieldJiggleTime);
            return true;
        }

        // State Management Methods
        public void MakeDangerous() => stateManager?.MakeDangerous();
        public void DeactivateShields() => stateManager?.DeactivateShields();

        /// <summary>
        /// Drops every shield tier and hands the disengage overlay the WORLD-space impact
        /// vector of the force that BROKE it. The overlay is ordinary prism-explosion
        /// debris (Docs/PRISM_ANIMATION.md §4.8.1), so the vector and the optional
        /// true-velocity ceiling carry EXACTLY the semantics of <see cref="Damage"/>'s own
        /// parameters — the shards fly, rotate away per face, erode and fade the way the
        /// prism's own pieces would have. Zero degrades to the impactless-death puff.
        /// </summary>
        public void DeactivateShields(Vector3 breakVelocity, float debrisSpeedLimit = 0f) =>
            stateManager?.DeactivateShields(null, breakVelocity, debrisSpeedLimit);
        public void ActivateShield() => stateManager?.ActivateShield();
        public void ActivateShield(float duration) => stateManager?.ActivateShield(duration);
        public void ActivateSuperShield() => stateManager?.ActivateSuperShield();
        public void SetTransparency(bool transparent) => materialAnimator?.SetTransparency(transparent);

        // Team Management Methods
        public void Steal(string playerName, Domains domain, bool superSteal = false) =>
            teamManager.Steal(playerName, domain, superSteal);
        public void ChangeTeam(Domains domain) => teamManager?.ChangeTeam(domain);

        /// <summary>
        /// Declare this prism a member of <paramref name="trail"/> - the ONE way to stamp
        /// trail membership, keeping the public field and the prismProperties mirror coherent.
        /// Call it AFTER <see cref="Initialize"/>: pool-reuse reset clears membership
        /// (a reused prism must never wear its previous life's container), so a stamp made
        /// before Initialize is silently wiped.
        /// </summary>
        public void AssignTrail(Trail trail)
        {
            Trail = trail;
            if (prismProperties != null) prismProperties.Trail = trail;
        }

        
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
                blockCollider.enabled = true;
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
            {
                var index = PrismSpatialIndex.Instance;
                if (index != null)
                {
                    index.UpdatePosition(SpatialIndexId, transform.position);
                    // Movers can rotate too (gyroid bonding, fauna bodies) - a
                    // shielded mover's shell pose must track the full transform.
                    index.UpdateShellTransform(SpatialIndexId);
                }
            }

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