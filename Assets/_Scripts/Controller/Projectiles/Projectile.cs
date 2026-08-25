using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using CosmicShore.Core;
using Cysharp.Threading.Tasks;
using Reflex.Attributes;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using CosmicShore.Data;
namespace CosmicShore.Gameplay
{
    public class Projectile : MonoBehaviour
    {
        [Inject] AudioSystem audioSystem;
        public Vector3 Velocity { get; set; }
        public Vector3 InitialScale { get; private set; }

        [SerializeField] private ProjectileImpactor projectileImpactor;

        [Header("Projectile Settings")]
        [SerializeField] private bool spike = false;
        [SerializeField] private bool friendlyFire = false;

        [Tooltip("Test the SEGMENT this projectile crossed each frame for prism contact, " +
                 "instead of relying on the PhysX trigger at the point it landed on.\n\n" +
                 "A projectile is a TELEPORT, not a sweep: the mover writes " +
                 "position += Velocity·Δt and PhysX samples the trigger once per physics " +
                 "step. A Sparrow round at its base 375 u/s crosses 6.25 u per frame behind " +
                 "a 1.65-diameter hit sphere, so ~74% of its path is never tested for " +
                 "collision at all — and at high SPACE it is ~97%. That reads in play as a " +
                 "gun that cannot clear a small area no matter how much you shoot, and it is " +
                 "why oversizing the collider 'fixed' the feel: a big enough ball closes the " +
                 "per-frame gap.\n\n" +
                 "With this on, prism contact comes from the swept query ONLY (the trigger " +
                 "path is suppressed for prisms, so nothing double-dispatches) and the hit " +
                 "volume can be the size the projectile actually looks.")]
        [SerializeField] private bool sweptPrismDetection = false;

        /// <summary>True when prism contact for this projectile is owned by the swept
        /// segment query rather than the PhysX trigger — read by
        /// <c>ProjectileImpactor</c> to suppress the trigger's prism case.</summary>
        public bool UsesSweptPrismDetection => sweptPrismDetection;

        [Header("In-flight Growth (MASS)")]
        [Tooltip("A see-through shell that DRAWS this round's hit volume while MASS in-flight " +
                 "growth swells it (Shader Graphs/ProjectileChargeField).\n\n" +
                 "Growth is a HIT VOLUME, not a size. The tracer MODEL is the size it left the " +
                 "muzzle for the whole flight — swelling it turned a small ship firing needles " +
                 "into a small ship firing cannonballs — so this shell is what the player " +
                 "actually reads the growth off, and it is additive and mostly empty so an " +
                 "enormous round never hides the arena behind it.\n\n" +
                 "Sized every frame to exactly the swept hit radius, so the shell IS the hit " +
                 "volume rather than an impression of it. Leave empty on a round that does not " +
                 "grow, or on one whose transform IS its hit volume (the turret's carried " +
                 "collider) — an unassigned shell is simply never shown.")]
        [SerializeField] private Transform chargeField;

        [Tooltip("The SECOND way to satisfy \"the size you see is the size that hits\": instead of " +
                 "a fixed model and a shell drawing the growing hit volume, the MODEL grows and " +
                 "the round's sphere collider is FITTED to it every frame — same radius as the " +
                 "model at its widest, front surface exactly on the model's tip.\n\n" +
                 "Empty (every round but the skyburst) = the shell path above. Point it at a " +
                 "VISUAL CHILD and that child grows while the collider tracks it.\n\n" +
                 "Use it where the round has a READABLE BODY worth growing, and the shell would " +
                 "have nothing to draw. The skyburst is that case: it launches at bay size " +
                 "(~1.7 u long) and swells into the warhead that detonates, so the missile IS " +
                 "the hit volume. A round whose model is a 20-long tracer streak is not — the " +
                 "streak is a smear, not a body, and growing it draws a cannonball.")]
        [SerializeField] private Transform flightGrowthTarget;

        [Tooltip("On: grow every axis, keeping the round's proportions — for a compact round " +
                 "whose length is not a streak.\n\n" +
                 "Off (default): grow the CROSS-SECTION only (the target's local x/y, with " +
                 "flight along +z). The tracer mesh is a 20-long dart, so scaling it " +
                 "uniformly at 6x would draw a 120-unit needle across a ~72-unit range: " +
                 "width is what a hit volume is made of, length is just the streak.")]
        [SerializeField] private bool flightGrowthUniform = false;

        [Tooltip("What FRACTION of the flight the swell takes. 1 (default) = the round is " +
                 "still growing when it arrives, so its size reports how far it has come — " +
                 "the full-auto tracer.\n\n" +
                 "Below 1 it reaches full size that early and HOLDS for the rest of the " +
                 "flight. The skyburst missile is 0.2: it swells over the first fifth (~0.6 s, " +
                 "~70 u from the bay) and then flies as the thing it will arrive as, so what " +
                 "you are looking at from the moment it clears the hull is the real round.")]
        [SerializeField, Range(0.01f, 1f)] private float flightGrowthCompleteAt01 = 1f;

        [Header("Data Containers")]
        [SerializeField] private ThemeManagerDataContainerSO _themeManagerData;

        public float Charge { get; private set; }
        public ProjectileType Type { get; private set; }
        public float ProjectileTime { get; private set; }

        public Domains OwnDomain { get; private set; }
        public IVesselStatus VesselStatus { get; private set; }

        /// Incremented on every launch. Delayed continuations (e.g. the detonator's
        /// post-explosion pool return) must capture this and bail if it has moved on,
        /// so a stale callback can't act on a pooled-and-reissued instance.
        public int FlightGeneration { get; private set; }

        /// Per-shot: destroyed on its first prism impact (the sub-level-5 SPACE default;
        /// the 'Piercing Bullets' upgrade clears it at fire time). Detonating projectiles
        /// manage their own return through the detonator and leave this false.
        public bool StopOnFirstPrismImpact { get; private set; }

        /// Per-shot: the CHARGE level-5 'Domain-Safe Skybursts' upgrade — direct-hit
        /// damage spares prisms of the shooter's own domain. Snapshot at fire time.
        public bool SpareOwnDomain { get; private set; }

        /// Per-shot: this projectile is a PART of a pooled host object (the Sparrow's
        /// turret prism carries one), not an instance of the projectile pool. Its
        /// lifetime belongs to the host, so <see cref="ReturnToFactory"/> stops the
        /// flight and returns nothing — without this the null-factory branch would
        /// Destroy the host's child on the first stopping impact.
        public bool IsCarriedByHost { get; private set; }

        /// <summary>
        /// How many further generations of the chain reaction this round may still seed —
        /// the Urchin's spike cascade (<c>ProjectileChainFirePrismEffectSO</c>). It is the
        /// same quantity the gun calls <c>energy</c>: it picks the projectile TIER from
        /// <see cref="ProjectileFactory.GetProjectile"/> and it is the recursion depth, so the
        /// two can never drift apart.
        ///
        /// **Zero is TERMINAL and that is load-bearing.** The 2023 original had no depth cap
        /// at all: its base tier fired a volley whose children were also base tier, so the
        /// only thing that ever stopped the cascade was territory conversion (a prism already
        /// yours is skipped by <see cref="DisallowImpactOnPrism"/>). That emergent brake is
        /// still the PRIMARY one and is deliberately kept — this counter is the second,
        /// authored brake that bounds the worst case, and the per-frame volley budget in
        /// <see cref="ChainReactionBudget"/> is the third.
        ///
        /// Per-FLIGHT: cleared by <see cref="Initialize"/> so a pooled reissue never inherits
        /// the previous shot's remaining depth.
        /// </summary>
        public int ChainGeneration { get; private set; }

        /// <summary>Stamped by <see cref="Gun.FireSingle"/> from the volley's energy.</summary>
        public void SetChainGeneration(int generation) => ChainGeneration = Mathf.Max(0, generation);

        /// <summary>
        /// The firing vessel's SPACE reach, carried DOWN the cascade so every generation
        /// inherits the range the pilot paid for. The vessel's gun stamps it on the first
        /// volley; each spike hands it to its own <see cref="LoadedGun"/>, which stamps it on
        /// the volley it fires — so one authored multiplier reaches the last generation
        /// without any generation having to look back up at the vessel (which, by then, may be
        /// dead, respawned, or on the other side of the cell). 1 = unscaled.
        /// </summary>
        public float ChainRangeScale { get; private set; } = 1f;

        public void SetChainRangeScale(float scale) => ChainRangeScale = Mathf.Max(0.01f, scale);

        /// <summary>
        /// How much of its reach each generation hands to the next: the children of this spike
        /// launch at <see cref="ChainRangeScale"/> × this. Below 1 the cascade visibly runs out
        /// of steam as it spreads, which is what makes a deep chain feel like a wave rather
        /// than an expanding sphere.
        ///
        /// The Urchin's SPACE level-5 upgrade ("Deep Cascade") sets it to 1 so the wavefront
        /// keeps its full reach to the last generation. Propagated unchanged down the chain, so
        /// the pilot's upgrade state at FIRE time governs the whole cascade even if their level
        /// moves while it is still running.
        /// </summary>
        public float ChainRangeFalloff { get; private set; } = 1f;

        public void SetChainRangeFalloff(float falloff) => ChainRangeFalloff = Mathf.Clamp(falloff, 0.05f, 1f);


        /// <summary>
        /// Raised the instant this flight ends — at BOTH death points: the lifetime
        /// expiring in <see cref="MoveProjectileAsync"/>, and a stopping prism impact
        /// in <c>ProjectileImpactor</c>. The bool is true when a prism stopped it.
        ///
        /// This is "wherever the bullet would be destroyed" made addressable: the
        /// Sparrow's Turret Stance anchors its prism here instead of the shot simply
        /// vanishing. Per-FLIGHT — cleared by <see cref="Initialize"/>, so a pooled
        /// reissue never carries the previous shooter's handler.
        /// </summary>
        public event Action<Projectile, bool> FlightEnded;

        /// <summary>Raises <see cref="FlightEnded"/> exactly once per flight.</summary>
        internal void RaiseFlightEnded(bool stoppedByImpact)
        {
            if (_flightEndRaised) return;
            _flightEndRaised = true;
            FlightEnded?.Invoke(this, stoppedByImpact);
        }

        bool _flightEndRaised;

        private MeshRenderer meshRenderer;
        private Collider _rootCollider;

        // NEW: remember pooled parent so we can restore it
        private Transform _pooledParent;

        // Replaces Coroutine
        private CancellationTokenSource _moveCts;

        // Factory reference
        private ProjectileFactory _factory;

        private bool _poolParentCaptured;
        private bool _detachOnLaunch;
        private bool _detachedThisFlight;

        private void OnEnable()
        {
            if (!_poolParentCaptured)
            {
                _pooledParent = transform.parent;
                _poolParentCaptured = true;
            }

            // The detonator disables the root collider on detonation (DisableColliderNow) and
            // pool reuse must undo that, or every detonated missile is reissued as a
            // fly-through-everything dud.
            if (_rootCollider) _rootCollider.enabled = true;

            // Proximity collider-LOD: a projectile in flight is a focus, so the prism
            // colliders along its path (including distant structures it was fired at)
            // are awake by the time it arrives. Unregistered on pool return.
            PrismColliderLodManager.RegisterFocus(transform);
        }

        private void OnDisable()
        {
            PrismColliderLodManager.UnregisterFocus(transform);

            // A pooled round is reissued to a different pilot at a different MASS level, so it
            // must not carry the last flight's shell back out with it. LaunchProjectile decides
            // the state again from that shot's own growth factor.
            if (chargeField) chargeField.gameObject.SetActive(false);
        }

        private void Awake()
        {
            InitialScale = transform.localScale;
            // A growth target that is NOT the root is never re-sized by
            // ApplyIntendedWorldScale, so the size it was authored at is the only baseline it
            // has — and a pooled reissue must start from it rather than from where the last
            // flight's growth left it.
            _growthTargetAuthoredScale = GrowthTarget.localScale;
            _rootCollider = GetComponent<Collider>();
            _loadedGun = GetComponent<LoadedGun>();   // only chain spikes carry one

            // cache whatever parent it has in the pool (ship container or pool root)
            _pooledParent = transform.parent;

            CacheTransformRole();

            if (chargeField) chargeField.gameObject.SetActive(false);
        }

        /// <summary>
        /// Decides — once, from the prefab itself — whether MASS growth may scale this
        /// projectile's TRANSFORM, and the rule is exactly "does this transform draw a model?"
        ///
        /// Two structurally different things call themselves a projectile here. The bullets'
        /// <c>SparrowProjectile</c> puts its tracer mesh on its own root, so its transform IS
        /// the model and scaling it is what made growth look wrong. The Turret Stance's carried
        /// <c>ProjectileCollider</c> (a child of the fired prism) has no renderer at all — it is
        /// a bare hit sphere, its transform IS the hit volume, and scaling it is the only way
        /// its growth reaches PhysX.
        ///
        /// Derived rather than authored so it cannot be mis-set: a projectile that grows a
        /// visible body is a bug in any prefab, present or future, and this answers it from the
        /// prefab's own contents. The charge shell is excluded — it draws the hit volume, not
        /// the round.
        /// </summary>
        void CacheTransformRole()
        {
            _transformIsHitVolume = true;

            var renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (!r) continue;
                if (chargeField && r.transform.IsChildOf(chargeField)) continue;
                _transformIsHitVolume = false;
                return;
            }
        }

        /// <summary>True when nothing visible hangs off this transform, so growth may scale
        /// it. See <see cref="CacheTransformRole"/>.</summary>
        bool _transformIsHitVolume = true;

        /// The gun a chain spike fires its next generation from; null on every ordinary round.
        private LoadedGun _loadedGun;

        /// <summary>
        /// Paints a spike in its firing domain's colours. Called from
        /// <see cref="LaunchProjectile"/> — NOT from <c>Start</c>, where it used to live.
        ///
        /// <c>Start</c> runs once per INSTANCE: on a fresh object it runs before
        /// <see cref="Initialize"/> has supplied <see cref="OwnDomain"/> (so the spike wore
        /// whatever <c>Domains.Blue</c> maps to), and on every subsequent pull from the pool
        /// it does not run at all (so a spike recycled from a Ruby pilot stayed Ruby in a Jade
        /// player's hands). Domain is re-read per flight for the same reason the gun re-reads
        /// it at fire time: domains re-pick at runtime and must never be snapshotted.
        ///
        /// The material is assigned as <c>sharedMaterial</c> — the theme's own asset, one per
        /// domain — so spikes of a domain still batch; the per-instance opacity that the
        /// launch and the embed fade animate rides a MaterialPropertyBlock instead of the
        /// per-renderer clone `.material` would mint.
        /// </summary>
        void ApplySpikeAppearance()
        {
            if (!spike) return;
            if (!meshRenderer) meshRenderer = GetComponent<MeshRenderer>();
            if (!meshRenderer || _themeManagerData == null) return;

            var domainMaterial = _themeManagerData.GetTeamSpikeMaterial(OwnDomain);
            if (domainMaterial) meshRenderer.sharedMaterial = domainMaterial;

            SetSpikeOpacity(0.5f);
        }

        /// <summary>Per-instance opacity via MPB — never <c>renderer.material</c>.</summary>
        internal void SetSpikeOpacity(float opacity)
        {
            if (!meshRenderer) return;
            _mpb ??= new MaterialPropertyBlock();
            meshRenderer.GetPropertyBlock(_mpb);
            _mpb.SetFloat(OpacityId, opacity);
            meshRenderer.SetPropertyBlock(_mpb);
        }

        MaterialPropertyBlock _mpb;

        /*private void OnDestroy()
        {
            CSDebug.LogError("Projectile destroyed! Should not happen! Should return to pool!");
        }*/

        #region Initialization
        public virtual void Initialize(ProjectileFactory factory, Domains ownDomain, IVesselStatus vesselStatus, float charge, bool detachOnLaunch = false,
            bool stopOnFirstPrismImpact = false, bool spareOwnDomain = false, bool carriedByHost = false)
        {
            _factory = factory;
            OwnDomain = ownDomain;
            VesselStatus = vesselStatus;
            Charge = charge;
            _detachOnLaunch = detachOnLaunch;
            StopOnFirstPrismImpact = stopOnFirstPrismImpact;
            SpareOwnDomain = spareOwnDomain;
            IsCarriedByHost = carriedByHost;

            // Per-flight: a pooled reissue must not inherit the previous shooter's
            // end-of-flight handler, and the once-only latch must re-arm.
            FlightEnded = null;
            _flightEndRaised = false;

            // Likewise the previous shot's MASS growth — a caller that does not set it gets
            // the un-grown default rather than whoever fired this instance last.
            _flightGrowthFactor = 1f;
            _intendedWorldScale = Vector3.zero;

            // Per-flight chain state. Without these two an instance recycled out of the pool
            // would carry the previous cascade's remaining depth (deepening the chain for
            // free) and its spent embed latch (making the round un-embeddable for life).
            ChainGeneration = 0;
            ChainRangeScale = 1f;
            ChainRangeFalloff = 1f;
            _embedded = false;

            // A chain spike carries its own gun. It is a pooled prefab, so it cannot be
            // authored with the scene's factory or with a pilot — both arrive here, per
            // flight, from whoever fired this round. Re-supplied every flight rather than
            // once, because a pooled instance changes hands between pilots and domains.
            if (_loadedGun)
            {
                _loadedGun.SetProjectileFactory(factory);
                _loadedGun.Initialize(vesselStatus);
            }
        }

        public void SetType(ProjectileType type) => Type = type;

        /// <summary>
        /// The size this round should be IN THE WORLD, handed over by the gun at fire time and
        /// applied by <see cref="ApplyIntendedWorldScale"/> at launch.
        ///
        /// It is deliberately NOT applied by the gun. A round spawns parented to its fire
        /// container so it inherits that container's motion for the spawn frame, and a
        /// container is a POSE source, never a size source - its scale must not reach the
        /// round's mesh, collider or sweep radius. Cancelling it from the gun works only while
        /// the container's scale is UNIFORM: a local scale cannot undo a non-uniform parent
        /// that is also rotated (the product shears). A CHAIN hop's container is the previous
        /// spike itself, non-uniform at (0.4, 0.4, 2) and rotated to its own flight direction,
        /// so that error compounded once per generation.
        /// </summary>
        Vector3 _intendedWorldScale = Vector3.zero;

        public void SetIntendedWorldScale(Vector3 worldScale) => _intendedWorldScale = worldScale;

        /// <summary>
        /// Resolve <see cref="_intendedWorldScale"/> against whatever parent the round has
        /// AFTER the launch-time detach. Unparented (every spike, and every detachOnLaunch
        /// round) is the exact case: local scale IS world scale, so no compensation is needed
        /// and none can go wrong. A round that stays parented divides its parent's lossy scale
        /// out, which is exact for the uniform containers those rounds actually use.
        /// </summary>
        void ApplyIntendedWorldScale()
        {
            if (_intendedWorldScale == Vector3.zero) return;

            var parent = transform.parent;
            if (!parent)
            {
                transform.localScale = _intendedWorldScale;
                return;
            }

            var lossy = parent.lossyScale;
            transform.localScale = new Vector3(
                _intendedWorldScale.x / Mathf.Max(Mathf.Abs(lossy.x), 1e-4f),
                _intendedWorldScale.y / Mathf.Max(Mathf.Abs(lossy.y), 1e-4f),
                _intendedWorldScale.z / Mathf.Max(Mathf.Abs(lossy.z), 1e-4f));
        }
        #endregion

        #region Impact Checks
        /// <summary>
        /// Two independent reasons a projectile ignores a prism:
        /// (1) authored friendly-fire off for this projectile family (domain gate);
        /// (2) the prism's PLACEMENT-IMMUNITY window (<see cref="Prism.ProjectileImmuneUntil"/>)
        ///     is still open. The Sparrow turret stamps a window on each fired prism spanning
        ///     its flight (it is parked live at the anchor from fire time) plus a short settle
        ///     after placement — a shot cannot destroy its own delivery, and a spray cannot
        ///     erase its own freshest output. One TIME rule instead of identity/owner special
        ///     cases; once the window closes the prism is ordinary friendly-fire mass.
        /// </summary>
        public bool DisallowImpactOnPrism(Prism prism) =>
            (!friendlyFire && prism.Domain == OwnDomain) || Time.time < prism.ProjectileImmuneUntil;
        public bool DisallowImpactOnVessel(Domains vesselDomain) => vesselDomain == OwnDomain;
        #endregion

        public void LaunchProjectile(float projectileTime)
        {
            // A carried projectile has no factory BY DESIGN — its lifetime belongs to the
            // pooled host it is part of — so only shout about a missing one when there
            // should have been one.
            if (!_factory && !IsCarriedByHost)
            {
                CSDebug.LogError("No factory for this projectile found. Can't return to pool!");
            }

            FlightGeneration++;
            if (audioSystem)
                audioSystem.PlayGameplaySFX(GameplaySFXCategory.ProjectileLaunch, transform.position);
            ProjectileTime = projectileTime;

            if (_detachOnLaunch && transform.parent)
            {
                transform.SetParent(null, true);
                _detachedThisFlight = true;
            }
            else
            {
                _detachedThisFlight = false;
            }

            // === DETACH when spawned if it's a spike ===
            // A chain spike outlives the muzzle that fired it — its parent is very often
            // ANOTHER SPIKE that is about to retire into the pool — so it must fly in world
            // space or it would be dragged (and deactivated) with its parent.
            if (spike)
            {
                transform.SetParent(null, true);   // keep world position/rotation
                _detachedThisFlight = true;
                ApplySpikeAppearance();
            }

            Stop(); // Stop any running movement before starting a new one

            // Size the round now that its parent chain is FINAL (both detach paths above have
            // run). Must precede CacheSweepRadius and the _launchScale capture below, or the
            // hit radius and the MASS growth baseline are taken from the pre-correction scale.
            ApplyIntendedWorldScale();

            // After every scale/parent change above — the carried turret collider is sized
            // per shot, so this cannot be cached at Awake.
            if (sweptPrismDetection) CacheSweepRadius();

            // The growth baseline is whatever this shot actually launched at (the gun applies
            // projectileScale, the turret sizes its carried collider per shot), never the
            // prefab's InitialScale.
            //
            // A CHILD target has no such per-shot sizing pass, so last flight's growth would
            // still be sitting on it: restore the authored scale first, or a pooled missile
            // launches at the size the previous one died at and compounds every reuse.
            var growthTarget = GrowthTarget;
            if (growthTarget != transform)
                growthTarget.localScale = _growthTargetAuthoredScale;

            _flightGrowthSettled = false;
            _launchScale = growthTarget.localScale;
            _launchSweepRadius = _sweepRadius;

            // A model that IS the hit volume needs its sphere measured from the model, at the
            // authored scale restored just above. Re-measured per flight rather than at Awake
            // because the parent chain (and so the root-local matrix) is only final here.
            CaptureModelHitSphere();
            FitColliderToModel(1f);

            // The charge shell only exists to sell growth, so a round that does not grow never
            // shows one. Sized here as well as in the flight loop: the loop's first tick lands a
            // frame later, and until then the shell would render at whatever scale the prefab
            // happened to author.
            if (chargeField)
            {
                bool grows = _flightGrowthFactor != 1f;
                if (grows) SizeChargeField();
                if (chargeField.gameObject.activeSelf != grows)
                    chargeField.gameObject.SetActive(grows);
            }

            _moveCts = CancellationTokenSource.CreateLinkedTokenSource(
                this.GetCancellationTokenOnDestroy());
            MoveProjectileAsync(projectileTime, _moveCts.Token).Forget();
        }

        public void ReturnToFactory()
        {
            Stop();

            // Carried by a pooled HOST (the turret prism): the host owns the lifetime
            // and its own end-of-flight handler does the reattach. Returning here would
            // fall through to the null-factory branch and Destroy the host's child.
            if (IsCarriedByHost) return;

            // Only reattach if we had detached for this flight
            if (_detachedThisFlight && _pooledParent != null && transform.parent == null)
            {
                transform.SetParent(_pooledParent, false);
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
                transform.localScale    = Vector3.one; // or InitialScale
            }

            if (_factory) _factory.ReturnProjectile(this);
            else
            {
                // This should not happen, make sure to handle later
                CSDebug.LogWarning("No projectile factory found to release projectile!");
                Destroy(gameObject);
            }
        }

        #region Embed (the Urchin spike's landing)

        bool _embedded;

        /// <summary>
        /// Halts this round where it struck and leaves it standing in the prism for
        /// <paramref name="dwellSeconds"/>, then fades it out and returns it to the pool.
        /// This is the modern <c>TrailBlockImpactEffects.Stop</c> — the Urchin spike sticking
        /// into the mass it just converted.
        ///
        /// **It owns the pool return, and that is the whole point.** The 2023 original
        /// implemented Stop as a bare <c>StopCoroutine(moveCoroutine)</c>, which killed the
        /// coroutine whose terminal statement was the only <c>Destroy</c>/<c>ReturnToPool</c>
        /// call — so every spike that actually hit something (i.e. every spike that mattered)
        /// was immortal, and after the pool port it permanently drained a 1,500-deep pool.
        /// Cancelling <see cref="_moveCts"/> here has exactly that shape —
        /// <see cref="MoveProjectileAsync"/> swallows the cancellation and never reaches its
        /// tail — so the retirement must be, and is, explicit.
        ///
        /// Fading rather than vanishing is the continuity-of-existence law: nothing the player
        /// can see may pop out of existence.
        /// </summary>
        public void EmbedAndRetire(float dwellSeconds, float fadeSeconds = 0.35f)
        {
            if (_embedded) return;
            _embedded = true;

            Stop();                     // halt the mover; its tail will NOT run
            Velocity = Vector3.zero;

            EmbedAndRetireAsync(dwellSeconds, fadeSeconds, FlightGeneration,
                                this.GetCancellationTokenOnDestroy()).Forget();
        }

        async UniTaskVoid EmbedAndRetireAsync(float dwellSeconds, float fadeSeconds,
                                              int generation, CancellationToken token)
        {
            try
            {
                if (dwellSeconds > 0f)
                    await UniTask.Delay(System.TimeSpan.FromSeconds(dwellSeconds),
                                        DelayType.DeltaTime, PlayerLoopTiming.Update, token);

                // The instance may have been reissued while we waited (an embed dwell easily
                // outlives a pool round-trip). Acting on it now would retire someone else's
                // shot mid-flight.
                if (generation != FlightGeneration) return;

                if (spike && meshRenderer && fadeSeconds > 0f)
                {
                    const float from = 0.5f;      // the opacity a spike flies at
                    float t = 0f;
                    while (t < fadeSeconds && !token.IsCancellationRequested)
                    {
                        t += Time.deltaTime;
                        SetSpikeOpacity(Mathf.Lerp(from, 0f, t / fadeSeconds));
                        await UniTask.Yield(PlayerLoopTiming.Update, token);
                        if (generation != FlightGeneration) return;
                    }
                }

                if (generation != FlightGeneration) return;

                RaiseFlightEnded(stoppedByImpact: true);
                ReturnToFactory();
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                CSDebug.LogError($"[Projectile] Embed retire error: {ex}");
            }
        }

        static readonly int OpacityId = Shader.PropertyToID("_Opacity");

        #endregion

        private async UniTaskVoid MoveProjectileAsync(float projectileTime, CancellationToken token)
        {
            float elapsedTime = 0f;
            var t = transform; // cache
            var useSpike = spike && meshRenderer;

            try
            {
                while (elapsedTime < projectileTime && !token.IsCancellationRequested)
                {
                    float deltaTime = Time.deltaTime;
                    float factor = Mathf.Cos(elapsedTime * Mathf.PI / (2f * projectileTime));

                    // Grow BEFORE the step is swept, so this frame's hit volume is the size the
                    // round has actually reached rather than the one it left the muzzle at.
                    // Latched: a round that finished swelling early holds a size that will not
                    // change again, and re-writing that transform every frame for the rest of
                    // the flight would dirty its hierarchy for nothing.
                    if (_flightGrowthFactor != 1f && !_flightGrowthSettled)
                        ApplyFlightGrowth(elapsedTime / projectileTime);

                    Vector3 sweepFrom = t.position;
                    t.position += Velocity * (deltaTime * factor);

                    if (sweptPrismDetection)
                    {
                        SweepPrismsAlong(sweepFrom, t.position);

                        // A stopping impact has already run the whole end-of-flight path
                        // (RaiseFlightEnded + ReturnToFactory). Returning rather than
                        // breaking is deliberate: the loop's tail would otherwise fire the
                        // end effects a second time on an instance already back in the pool.
                        if (_flightEndRaised)
                            return;
                    }

                    if (useSpike)
                    {
                        float percentRemaining = elapsedTime / projectileTime;
                        if (percentRemaining > 0.9f)
                            SetSpikeOpacity(1f - Mathf.Pow(percentRemaining, 4f));
                    }

                    elapsedTime += deltaTime;
                    await UniTask.Yield(PlayerLoopTiming.PreLateUpdate, token);
                }

                // Death point #1: the lifetime expired. Signal before the end effects,
                // so a host that leaves something behind (the turret prism's anchor)
                // acts on the position the shot actually reached rather than a pooled
                // instance that has already been reset.
                RaiseFlightEnded(stoppedByImpact: false);

                projectileImpactor.ExecuteEndEffects();
                // ReturnToFactory(); // handled by end effects (delayed)
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                CSDebug.LogError($"[Projectile] Move loop error: {ex}");
            }
        }

        #region Swept prism detection

        /// <summary>
        /// Extra radius added when GATHERING candidates, on top of the projectile's own hit
        /// radius. <see cref="PrismSpatialIndex.QuerySegment"/> tests a prism's CENTRE, so
        /// without an allowance a prism whose body crosses the path but whose centre sits
        /// beside it would be missed. Sized to comfortably exceed the bounding radius of the
        /// largest prism the guns meet (a 0.8×0.5×5 turret prism is ~2.5, stretched by MASS);
        /// every candidate is then re-tested precisely against its own bounds, so a generous
        /// value costs a few extra distance checks, never a false hit.
        /// </summary>
        const float SweepCandidateExtent = 8f;

        readonly struct SweepHit
        {
            public readonly float T;                 // parameter along this frame's segment
            public readonly PrismImpactor Impactor;
            public SweepHit(float t, PrismImpactor impactor) { T = t; Impactor = impactor; }
        }

        // Shared scratch, RENTED BY DEPTH. The sweep is main-thread but it is NOT
        // non-reentrant, which an earlier comment here asserted and the Urchin disproved:
        // dispatching a swept hit runs the effect list synchronously, ProjectileChainFire
        // fires the next generation from inside it, and that child's MoveProjectileAsync runs
        // synchronously up to its first await - which is PAST its own first SweepPrismsAlong.
        // So a child clears and refills the very list its parent is mid-iteration over,
        // dropping the parent's remaining hits and re-dispatching converted prisms. Depth is
        // bounded by ChainGeneration (<= 4) and the per-frame volley budget, so a small stack
        // of buffers covers it; the lists grow on demand and are never freed.
        static readonly List<List<Prism>> s_candidatePool = new();
        static readonly List<List<SweepHit>> s_hitPool = new();
        static int s_sweepDepth;

        static (List<Prism> candidates, List<SweepHit> hits) RentSweepBuffers(int depth)
        {
            while (s_candidatePool.Count <= depth)
            {
                s_candidatePool.Add(new List<Prism>(64));
                s_hitPool.Add(new List<SweepHit>(16));
            }
            return (s_candidatePool[depth], s_hitPool[depth]);
        }
        static readonly Comparison<SweepHit> s_nearestFirst = (x, y) => x.T.CompareTo(y.T);

        float _sweepRadius = 0.5f;

        // ---- in-flight growth (MASS) ----
        float _flightGrowthFactor = 1f;
        Vector3 _launchScale = Vector3.one;
        Vector3 _growthTargetAuthoredScale = Vector3.one;
        bool _flightGrowthSettled;
        float _launchSweepRadius = 0.5f;

        /// <summary>
        /// How many times its launch size this round swells to by the end of its flight. Set
        /// per shot from the vessel's live MASS level; 1 = no growth (every projectile that
        /// does not opt in). WHAT grows — the root's hit volume or a visual child, on the
        /// cross-section or on every axis — is the prefab's business, not the shooter's; see
        /// <see cref="ApplyFlightGrowth"/>.
        /// </summary>
        public void SetFlightGrowth(float factor) => _flightGrowthFactor = Mathf.Max(0.01f, factor);

        /// <summary>
        /// Grows the round's HIT VOLUME toward its full factor across the flight, and draws
        /// that volume with the charge shell — the size you see is the size that hits.
        ///
        /// **Growth is a hit volume, not a size.** The tracer mesh is a unit sphere at
        /// (0.75, 0.75, 20) — a 20-long dart — and swelling its cross-section 6× drew a fat
        /// lozenge: mechanically right, and it read as a small ship firing cannonballs, which
        /// is the exact silliness the growth pass set out to avoid. So the MODEL now stays the
        /// size it left the muzzle for the whole flight and <see cref="chargeField"/> — a
        /// see-through, additive, mostly-empty shell — is what carries the read. It is sized to
        /// exactly <see cref="_sweepRadius"/>, so it is not an impression of the hit volume, it
        /// IS the hit volume, and a pilot can still see the arena through their own enormous
        /// round.
        ///
        /// The transform is scaled only when it is not drawing anything
        /// (<see cref="CacheTransformRole"/>) — the Turret Stance's carried collider, where the
        /// transform IS the hit volume and scaling it is the only way growth reaches PhysX.
        ///
        /// The hit radius is scaled EXPLICITLY rather than re-derived from lossyScale, because
        /// a SphereCollider takes the largest lossy component: with the dart's length left
        /// alone, that stays the z-stretch and the derived radius would never move. (Consequence,
        /// and a deliberate scope line kept from the growth pass: the swept PRISM radius grows,
        /// while the PhysX radius the vessel/mine path uses is unchanged for the dart. Growing
        /// bullets against vessels is a Dog Fight balance change, not a prism-clearing one.)
        ///
        /// **<see cref="flightGrowthTarget"/> is the one exception, and it is a narrow one.**
        /// A round whose MODEL was authored far smaller than the hit volume it already had has
        /// no growing hit volume to draw — a shell would show a sphere that never moves — and
        /// growing that model walks it INTO its own reach rather than out past it. The skyburst
        /// missile is the only one: ~1.7 u long inside an 8.5 u-RADIUS sphere. Its collider and
        /// its launch scale are untouched, so its reach does not move either.
        ///
        /// **When it grows is <see cref="flightGrowthCompleteAt01"/>** — the whole flight
        /// (the tracer, whose size therefore reports how far it has come) or an early window
        /// it then holds (the missile). See <see cref="RoundGrowthRamp"/>.
        /// </summary>
        void ApplyFlightGrowth(float progress01)
        {
            float g = RoundGrowthRamp.At(progress01, _flightGrowthFactor, flightGrowthCompleteAt01);

            // A settled round stops re-writing its transform — but a charge shell has to keep
            // tracking whatever its parent's scale is doing, so a round that draws one never
            // latches. Rounds on the full-flight shape never settle mid-flight anyway (the
            // mover's progress only reaches 1 at the end), so this costs the tracer nothing.
            _flightGrowthSettled = !chargeField
                && RoundGrowthRamp.IsComplete(progress01, flightGrowthCompleteAt01);

            _sweepRadius = _launchSweepRadius * g;

            if (flightGrowthTarget)
            {
                flightGrowthTarget.localScale = flightGrowthUniform
                    ? _launchScale * g
                    : new Vector3(_launchScale.x * g, _launchScale.y * g, _launchScale.z);
                FitColliderToModel(g);
            }
            else if (_transformIsHitVolume)
                transform.localScale = new Vector3(_launchScale.x * g, _launchScale.y * g, _launchScale.z);

            SizeChargeField();
        }

        /// <summary>
        /// Puts the charge shell at exactly <paramref name="worldRadius"/> in the WORLD, given
        /// whatever the parent's scale happens to be.
        ///
        /// The dart's own transform is non-uniform — (0.75, 0.75, 20) — so a uniform world sphere
        /// under it needs a per-axis divide. That is only safe because the shell is authored
        /// with identity rotation: a non-uniform parent above a ROTATED child is a shear, and no
        /// local scale can undo one. Keep the shell unrotated.
        ///
        /// The mesh is Unity's built-in sphere, whose object-space radius is 0.5 — hence the
        /// diameter, not the radius, is what the scale has to produce.
        /// </summary>
        public static Vector3 ChargeFieldLocalScale(float worldRadius, Vector3 parentLossyScale)
        {
            float diameter = 2f * Mathf.Max(worldRadius, 0f);
            return new Vector3(
                diameter / Mathf.Max(Mathf.Abs(parentLossyScale.x), 1e-4f),
                diameter / Mathf.Max(Mathf.Abs(parentLossyScale.y), 1e-4f),
                diameter / Mathf.Max(Mathf.Abs(parentLossyScale.z), 1e-4f));
        }

        /// <summary>
        /// One transform write per live round per frame, which is why the shell's whole
        /// appearance is otherwise a function of TIME and its own object-to-world matrix: at 90
        /// volleys/s over a 0.3 s flight a single Sparrow keeps ~54 of these in the air, and a
        /// per-renderer MaterialPropertyBlock (the skimmer crackle's driver) would be ~54 extra
        /// draw calls plus two 16-element vector arrays each, every frame. The shader reads its
        /// own world radius instead, so growth needs no stamp and every round in the match
        /// batches through one material.
        /// </summary>
        void SizeChargeField()
        {
            if (!chargeField) return;
            var parent = chargeField.parent;
            chargeField.localScale = ChargeFieldLocalScale(
                _sweepRadius, parent ? parent.lossyScale : Vector3.one);
        }

        // ---- the model-IS-the-hit-volume path (flightGrowthTarget) ----
        Vector3 _modelHitCentre;      // root-local, at growth 1
        Vector3 _modelHitExtents;     // root-local half-extents, at growth 1
        bool _fitsColliderToModel;

        /// <summary>
        /// Measures the growth target's drawn geometry in THIS projectile's local space, so the
        /// hit sphere can be fitted to the model rather than authored beside it.
        ///
        /// Read from the renderer's own local bounds through the renderer→root matrix, so it is
        /// correct for any nesting, rotation or child scale and hardcodes nothing about the one
        /// prefab that uses it. Mesh bounds need no Read/Write enabled.
        /// </summary>
        void CaptureModelHitSphere()
        {
            _fitsColliderToModel = false;
            if (!flightGrowthTarget || _rootCollider is not SphereCollider) return;

            var renderer = flightGrowthTarget.GetComponentInChildren<Renderer>();
            if (!renderer) return;

            var toRoot = transform.worldToLocalMatrix * renderer.transform.localToWorldMatrix;
            var local = renderer.localBounds;
            Vector3 c = local.center, e = local.extents;

            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            for (int i = 0; i < 8; i++)
            {
                var corner = toRoot.MultiplyPoint3x4(new Vector3(
                    c.x + ((i & 1) == 0 ? -e.x : e.x),
                    c.y + ((i & 2) == 0 ? -e.y : e.y),
                    c.z + ((i & 4) == 0 ? -e.z : e.z)));
                min = Vector3.Min(min, corner);
                max = Vector3.Max(max, corner);
            }

            _modelHitCentre  = (min + max) * 0.5f;
            _modelHitExtents = (max - min) * 0.5f;
            _fitsColliderToModel = true;
        }

        /// <summary>
        /// The hit sphere's radius: the model at its WIDEST across the flight axis. For a body
        /// of revolution that is its radius; the box DIAGONAL would overstate a round missile by
        /// √2, which is the mistake to avoid here.
        /// </summary>
        public static float ModelHitRadius(Vector3 halfExtents, float growth)
            => Mathf.Max(halfExtents.x, halfExtents.y) * growth;

        /// <summary>
        /// The hit sphere's centre, placed so its FRONT surface sits exactly on the model's tip.
        ///
        /// That is the whole contract: a projectile model may stick out the BACK of its collider
        /// — a tail that has already passed you cannot cause a false read — but never out the
        /// FRONT, where the nose would visibly reach a target before the hit registers. Both this
        /// and the radius are linear in growth, because the model scales about the root origin.
        /// </summary>
        public static Vector3 ModelHitCentre(Vector3 centre, Vector3 halfExtents, float growth)
        {
            float radius = ModelHitRadius(halfExtents, 1f);
            return new Vector3(centre.x, centre.y, centre.z + halfExtents.z - radius) * growth;
        }

        /// <summary>
        /// Re-fits the sphere collider to the grown model. Runs only while the round is still
        /// swelling — the settle latch stops the growth pass once the size is final, and a held
        /// size needs no further write.
        /// </summary>
        void FitColliderToModel(float growth)
        {
            if (!_fitsColliderToModel || _rootCollider is not SphereCollider sphere) return;
            sphere.radius = ModelHitRadius(_modelHitExtents, growth);
            sphere.center = ModelHitCentre(_modelHitCentre, _modelHitExtents, growth);
        }

        /// <summary>
        /// What the in-flight growth scales — the authored <see cref="flightGrowthTarget"/>,
        /// or this projectile's own root when none is wired (every round that already grew).
        /// </summary>
        Transform GrowthTarget => flightGrowthTarget ? flightGrowthTarget : transform;

        /// <summary>
        /// The projectile's hit radius in world units, cached per launch (the Sparrow's
        /// carried turret collider is re-scaled per shot, so it cannot be cached earlier).
        /// A SphereCollider takes the LARGEST lossy-scale component — the same rule that once
        /// turned a 0.3 radius on a ×20-stretched tracer into a 6.0 world radius.
        /// </summary>
        void CacheSweepRadius()
        {
            if (_rootCollider is SphereCollider sphere)
            {
                Vector3 s = _rootCollider.transform.lossyScale;
                _sweepRadius = sphere.radius *
                    Mathf.Max(Mathf.Abs(s.x), Mathf.Max(Mathf.Abs(s.y), Mathf.Abs(s.z)));
                return;
            }

            // Non-sphere colliders: the smallest half-extent is the conservative read (a long
            // dart's AABB diagonal would massively overstate its cross-section). Swept
            // detection is opt-in and every current user is a sphere.
            _sweepRadius = _rootCollider
                ? Mathf.Min(_rootCollider.bounds.extents.x,
                    Mathf.Min(_rootCollider.bounds.extents.y, _rootCollider.bounds.extents.z))
                : 0.5f;
        }

        /// <summary>
        /// Tests the segment this projectile crossed THIS FRAME for prism contact and
        /// dispatches the hits **nearest-first**, which is what makes the sub-SPACE-5
        /// "destroyed on its first prism impact" rule mean the first prism along the path
        /// rather than an arbitrary one.
        ///
        /// The projectile is moved to each contact point before its impact is dispatched, so
        /// an effect reading the shot's position — and the Turret Stance's anchor, which puts
        /// its prism "wherever the bullet would be destroyed" — sees where the shot actually
        /// met the prism, not where the frame's step happened to end.
        /// </summary>
        void SweepPrismsAlong(Vector3 from, Vector3 to)
        {
            if (!projectileImpactor) return;

            var index = PrismSpatialIndex.Instance;
            if (!index || !index.IsAvailable) return;

            int depth = s_sweepDepth++;
            try
            {
            var (s_sweepCandidates, s_sweepHits) = RentSweepBuffers(depth);

            if (index.QuerySegment(from, to, _sweepRadius + SweepCandidateExtent, s_sweepCandidates) == 0)
                return;

            Vector3 ab = to - from;
            float abLenSq = ab.sqrMagnitude;

            s_sweepHits.Clear();
            for (int i = 0; i < s_sweepCandidates.Count; i++)
            {
                var prism = s_sweepCandidates[i];
                if (!prism || prism.destroyed) continue;
                if (!prism.TryGetComponent(out PrismImpactor prismImpactor)) continue;

                Vector3 centre = prism.transform.position;
                float t = abLenSq > 1e-8f
                    ? Mathf.Clamp01(Vector3.Dot(centre - from, ab) / abLenSq)
                    : 0f;

                // Bounding-SPHERE contact rather than an exact capsule-vs-OBB test: it is a
                // few instructions instead of a narrowphase, and its error is a slightly
                // generous corner hit — the right direction to err for a saturation weapon,
                // and still far tighter than the oversized collider this replaces.
                float contact = _sweepRadius + 0.5f * prism.transform.lossyScale.magnitude;
                if ((centre - (from + ab * t)).sqrMagnitude > contact * contact) continue;

                s_sweepHits.Add(new SweepHit(t, prismImpactor));
            }

            if (s_sweepHits.Count == 0) return;
            if (s_sweepHits.Count > 1) s_sweepHits.Sort(s_nearestFirst);

            for (int i = 0; i < s_sweepHits.Count; i++)
            {
                var hit = s_sweepHits[i];
                if (!hit.Impactor) continue;

                transform.position = from + ab * hit.T;
                projectileImpactor.AcceptImpacteeFromSweep(hit.Impactor);

                // A stopping impact ran the whole end-of-flight path from inside that call;
                // the shot rests here.
                if (_flightEndRaised) return;
            }

            // Pierced everything it met — finish the frame's step.
            transform.position = to;
            }
            finally { s_sweepDepth--; }
        }

        #endregion

        void Stop()
        {
            if (_moveCts == null) return;

            _moveCts.Cancel();
            _moveCts.Dispose();
            _moveCts = null;
        }
    }
}
