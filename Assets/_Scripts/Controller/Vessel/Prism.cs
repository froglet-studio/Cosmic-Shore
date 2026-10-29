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
using Unity.Mathematics;
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
        [SerializeField] bool devastated;
        public bool Devastated => devastated;

        // Set transiently by Damage/Consume right before destruction so the destruction
        // SFX can tell a creature (fauna) kill from a generic block destroy. Reset on pool reuse.
        bool _destroyedByCreature;

        /// <summary>The force that destroyed this prism (the destroyed channel's attacker name), or
        /// null while it stands. An Ark's fall is credited to whoever took its LAST hull prism
        /// (Docs/Pilots/ARTIFACTS.md §5). Reset on pool reuse.</summary>
        public string DestroyerName { get; private set; }

        /// <summary>Game time this prism was destroyed (meaningless while it stands).</summary>
        public float DestroyedAt { get; private set; }

        /// <summary>True when fauna took this prism (a creature kill credits nobody).</summary>
        public bool DestroyedByCreature => _destroyedByCreature;

        // Set transiently by Damage the same way, so the destroyed-channel payload can say
        // whether DIRECT GUNFIRE killed this prism. It exists for the weapon that reloads by
        // destroying mass (VesselRearmOnPrismDestruction): a Sparrow's tank is filled by its
        // guns and by nothing else, so the listener has to be able to tell a bullet's kill from
        // a rocket's blast, and the channel is the only place every route is visible.
        // False for everything else by construction - a blast, a ram, a creature, a teardown.
        bool _destroyedByGunfire;
        // Set transiently by Slice around its Damage call, the same way: the plane a blade cut
        // this prism along. Explode forwards it on the death event so the factory draws a CUT
        // (Docs/PRISM_ANIMATION.md §4.10) instead of a burst. A zero normal is an ordinary death;
        // Slice clears it again whether or not the hit destroyed the prism.
        Vector3 _sliceCutPoint;
        Vector3 _sliceCutNormal;
        // Set transiently by DamageByCollision around its Damage call, the same way: this death is
        // prism movement's collision ABOVE level 1 of its ladder, so the factory draws the plain
        // explosion rather than a finer detail level (Docs/PRISM_ANIMATION.md §4.12).
        bool _plainDeath;
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


        /// <summary>
        /// Local-space geometry of the prism's OWN authored box — the surface an
        /// UNSHIELDED prism presents. Distinct from <see cref="TryGetShellGeometry"/>,
        /// which always returns the SHIELD (3x circumscribing) geometry even for a
        /// prism that is not currently shielded; reading that for a plain prism would
        /// hand the shell tier a box three times too big.
        ///
        /// Used only by the experimental extended-coverage shell tier
        /// (<see cref="PrismShellContactManager.ExtendToUnshieldedPrisms"/>).
        /// </summary>
        internal bool TryGetBoxGeometry(out Vector3 centerLocal, out Vector3 halfExtentsLocal)
        {
            if (_authoredColliderSizeCached)
            {
                centerLocal = blockCollider != null ? blockCollider.center : Vector3.zero;
                halfExtentsLocal = _authoredColliderSize * 0.5f;
                return true;
            }
            if (blockCollider != null)
            {
                centerLocal = blockCollider.center;
                halfExtentsLocal = blockCollider.size * 0.5f;
                return true;
            }
            centerLocal = default;
            halfExtentsLocal = default;
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
        /// True for environment/structure prisms (e.g. the SkimRace track, spawnable shapes)
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

        static readonly int SuctionStartTimeId = Shader.PropertyToID("_SuctionStartTime");
        const float ConvergenceBoundsPadding = 2f;

        // --- Instanced rendering (Entities Graphics companion entity) -----------
        // While the instanced path is active the MeshRenderer stays disabled and a
        // companion entity draws in its place (PrismRenderService — see
        // Docs/PRISM_ECS_MIGRATION.md). _renderVisible is the single visibility
        // truth for BOTH paths; _exoticVisualActive forces the GameObject renderer
        // for per-prism-unique geometry (octahedron shield morph/shatter).
        internal PrismRenderHandle RenderHandle;
        bool _renderVisible;
        bool _exoticVisualActive;
        bool _ownerHidden;

        /// <summary>
        /// An OWNER that draws this prism by other means hides it here (a swarm member's body: the swarm
        /// draws every living member from one GPU buffer, Docs/SWARM_FAUNA.md §14, and this prism is the
        /// member's collider-and-mass PROXY). Only photons change: the collider, the spatial index, the
        /// mass and every gameplay state stay exactly as they are, and the companion entity still EXISTS
        /// (clock stamps are one-shot - an entity created late loses them), it is just not drawn. The
        /// owner must un-hide before the prism stops being drawn elsewhere - a skeleton, a released body -
        /// or it becomes an invisible piece of mass. Cleared on pool reuse.
        /// </summary>
        public void SetOwnerHidden(bool hidden)
        {
            if (_ownerHidden == hidden) return;
            _ownerHidden = hidden;
            ApplyRenderPath();
        }

        public bool OwnerHidden => _ownerHidden;

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

        /// <summary>The renderer the §4.11 MeshRenderer stamp path writes to (Docs/PRISM_ANIMATION.md) on a
        /// device without Entities Graphics — the same renderer that draws this prism there.</summary>
        internal MeshRenderer StampRenderer => meshRenderer;

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

        /// <summary>Hook for a one-shot creation stamp that needs the FINAL transform and a
        /// live companion entity. Called once from the creation coroutine's completion, right
        /// after <see cref="IsCreationComplete"/> goes true. Base does nothing.</summary>
        protected virtual void OnCreationComplete() { }

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

        /// <summary>The size this prism's prefab AUTHORED (its local scale before Awake zeroed it), unclamped: the
        /// scale window trims <see cref="TargetScale"/>, never this. See PrismScaleAnimator.AuthoredTargetScale.</summary>
        public Vector3 AuthoredScale =>
            scaleAnimator && scaleAnimator.AuthoredTargetScale != Vector3.zero ? scaleAnimator.AuthoredTargetScale : transform.localScale;

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
            // existence follows `show`; DRAWING additionally follows the owner's hide (SetOwnerHidden)
            bool draw = show && !_ownerHidden;

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
                    // An entity minted after the super-shield engaged was born at 0.
                    if (_superShieldMark) SyncSuperShieldMark();
                }
                // Batched: applied in one structural change per direction at
                // LateUpdate (same frame, before rendering). Per-prism toggles were
                // the dominant creation-tick cost (Prism.Create.Visibility).
                PrismRenderService.QueueVisible(in RenderHandle, draw);
            }
            else
            {
                // Immediate: the GameObject renderer takes over THIS frame (exotic
                // shield morph / legacy fallback) — a queued hide would double-draw
                // entity + MeshRenderer for the rest of the frame.
                if (PrismRenderService.IsHandleUsable(in RenderHandle))
                    PrismRenderService.SetVisible(in RenderHandle, false);
                if (meshRenderer) meshRenderer.enabled = _renderVisible && !_ownerHidden;
            }
        }

        // While set, the companion entity renders this mesh (a cache-shared settled-shield
        // octahedron) instead of meshFilter.sharedMesh. This keeps SETTLED shielded prisms
        // on the instanced path — batched with every same-geometry shielded prism — while
        // the GameObject renderer handles only the brief per-prism morph/shatter animations.
        Mesh _renderMeshOverride;

        /// <summary>
        /// The shared mesh currently overriding the companion entity's geometry, or null.
        ///
        /// Exposed because an override is a SHARED resource with more than one legitimate
        /// claimant — the settled shield and the Urchin cradle's high-poly swap — and neither
        /// may stomp the other. A claimant checks this is null before taking the slot, and on
        /// release checks it is still holding ITS OWN mesh before calling
        /// <see cref="ClearRenderMeshOverride"/>: a prism that got shielded mid-cradle has
        /// legitimately been taken over, and clearing there would drop the shield's geometry
        /// on the floor. Never write through this — it is the question, not the setter.
        /// </summary>
        internal Mesh RenderMeshOverride => _renderMeshOverride;

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
                ApplyColorShade();
            }
            else
            {
                _renderEntityDecline = "PrismRenderService.Create declined (no ECS world / EntitiesGraphicsSystem, or the master toggle is off)";
                _renderEntityMesh = null;
            }
        }

        /// <summary>
        /// Re-evaluates which path draws this prism against the CURRENT state of
        /// <see cref="PrismRenderService.Enabled"/>, releasing the companion entity when
        /// the service has been switched off.
        ///
        /// WHY this exists: <c>SetRuntimeOverride</c> only gates entity CREATION, so
        /// flipping it mid-session leaves every prism that already owns an entity drawing
        /// through it with its MeshRenderer still disabled — i.e. the A/B measures the two
        /// paths on the prisms laid AFTER the flip and nothing else, which for a standing
        /// arena is nothing at all. This is the missing half: the diagnostic toggle calls
        /// it on every live prism so the whole population moves together.
        ///
        /// Diagnostics only — nothing in the game loop calls it, and the render path is not
        /// a thing gameplay may switch. Left outside a compilation guard deliberately: it is
        /// a handful of unreferenced lines in a release build, against the guard hazards
        /// Docs/CONDITIONAL_COMPILATION.md records.
        /// </summary>
        internal void ResyncRenderPathForDiagnostics()
        {
            if (!PrismRenderService.Enabled && PrismRenderService.IsHandleUsable(in RenderHandle))
            {
                PrismRenderService.Destroy(ref RenderHandle);
                // SyncRenderMesh dedupes against this; a stale value would suppress the
                // mesh push to a LATER entity for as long as the prism keeps that mesh.
                _renderEntityMesh = null;
                _renderEntityDecline = null;
            }

            ApplyRenderPath();
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

        // True while this prism wears the super-shield (the stellated octahedron). Rendered as
        // the per-instance _PrismSuperShielded bit, which the Dolphin's Echo Sight reads to
        // paint every super-shield inside the cone in the danger colour: a crystal blast that
        // reaches one ENDS there (PrismSpatialIndex.ResolveExplosionHit), so the aim has to say
        // so. Owned here rather than on the shield component because it has to survive the
        // companion entity being (re)created after the shield engaged.
        bool _superShieldMark;

        static readonly int SuperShieldedPropertyId = Shader.PropertyToID("_PrismSuperShielded");
        static MaterialPropertyBlock s_superShieldBlock;

        /// <summary>
        /// Sets the super-shield MARK. Called by <see cref="PrismStellatedOctahedronShield"/> as
        /// its pose goes final in both directions, so the bit follows the shield the player can
        /// SEE — every path that super-shields a prism engages that shield. A STATE write, once
        /// per transition, never per frame (the clock-material law has no quarrel with a state
        /// change that is final the instant it is applied).
        /// </summary>
        internal void SetSuperShieldMark(bool superShielded)
        {
            if (_superShieldMark == superShielded) return;
            _superShieldMark = superShielded;
            SyncSuperShieldMark();
        }

        void SyncSuperShieldMark()
        {
            if (PrismRenderService.SetSuperShieldMark(in RenderHandle, _superShieldMark)) return;

            // No Entities Graphics on this device: the mark is a family of the §4.11 stamp block, so a
            // stamp rebuild keeps it and an unmarked prism carries no block at all.
            if (PrismMeshRendererStamps.SetSuperShieldMark(meshRenderer, _superShieldMark)) return;

            // Legacy MeshRenderer path (no Entities Graphics on this device): the same bit rides
            // a property block. Get-modify-set, so any other writer's properties survive.
            if (meshRenderer == null) return;
            s_superShieldBlock ??= new MaterialPropertyBlock();
            meshRenderer.GetPropertyBlock(s_superShieldBlock);
            s_superShieldBlock.SetFloat(SuperShieldedPropertyId, _superShieldMark ? 1f : 0f);
            meshRenderer.SetPropertyBlock(s_superShieldBlock);
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
            ApplyColorShade();
        }

        // Per-prism SHADE of the domain colour (SetColorShade, SetColorLift). Identity = gain 1, lift 0, which every
        // prism has unless an owner asks otherwise; reset on every pooled Initialize.
        float _colorShadeGain = 1f;
        float _colorLift;

        bool HasColorShade => _colorShadeGain != 1f || _colorLift != 0f;

        /// <summary>The lit mark this machine draws on the prism (<see cref="SetColorLift"/>), 0 when unlit.</summary>
        public float ColorLift => _colorLift;

        /// <summary>
        /// Draw this prism DARKER than its domain colour: brightness scaled by <paramref name="gain"/>, clamped to
        /// [0, 1]. The hue stays the domain's, because a prism's colour is its team; the one LIGHTER state is
        /// <see cref="SetColorLift"/>. It is re-applied after every material sync, so it survives a team change,
        /// a danger or shield state and a transparency swap, and is cleared by the next pooled
        /// <see cref="Initialize"/>. The nested gyroid grades its sheets with it (Docs/ECOSYSTEM.md §58.5). Drawn on
        /// the instanced (entity) path only; while a per-prism exotic visual holds the GameObject renderer the prism
        /// shows its material unshaded.
        /// </summary>
        public void SetColorShade(float gain)
        {
            _colorShadeGain = Mathf.Clamp01(gain);
            RefreshColorShade();
        }

        /// <summary>
        /// Draw this prism LIT: its domain colour lifted toward white by <paramref name="lift"/> (0 = its own colour,
        /// 1 = white). This is the per-prism SELECTED state: a pilot marking particular prisms (the Angler's paint,
        /// <c>R_VesselActions/ANGLER.md</c> §1a). It is a look, never a gameplay state: nothing reads it, so it is set
        /// on the machine that shows it and is not replicated. <paramref name="fadeSeconds"/> &gt; 0 fades from the
        /// lift it showed (or from <paramref name="fromLift"/> when that is 0 or more) with one clock stamp, so a mark
        /// never pops. Like the shade it survives a material sync and is cleared by the next pooled
        /// <see cref="Initialize"/>; on the instanced (entity) path only.
        /// </summary>
        public void SetColorLift(float lift, float fadeSeconds = 0f, float fromLift = -1f)
        {
            float from = fromLift < 0f ? _colorLift : Mathf.Clamp01(fromLift);
            _colorLift = Mathf.Clamp01(lift);
            RefreshColorShade();
            if (fadeSeconds > 0f && PrismRenderService.IsHandleUsable(in RenderHandle) && meshRenderer != null)
                PrismRenderService.StampShadeTransition(in RenderHandle, meshRenderer.sharedMaterial, PrismClock.Now,
                    fadeSeconds, _colorShadeGain, from);
        }

        void RefreshColorShade()
        {
            if (!PrismRenderService.IsHandleUsable(in RenderHandle) || meshRenderer == null) return;
            if (HasColorShade) ApplyColorShade();
            else PrismRenderService.SetMaterial(in RenderHandle, meshRenderer.sharedMaterial, refreshColors: true);
        }

        void ApplyColorShade()
        {
            if (!HasColorShade || meshRenderer == null) return;
            PrismRenderService.ApplyColorShade(in RenderHandle, meshRenderer.sharedMaterial, _colorShadeGain, _colorLift);
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
        /// Whole-prism GPU suction toward a world-space point (Docs/PRISM_ANIMATION.md §5 C9).
        /// Collider off at stamp — gameplay state is FINAL; only photons animate. Pair with
        /// <see cref="ClearSuctionClockStamp"/> on pool return.
        /// </summary>
        public void StampSuctionToward(Vector3 worldLocation, float duration)
        {
            if (duration <= 0.01f) return;
            if (blockCollider) blockCollider.enabled = false;

            // A prism whose creation has not completed is invisible and has no render entity yet (one is made
            // when it first shows), so there is nothing to suction. Its pending creation is cancelled: completing
            // during the suction would pop it in at full size inside a world shrinking to a point. These were the
            // Orrery swaps' "no companion render entity" prisms (T-20261021-e916).
            if (!IsCreationComplete)
            {
                CancelPendingCreation();
                CosmicShore.Utility.PerformanceBenchmark.LoadInsights.Count(
                    "Suction: prisms not created yet (invisible; creation cancelled)");
                return;
            }

            var handle = RenderHandle;
            if (!PrismRenderService.IsHandleUsable(in handle))
            {
                // No Entities Graphics on this device (a WebGL2 browser): the same stamp and the same
                // culling envelope ride the MeshRenderer (Docs/PRISM_ANIMATION.md §4.11).
                var suctionPoint = new float3(worldLocation.x, worldLocation.y, worldLocation.z);
                if (PrismMeshRendererStamps.StampSuctionClock(meshRenderer, PrismClock.Now, duration, 1f, 0f,
                        in suctionPoint))
                {
                    var local = transform.InverseTransformPoint(worldLocation);
                    PrismMeshRendererStamps.ResetBounds(meshRenderer);
                    PrismMeshRendererStamps.EncapsulateBoundsPoint(meshRenderer,
                        new float3(local.x, local.y, local.z), ConvergenceBoundsPadding);
                    return;
                }

                CosmicShore.Utility.PerformanceBenchmark.LoadInsights.Count(
                    "Suction: prisms with no render entity (they snap instead of suctioning)");
                // The detail line is composed only for the one warning that is actually logged.
                if (PrismClockDiagnostics.WillWarnNoRenderEntity("cellSwapSuction"))
                    PrismClockDiagnostics.WarnNoRenderEntity("cellSwapSuction", this,
                        _renderEntityDecline ?? PrismRenderService.StatusLine());
                return;
            }

            var mat = meshRenderer != null ? meshRenderer.sharedMaterial : null;
            if (mat == null || !mat.HasProperty(SuctionStartTimeId))
                PrismClockDiagnostics.WarnUnwiredMaterial(mat, "_SuctionStartTime", this);

            var loc = new float3(worldLocation.x, worldLocation.y, worldLocation.z);
            if (!PrismRenderService.StampSuctionClock(in handle, PrismClock.Now, duration, 1f, 0f, loc))
                return;

            if (meshFilter != null)
                PrismRenderService.ResetBoundsToMesh(in handle, meshFilter.sharedMesh);
            var objectPoint = transform.InverseTransformPoint(worldLocation);
            PrismRenderService.EncapsulateBoundsPoint(in handle,
                new float3(objectPoint.x, objectPoint.y, objectPoint.z), ConvergenceBoundsPadding);
        }

        /// <summary>Pool hygiene: Duration 0 is identity on live graphs. Initialize also
        /// routes through <see cref="PrismRenderService.ClearPrismStamps"/>.</summary>
        public void ClearSuctionClockStamp()
        {
            PrismRenderService.ClearSuctionClockStamp(in RenderHandle);
            PrismMeshRendererStamps.ClearSuctionClockStamp(meshRenderer);
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
            PrismMeshRendererStamps.ClearPrismStamps(meshRenderer); // §4.11 twin (no-op where entities run)
            SetSuperShieldMark(false); // nor a prior life's super-shield
            _colorShadeGain = 1f;      // nor a prior owner's colour shade (the next material sync drops it)
            _colorLift = 0f;           // nor a prior pilot's lit mark
            PaintedPrey.Forget(this);  // nor a prior life's paint (every creature preferred it)

            PlayerName = playerName;
            blockCollider.enabled = false;
            SetRenderVisible(false);
            IsCreationComplete = false; // this life is invisible until CreateBlockCoroutine finishes

            var authoredTargetScale = scaleAnimator ? scaleAnimator.TargetScale : transform.localScale;
            if (authoredTargetScale == Vector3.zero)
                authoredTargetScale = transform.localScale;

            scaleAnimator.SetTargetScale(authoredTargetScale);
            _creationScale = authoredTargetScale;
            _creationLife++;
            _creationQueued = false;
            _creationRoutine = StartCoroutine(CreateBlockCoroutine(authoredTargetScale));

            if (prismProperties.IsShielded) ActivateShield();
            if (prismProperties.IsDangerous)
            {
                // A silenced pilot lays no new danger (VesselSilence): the request is dropped here,
                // at birth, so the prism is plain in every respect - the flag is what danger effects
                // read, not only the look.
                if (VesselSilence.SuppressesDanger(playerName)) prismProperties.IsDangerous = false;
                else MakeDangerous();
            }
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
            _ownerHidden = false;
            CompletesAsLiveRibbon = false; // pool reuse: the next life states its own class
            _destroyedByCreature = false; // pool reuse: clear stale creature-kill flag
            DestroyerName = null;         // pool reuse: the next life has no destroyer yet
            DestroyedAt = 0f;             // pool reuse: recycled corpses come back (PrismRecycler); no stale death time
            RecyclesWhenDestroyed = false; // pool reuse: each life opts in to recycling itself
            _destroyedByGunfire = false;  // pool reuse: clear stale gunfire-kill flag
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
            _carried = false;   // (and no parcel holds a pooled prism)
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
            prismProperties.DangerWeight = 1f;
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

        // Completions spent this frame by mass the ENVIRONMENT pool issued (flora, laid
        // worlds). Counted separately from everything else - a pilot's trail, an Ark's wake,
        // a projectile's cairn - because the two share one budget otherwise, and a growing
        // world easily queues thousands of completions: at 6/frame an environment backlog put
        // every newly laid trail prism behind it, so a pilot flying past a growing forest laid
        // a trail that never appeared (the Arkway report: "neither the Ark nor I left a trail").
        // Each class gets the tier's full count, so the worst frame is 2x the authored cap -
        // still a slice, and the order within each class is unchanged.
        static int s_environmentCompletionsThisFrame;

        /// <summary>
        /// This life is a LIVE RIBBON laid behind a single mover (the Ark's wake), not bulk
        /// world growth, even though the environment pool issued it: it completes on the
        /// pilot-mass budget so a growing world cannot hold it back. Set by the laying site
        /// after Initialize; cleared on every reuse.
        /// </summary>
        public bool CompletesAsLiveRibbon { get; set; }

        // Milliseconds of creation-completion work spent this frame. Only read while a WATCHED
        // load gate states a time slice (PrismTrailBuilder.LoadGateCreationBudgetMsOverride):
        // the connecting screen now shows the arena being built, so the covered-screen premise
        // behind the 512/frame count - that no frame is worth protecting - no longer holds.
        static double s_creationSpentMs;
        static readonly double s_creationMsPerTick = 1000.0 / System.Diagnostics.Stopwatch.Frequency;
        static int s_creationBudgetFrame = -1;

        // Split attribution for the ~0.5ms creation-completion tick: which of the
        // three suspects dominates decides the fix (enableable-component render flag
        // vs SOAP listener work vs spatial bind). See
        // Docs/archive/PERFORMANCE_LOG_2026.md Task 4.
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

            // Read once, AFTER the first yield so a laying site can still mark this life as a
            // live ribbon (CompletesAsLiveRibbon) after Initialize. Membership cannot change
            // while the prism waits (a release parks it inactive, which stops this coroutine).
            bool isEnvironmentMass = !CompletesAsLiveRibbon && EnvironmentPrismPool.IsIssued(this);
            _creationRoutine = null;

            // Complete now only if nobody of this class is already waiting (first come, first served).
            if (CreationQueue.IsEmpty(isEnvironmentMass) && HasCreationBudget(isEnvironmentMass))
            {
                SpendCreationBudget(isEnvironmentMass);
                FinishCreation(authoredTargetScale);
                yield break;
            }

            // Otherwise wait in ONE queue, drained by one driver, instead of as a coroutine woken every
            // frame only to yield again: at 6 completions a frame, 57,638 waiting prisms cost 139 ms a
            // frame of coroutine scheduling alone in a Bloomrush capture (3.5 fps, 2026-10-20).
            _creationQueued = true;
            CreationQueue.Enqueue(this, isEnvironmentMass);
        }

        static void BeginCreationBudgetFrame()
        {
            if (s_creationBudgetFrame == Time.frameCount) return;
            s_creationBudgetFrame = Time.frameCount;
            s_creationCompletionsThisFrame = 0;
            s_environmentCompletionsThisFrame = 0;
            s_creationSpentMs = 0.0;
        }

        static bool HasCreationBudget(bool isEnvironmentMass)
        {
            BeginCreationBudgetFrame();

            // A WATCHED load gate (the connecting screen showing the build live) states a
            // per-frame TIME slice instead of a completion count - per-prism completion
            // cost varies with scene size and collider density, so a count cannot hold a
            // frame budget across machines. Same argument, same shape, as the lay budget.
            float watchedCreationMs = PrismTrailBuilder.IsLoadGateHolding
                ? PrismTrailBuilder.LoadGateCreationBudgetMsOverride
                : 0f;
            if (watchedCreationMs > 0f)
                return s_creationSpentMs < watchedCreationMs;

            int creationBudget = PrismTrailBuilder.IsLoadGateHolding
                ? LoadGateCreationCompletionsPerFrame
                : s_bulkTransportsInFlight > 0
                    ? BulkTransportCreationCompletionsPerFrame
                    : MaxCreationCompletionsPerFrame;
            int spent = isEnvironmentMass
                ? s_environmentCompletionsThisFrame
                : s_creationCompletionsThisFrame;
            return spent < creationBudget;
        }

        static void SpendCreationBudget(bool isEnvironmentMass)
        {
            if (isEnvironmentMass) s_environmentCompletionsThisFrame++;
            else s_creationCompletionsThisFrame++;
        }

        /// <summary>Prisms waiting for a creation completion (diagnostics, tests).</summary>
        public static int CreationQueueLength => CreationQueue.Count;

        /// <summary>
        /// The prisms whose spawn wait has ended but whose completion the per-frame budget has not
        /// reached yet, in arrival order, one queue per budget class. A waiting prism costs nothing
        /// per frame. An entry whose prism has since died, completed, gone inactive (a coroutine would
        /// have been stopped there) or started a new life is dropped without spending budget.
        /// </summary>
        static class CreationQueue
        {
            static readonly System.Collections.Generic.Queue<(Prism prism, int life)> s_environment = new();
            static readonly System.Collections.Generic.Queue<(Prism prism, int life)> s_other = new();
            static readonly ProfilerMarker s_drainMarker = new("Prism.CreationQueue.Drain");
            static Driver s_driver;

            public static int Count => s_environment.Count + s_other.Count;

            public static bool IsEmpty(bool isEnvironmentMass) =>
                (isEnvironmentMass ? s_environment : s_other).Count == 0;

            public static void Enqueue(Prism prism, bool isEnvironmentMass)
            {
                (isEnvironmentMass ? s_environment : s_other).Enqueue((prism, prism._creationLife));
                if (s_driver) return;
                var go = new GameObject("[PrismCreationQueue]") { hideFlags = HideFlags.HideInHierarchy };
                UnityEngine.Object.DontDestroyOnLoad(go);
                s_driver = go.AddComponent<Driver>();
            }

            [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
            static void ResetOnLoad()
            {
                s_environment.Clear();
                s_other.Clear();
                s_driver = null;
            }

            static void Drain(System.Collections.Generic.Queue<(Prism prism, int life)> queue, bool isEnvironmentMass)
            {
                while (queue.Count > 0 && HasCreationBudget(isEnvironmentMass))
                {
                    var (prism, life) = queue.Dequeue();
                    if (!prism || prism.destroyed || !prism._creationQueued || prism._creationLife != life
                        || prism.IsCreationComplete || !prism.gameObject.activeInHierarchy)
                        continue;

                    SpendCreationBudget(isEnvironmentMass);
                    prism._creationQueued = false;
                    try { prism.FinishCreation(prism._creationScale); }
                    catch (Exception e) { Debug.LogException(e, prism); }   // one bad prism must not stall the queue
                }
            }

            // Runs in Update, before this frame's coroutines resume, so prisms already waiting are served
            // before a prism whose spawn wait ends this frame.
            sealed class Driver : MonoBehaviour
            {
                void Update()
                {
                    if (s_environment.Count == 0 && s_other.Count == 0) return;
                    using (s_drainMarker.Auto())
                    {
                        Drain(s_environment, true);
                        Drain(s_other, false);
                    }
                }

                void OnDestroy()
                {
                    if (s_driver == this) s_driver = null;
                }
            }
        }

        Coroutine _creationRoutine;
        Vector3 _creationScale;
        // Bumped by every Initialize, so a queue entry left by an earlier life of a pooled prism is dropped.
        int _creationLife;
        bool _creationQueued;

        /// <summary>
        /// Bumped by every <see cref="SetupDestruction"/>. <see cref="PrismRecycler"/> stamps it on its queue entry
        /// and acts only while it still matches, so a Restore followed by a second death never lets the FIRST
        /// death's entry recycle the prism early (and a pool reuse is caught by <c>destroyed</c> going false).
        /// </summary>
        internal int DestroyEpoch { get; private set; }

        /// <summary>
        /// Opt-in for <see cref="PrismRecycler"/>'s environment route: this prism is free mass whose death nobody
        /// else owns (a natural-death skeleton, <see cref="HealthPrism.LeaveAsSkeleton"/>), so once destroyed and
        /// its death effect has played it goes back to <see cref="EnvironmentPrismPool"/>. Vessel-trail prisms do
        /// not need it (their trail opts in, <see cref="Trail.RecycleDestroyedMembers"/>). Cleared on every
        /// Initialize: each life states it again.
        /// </summary>
        public bool RecyclesWhenDestroyed { get; set; }

        /// <summary>
        /// Finish THIS life's creation now: skip the spawn wait and the per-frame completion budget, and run
        /// exactly the completion the coroutine would have run (visibility, collider, growth stamp, created
        /// event, spatial registration). For an OWNER that needs the prism real in the frame it asked - a swarm
        /// member materialised to take a hit a weapon has already landed (Docs/SWARM_FAUNA.md §16.2). The caller
        /// owns the budget: every call is one full completion. No-op once complete, destroyed, or never started.
        /// </summary>
        /// <summary>
        /// Abandon this life's pending creation (spawn wait or the creation queue): the prism stays invisible,
        /// collider-off and unregistered until it is released or re-initialised. For mass leaving the world
        /// before it ever showed (a retiring world's suction). No-op once complete.
        /// </summary>
        public void CancelPendingCreation()
        {
            if (IsCreationComplete) return;
            if (_creationRoutine != null) StopCoroutine(_creationRoutine);
            _creationRoutine = null;
            _creationQueued = false;   // its queue entry is dropped when the drain reaches it
        }

        public void CompleteCreationImmediately()
        {
            if (destroyed || IsCreationComplete || (_creationRoutine == null && !_creationQueued)) return;
            if (_creationRoutine != null) StopCoroutine(_creationRoutine);
            _creationRoutine = null;
            _creationQueued = false;   // its queue entry is dropped when the drain reaches it
            FinishCreation(_creationScale);
        }

        void FinishCreation(Vector3 authoredTargetScale)
        {
            // Measured across the WHOLE completion (visibility, growth stamp, SOAP raise,
            // spatial registration) rather than one block of it - a slice that only counts
            // part of the work it is meant to bound is not a bound.
            long creationT0 = System.Diagnostics.Stopwatch.GetTimestamp();
            long insightT0 = CosmicShore.Utility.PerformanceBenchmark.LoadInsights.AccumulateStart();

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

            // The one point in a prism's life that runs exactly once, AFTER the companion
            // entity exists and with the transform and parent final. A subclass whose
            // creation carries its own one-shot stamp hangs it here rather than racing
            // Initialize, which runs before any of that is true.
            OnCreationComplete();

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

            s_creationSpentMs += (System.Diagnostics.Stopwatch.GetTimestamp() - creationT0) * s_creationMsPerTick;
            CosmicShore.Utility.PerformanceBenchmark.LoadInsights.AccumulateSample(
                "Prism creation completion (visibility, collider, grow stamp, SOAP, spatial bind)", insightT0);
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
            else if (!destroyed && !_carried && _colliderBeforeLodCull)
            {
                blockCollider.enabled = true;
            }
            _lodCulled = culled;
        }

        bool _carried;
        bool _colliderBeforeCarry;

        /// <summary>
        /// A payload parcel is riding a vessel (<see cref="PayloadCarrier"/>,
        /// Docs/Pilots/ARTIFACTS.md §2): the prism keeps its mass, its domain and its place in the
        /// spatial index, but its collider is held off so the hull carrying it never strikes its own
        /// cargo. The LOD manager's restore respects the hold. Clear it before the prism goes back to
        /// a pool.
        /// </summary>
        public void SetCarried(bool carried)
        {
            if (_carried == carried || !blockCollider) { _carried = carried; return; }
            if (carried)
            {
                _colliderBeforeCarry = blockCollider.enabled;
                blockCollider.enabled = false;
            }
            else if (!destroyed && !_lodCulled && _colliderBeforeCarry)
            {
                blockCollider.enabled = true;
            }
            _carried = carried;
        }

        /// <summary>True while a payload parcel holds this prism (<see cref="SetCarried"/>).</summary>
        public bool IsCarried => _carried;

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

        /// <summary>
        /// Grow (or, with a negative component, shrink) this prism by an arbitrary per-axis
        /// <paramref name="delta"/> on the one growth engine — the target moves and the clock
        /// carries the visual there (Docs/PRISM_ANIMATION.md), exactly as <see cref="Grow"/>
        /// does along <see cref="GrowthVector"/>. The result is clamped per axis by the prism's
        /// own scale window, so nothing can be driven to zero or past its ceiling. Used by the
        /// Butterfly's dust, which grows a prism along a direction it rolls rather than the
        /// authored growth axis.
        /// </summary>
        public void GrowAlong(Vector3 delta)
        {
            if (scaleAnimator is null || !scaleAnimator.enabled) return;
            scaleAnimator.SetTargetScale(scaleAnimator.TargetScale + delta);
            scaleAnimator.BeginGrowthAnimation();
        }

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

            // The volume this prism dies WITH, read before anything below can erase it. It used
            // to be read AFTER the scale animator was stood down, and GetCurrentVolume() reports 0
            // on a disabled animator - so every destroyed prism reported exactly 1 (the Max floor)
            // whatever its size: VolumeDestroyed was secretly a prism COUNT (Bloomrush scored on
            // it), VolumeRemaining drifted upward (credited true volume, debited 1), and a
            // restored prism re-entered the cell's volume spine weighing 1.
            //
            // CachedVolume is the SAME number the cell's volume ladder sums (volume is the spine),
            // so the stats and the phase ladder now agree on what a prism weighed. It is a field
            // read; the fallback reuses the lossyScale already read above for the debris pose, so
            // this adds no transform walk to the destruction path.
            float volumeAtDeath = CachedVolume > 0f
                ? CachedVolume
                : Mathf.Abs(destructionScale.x * destructionScale.y * destructionScale.z);

            if (scaleAnimator)
            {
                scaleAnimator.enabled = false;
            }

            blockCollider.enabled = false;
            SetRenderVisible(false);

            prismProperties.volume = volumeAtDeath;

            destroyed = true;
            DestroyerName = attackerPlayerName;
            DestroyedAt = Time.time;
            _creationQueued = false;   // as the waiting coroutine did: killed while waiting never completes (a Restore registers itself)
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
                    DestroyedByGunfire = _destroyedByGunfire,
                });
            }

            _lastDestructionScale = destructionScale;

            // Dead mass goes back to its pool once the death effect has played out, instead of living on
            // forever as a hidden, active GameObject in its trail (the idle-Lavalamp leak). The recycler decides
            // whether an owner keeps this corpse; see PrismRecycler for who is and is not recycled.
            DestroyEpoch++;
            PrismRecycler.Enqueue(this);
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
            // The legacy branch used to divide by prismProperties.volume. That divide was a
            // no-op for the whole life of the code - SetupDestruction read the volume AFTER
            // disabling the scale animator, so it was pinned to exactly 1 - and every debris
            // path was tuned against that. The read now happens first (the stats need the real
            // number), so the divide is DELETED rather than left to start damping large prisms:
            // the legacy gain stays exactly `inertia`, byte-for-byte what shipped. Do not
            // re-introduce a volume term here without retuning every debris consumer.
            using var effectScope = s_destroyEffectMarker.Auto();
            OnBlockImpactedEventChannel.RaiseEvent(new PrismEventData
            {
                ownDomain = Domain,
                SpawnPosition = _lastDestructionPosition,
                Rotation = _lastDestructionRotation,
                Scale = _lastDestructionScale,
                Velocity = impactVector,
                DebrisSpeedLimit = debrisSpeedLimit,
                Kind = kind,
                SlicePoint = _sliceCutPoint,
                SliceNormal = _sliceCutNormal,
                PlainDeath = _plainDeath,
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
        /// <param name="byGunfire">
        /// True when DIRECT gunfire caused this - a round that struck the prism itself, not a
        /// blast, a ram or a creature. Stamped onto the destroyed-channel payload
        /// (<see cref="PrismStats.DestroyedByGunfire"/>) for the reload-by-destroying-mass
        /// weapon; it changes nothing about the damage. Defaults false, so a new damage source
        /// is not gunfire until it says it is.
        /// </param>
        public void Damage(Vector3 impactVector, Domains domain, string playerName, bool devastate = false, bool byCreature = false,
                           float debrisSpeedLimit = 0f, bool byGunfire = false)
        {
            if (destroyed) return;
            // A SILENCED attacker's hit on anything but their own domain's mass deflects
            // (VesselSilence, Docs/ElementalAbilitySystem/SILENCE.md): the weapon still connects and
            // the prism still answers with the deflection wobble, so the shot reads as blocked rather
            // than as a miss - and nothing about the prism changes. Every damage source funnels here
            // (rounds, blasts, blades, flails, rams, sweeps), which is why the gate is here.
            if (DeflectsSilenced(playerName, impactVector.magnitude)) return;
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
                _destroyedByGunfire = byGunfire;
                Explode(impactVector, domain, playerName, devastate, debrisSpeedLimit);
            }
        }

        /// <summary>
        /// <see cref="Damage"/>, delivered by a BLADE: if this hit destroys the prism, its death
        /// is drawn as a cut along the world plane through <paramref name="cutPoint"/> with normal
        /// <paramref name="cutNormal"/> — two halves that part, open and dissolve from the cut
        /// face (Docs/PRISM_ANIMATION.md §4.10) — instead of a burst of debris. Everything the
        /// death MEANS is Damage's, unchanged: the same shield/super-shield gates, the same
        /// destruction bookkeeping, the same stats and SFX. Only the photons differ, and if the
        /// slice cannot be drawn (budget, config, a degenerate cut) the ordinary explosion is.
        /// </summary>
        public void Slice(Vector3 impactVector, Domains domain, string playerName,
                          Vector3 cutPoint, Vector3 cutNormal, bool devastate = false,
                          float debrisSpeedLimit = 0f)
        {
            _sliceCutPoint = cutPoint;
            _sliceCutNormal = cutNormal;
            try
            {
                Damage(impactVector, domain, playerName, devastate, debrisSpeedLimit: debrisSpeedLimit);
            }
            finally
            {
                _sliceCutPoint = Vector3.zero;
                _sliceCutNormal = Vector3.zero;
            }
        }

        /// <summary>
        /// <see cref="Damage"/>, delivered by prism movement's COLLISION rule (<see cref="PrismMotion"/>,
        /// Docs/PRISM_MOTION.md): devastating, because an ordinary shield is no answer to a collision.
        /// With <paramref name="fineDetail"/> (LEVEL 1 of the ladder) the death may be drawn at a finer
        /// destruction detail level — shatter or glitter, by the viewer's distance and the budget
        /// (Docs/PRISM_ANIMATION.md §4.12) — like any other death; without it (levels 2 and 3, many movers)
        /// it is always the plain explosion. Everything the death MEANS is Damage's, unchanged — the
        /// super-shield gate included, so super-shielded mass still deflects and survives.
        /// </summary>
        public void DamageByCollision(Vector3 impactVector, Domains domain, string playerName, bool fineDetail,
                                      float debrisSpeedLimit)
        {
            _plainDeath = !fineDetail;
            try
            {
                Damage(impactVector, domain, playerName, devastate: true, debrisSpeedLimit: debrisSpeedLimit);
            }
            finally
            {
                _plainDeath = false;
            }
        }

        /// <summary>
        /// Destroyed where it stands with NO debris and NO sound: the prism crossed a black hole's horizon
        /// and fell behind its shadow, so there is nothing left to see (the Stoat's dipole below its Space
        /// upgrade, <c>R_VesselActions/STOAT_DIPOLE.md</c>). Everything the death MEANS is the ordinary
        /// destruction's — the destroyed event scores it to <paramref name="playerName"/> and debits its
        /// owner, the index frees its site, it stays restorable — only the effect is withheld. Shields are
        /// no answer to a horizon (devastating).
        /// </summary>
        public void Vanish(string playerName)
        {
            if (destroyed) return;
            if (DeflectsSilenced(playerName, 0f)) return;
            SetupDestruction(Domain, playerName, devastate: true);
        }

        public void Consume(Transform target, Domains domain, string playerName, bool devastate = false, bool byCreature = false)
        {
            // A consume carries no impact vector — only the suction sink — so the deflection
            // is stamped at the floor magnitude rather than derived from a direction that
            // describes where the mass was being pulled, not how hard it was struck.
            if (destroyed) return;
            if (DeflectsSilenced(playerName, 0f)) return;
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
            StampDeflection(impactSpeed);
            return true;
        }

        /// <summary>
        /// The deflection wobble on its own - photons only, no state change of any kind. The read a
        /// super-shield gives a hit it survives, and the read a SILENCED pilot's hit gets on any
        /// prism (<see cref="DeflectsSilenced"/>). Rate-limited per prism by the same slot.
        /// </summary>
        public void StampDeflection(float impactSpeed) =>
            PrismSuperShieldJiggle.TryStamp(this, impactSpeed, ref _lastSuperShieldJiggleTime);

        /// <summary>
        /// THE silence gate on prism harm. True (and the hit is deflected) when
        /// <paramref name="attackerName"/> is silenced and this prism is not their own domain's
        /// (<see cref="VesselSilence.Protects"/>). Every call that destroys, consumes, steals or strips
        /// the shield off a prism on a pilot's behalf asks it first.
        /// </summary>
        public bool DeflectsSilenced(string attackerName, float impactSpeed)
        {
            if (!VesselSilence.Protects(attackerName, Domain)) return false;
            StampDeflection(impactSpeed);
            return true;
        }

        // State Management Methods
        public void MakeDangerous()
        {
            // A silenced pilot's abilities turn nothing dangerous (VesselSilence). Danger that is
            // already standing keeps its danger, so a re-apply on a dangerous prism goes through.
            if (!prismProperties.IsDangerous && VesselSilence.SuppressesDanger(PlayerName)) return;
            stateManager?.MakeDangerous();
        }
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

        /// <summary>
        /// Shield this prism because something HIT it — an explosion accepting its own
        /// domain's mass rather than clipping through it. <paramref name="impactSpeed"/>
        /// is the magnitude of that impact vector (a blast's <c>Speed x Inertia</c>) and
        /// <paramref name="debrisSpeedLimit"/> its true-velocity ceiling, as on
        /// <see cref="Damage"/>. The timed pop that ends the shield sheds along HALF that
        /// magnitude in a random direction, so an absorbed blow still reads as one when
        /// the armour comes off (PrismStateManager.ExecuteTimerDeactivation).
        /// </summary>
        public void ActivateShield(float duration, float impactSpeed, float debrisSpeedLimit = 0f) =>
            stateManager?.ActivateShield(duration, impactSpeed, debrisSpeedLimit);

        /// <summary>Untimed <see cref="ActivateShield(float,float,float)"/> — records the
        /// blow for a later <c>DeactivateShields(delay)</c> without scheduling one.</summary>
        public void ActivateShieldFromImpact(float impactSpeed, float debrisSpeedLimit = 0f) =>
            stateManager?.ActivateShield(null, impactSpeed, debrisSpeedLimit);
        public void ActivateSuperShield() => stateManager?.ActivateSuperShield();
        public void SetTransparency(bool transparent) => materialAnimator?.SetTransparency(transparent);

        // Team Management Methods
        public void Steal(string playerName, Domains domain, bool superSteal = false)
        {
            // A silenced pilot steals no prism (VesselSilence) - the steal deflects like a hit.
            if (DeflectsSilenced(playerName, 0f)) return;
            teamManager.Steal(playerName, domain, superSteal);
        }

        /// <summary>
        /// Hands this prism BACK to the pilot it was taken from - a restitution, not a theft, so it
        /// is the one ownership change silence does not refuse (a builder colony giving back what it
        /// took, <c>BuilderPrismWorld.GiveBack</c>). Everything else that changes a prism's owner on
        /// a pilot's behalf goes through <see cref="Steal"/>.
        /// </summary>
        public void ReturnTo(string playerName, Domains domain) =>
            teamManager.Steal(playerName, domain, false);
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
            // spatial index position. Queued, not written: every mover's pose this
            // frame lands in ONE Burst pass at PrismRenderService's LateUpdate flush
            // (still before rendering, so no visible lag) instead of one managed
            // EntityManager.SetComponentData per prism per frame.
            QueueRenderTransform();
        }

        /// <summary>Deferred <see cref="SyncRenderTransform"/> for per-frame movers —
        /// applied in this frame's batched transform flush.</summary>
        internal void QueueRenderTransform()
        {
            if (_exoticVisualActive) return;
            if (!PrismRenderService.IsHandleUsable(in RenderHandle)) return;
            PrismRenderService.QueueTransform(in RenderHandle, transform.localToWorldMatrix);
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
            EnvironmentPrismPool.ForgetDestroyed(this);

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