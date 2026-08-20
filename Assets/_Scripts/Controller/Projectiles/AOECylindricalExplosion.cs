using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// A blast shaped like a swept PLATE: a circular disc of constant radius that starts centred
    /// on the emitting hull with its face normal along the fire direction, then sweeps that normal
    /// for a fixed length. The volume it leaves behind is a cylinder with flat end caps — the
    /// Scarab's cavitation punch (design: <c>R_VesselActions/SCARAB.md</c> §3.4), the mantis-shrimp
    /// slap that rides the dash.
    ///
    /// WHY NOT THE CONE. <see cref="AOEConicExplosion"/>'s cross-section is proportional to axial
    /// depth — that is the one property this shape refuses to have. A plate is the same width at
    /// the hull as it is at full reach, which is what makes it read as a *slap* rather than a
    /// muzzle blast, and there is no apex for anything to radiate from.
    ///
    /// THE SWEEP IS LINEAR, deliberately breaking with the eased family (both the spherical and
    /// conic blasts run <c>Sin(t·π/2)</c>). The plate's whole contract is that it has ONE velocity
    /// — <see cref="AOEExplosion.speed"/> = length / duration — which it hands to everything it
    /// claims. An eased sweep would make the plate's instantaneous speed disagree with the impulse
    /// it imparts on every frame but one.
    ///
    /// EVERYTHING IT CLAIMS LEAVES ALONG THE SWEEP. Prisms get <see cref="_axis"/> as their impact
    /// direction (the Burst job hands back the axis itself, no per-hit normalize), and
    /// <see cref="CalculateImpactVector"/> returns the same vector at every position — so a vessel
    /// caught in the plate, and an Astro League ball it reaches, are shoved the way the punch
    /// travelled instead of away from a point. That is the whole read: the beetle's strike throws
    /// mass DOWN-RANGE.
    ///
    /// SPAWNED DIRECTLY, NOT THROUGH <c>ExplosionHelper.CreateExplosion</c>. That helper
    /// special-cases the cone (<c>FindCone</c> / <c>AuthoredConeHeight</c>) and would hand a plate
    /// a cone's height as its length. Routing one through it is benign today only because a
    /// non-cone yields no height and this class then falls back to <see cref="lengthPerRadius"/> —
    /// do not rely on that; give the helper a cylinder branch first if a second caller ever needs it.
    ///
    /// GEOMETRY LIVES IN WORLD UNITS ON A SCALE-1 ROOT. The cone drives a non-uniformly scaled
    /// container and then has to divide that scale back out of its capsule trigger, anisotropically
    /// (see <c>AOEConicExplosion.UpdateCapsuleTrigger</c>). This root is never scaled: the mesh
    /// child carries the cylinder's shape and the trigger box is written in world units, so there
    /// is no lossy scale to undo and the two cannot drift apart.
    /// </summary>
    public class AOECylindricalExplosion : AOEExplosion
    {
        [Header("Plate")]
        [Tooltip("How long the sweep is, as a multiple of the plate's RADIUS. The DEFAULT 2 is " +
                 "the neutral cylinder — exactly as long as it is wide, axial span == diameter. " +
                 "Individual blasts author their own; the Scarab's plate is 1.2, a broad shallow " +
                 "slap. An explicit HeightOverride on the InitializeStruct wins over both.")]
        [SerializeField, Min(0.05f)] float lengthPerRadius = 2f;

        [Tooltip("Sweep SPEED in world units/second. The plate's LENGTH is derived from the " +
                 "emitting hull, so holding the SPEED fixed (and letting the duration follow) is " +
                 "what keeps a resized punch feeling identical - it is the wavefront speed every " +
                 "impulse downstream is built from. 0 falls back to the authored " +
                 "ExplosionDuration. An explicit DurationOverride still wins over both.")]
        [SerializeField, Min(0f)] float sweepSpeed = 257.14f;

        [Tooltip("Seconds to hold the trigger at FULL extent after the sweep finishes, before " +
                 "retiring it. Trigger events are raised in the PHYSICS step while the sweep loop " +
                 "runs in Update, so the final and largest shape would otherwise be written and " +
                 "disabled inside one frame and never simulated. Floored at 2x fixedDeltaTime at " +
                 "run time, so this cannot silently become too short if the project's fixed " +
                 "timestep changes. 0 disables the hold entirely.")]
        [SerializeField, Min(0f)] float contactHoldSeconds = 0.05f;

        [Tooltip("The cylinder MESH. Unity's built-in cylinder is 2 units tall along its local Y " +
                 "and 1 unit across, so this child is posed with a +90 degree X roll (local +Y -> " +
                 "root +Z, the sweep axis) and scaled (diameter, halfLength, diameter). Leave it " +
                 "empty and the blast damages correctly but draws nothing.")]
        [SerializeField] Transform plateVisual;

        /// <summary>World-space unit vector the plate's face normal points along — the sweep
        /// direction, and the direction everything this blast claims is thrown.</summary>
        Vector3 _axis = Vector3.forward;

        /// <summary>The plate's radius in world units — CONSTANT for the whole sweep.</summary>
        float _radius;

        /// <summary>Total axial reach of the sweep in world units.</summary>
        float _length;

        BoxCollider _triggerBox;
        MaterialPropertyBlock _platePropertyBlock;
        static readonly int OpacityID = Shader.PropertyToID("_Opacity");

        /// <summary>The plate's radius, for a caller that wants to describe the volume it is
        /// about to sweep (a preview, a HUD readout) without re-deriving it.</summary>
        public float PlateRadius => _radius;

        /// <summary>The plate's axial reach.</summary>
        public float PlateLength => _length;

        public override void Initialize(InitializeStruct initStruct)
        {
            transform.SetPositionAndRotation(initStruct.SpawnPosition, initStruct.SpawnRotation);
            AnonymousExplosion = initStruct.AnnonymousExplosion;
            Vessel = initStruct.Vessel;
            Domain = initStruct.OwnDomain;
            MaxScale = initStruct.MaxScale;

            ApplyAffectSelfOverride(initStruct);
            ApplyDevastatingOverride(initStruct);

            // MaxScale is the plate's DIAMETER, matching the rest of the AOE family (the cone's
            // MaxScale is its base diameter). Length follows the authored ratio unless the caller
            // states one outright.
            _radius = Mathf.Max(MaxScale * 0.5f, 0f);
            _length = initStruct.HeightOverride > 0f
                ? initStruct.HeightOverride
                : _radius * lengthPerRadius;

            // THE PLATE'S SPEED IS AUTHORED; ITS DURATION FOLLOWS. Everything the blast imparts is
            // built from `speed` (AOEExplosion.speed = reach / duration) - prism debris, shatter
            // rate, the vessel debuff's impulse, the Astro League ball's kick - so a blast whose
            // REACH is derived from the hull would silently retune all of them if the duration
            // were held fixed instead. That is not hypothetical: this plate's reach has already
            // been re-derived twice on one branch (90 -> 18 when it was first sized off the hull,
            // 18 -> 54 when the hull ratio was retuned), and at a fixed 0.35s the first of those
            // alone would have dropped the play-tested 257 u/s wavefront to 51 and taken the ball
            // kick from 86% of its top speed to 17% - undoing SCARAB.md's "a punch should read as
            // a punch, not a bloom" pass without touching a number anyone would think to look at.
            // Authoring the SPEED makes every one of those re-derivations free. It also keeps the
            // plate AHEAD of the dash it rides: at the shipped 54u / 0.210s the hull covers 16.8u
            // while the plate sweeps 54, where at 0.35s it would cover 28 and the punch would
            // trail behind the ship that threw it.
            if (initStruct.DurationOverride > 0f)
                ExplosionDuration = initStruct.DurationOverride;
            else if (sweepSpeed > 0f && _length > 0f)
                ExplosionDuration = _length / sweepSpeed;

            // The spawn rotation's forward IS the fire direction: every call site builds it with
            // Quaternion.LookRotation(dir, up), the same contract the conic blast's container uses.
            _axis = transform.forward;
            if (_axis.sqrMagnitude < 1e-8f) _axis = Vector3.forward;
            _axis.Normalize();

            // The one velocity this blast has. Linear sweep, so this is the plate's speed on every
            // frame as well as the magnitude it imparts.
            speed = ExplosionDuration > 0f ? _length / ExplosionDuration : 0f;

            MaxScaleVector = new Vector3(MaxScale, MaxScale, _length);

            Material = initStruct.OverrideMaterial;

            // World units on a scale-1 root - see the class docs.
            transform.localScale = Vector3.one;

            _triggerBox = _triggerCollider as BoxCollider;
            _visualComplete = false;
            if (_triggerCollider) _triggerCollider.enabled = true;
            ShapeTriggerBox(0f);
            ShapePlateVisual(0f);

            if (meshRenderer) meshRenderer.enabled = false;

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (!_triggerBox)
                Debug.LogWarning(
                    "[AOECylindricalExplosion] The trigger is not a BoxCollider, so vessel and " +
                    "ball contacts will use the authored collider's own (unswept) shape while the " +
                    "prism sweep uses the plate. Swap the trigger to a box.", this);
            if (!plateVisual)
                Debug.LogWarning(
                    "[AOECylindricalExplosion] No plate visual assigned - the blast will damage " +
                    "correctly and draw nothing.", this);
#endif

            explosionCts = new CancellationTokenSource();
        }

        /// <summary>
        /// The plate throws everything it reaches the way it TRAVELLED, so the impact vector is
        /// the same at every position — no radial term and therefore no falloff-by-geometry. This
        /// is what a vessel caught in the blast feels, and what an Astro League ball is launched
        /// with (<c>ExplosionImpactor.OnTriggerEnter</c> hands this straight to
        /// <c>AstroLeagueBall.RequestBlast</c>), so the ball leaves along the dash exactly like
        /// the prism mass beside it.
        /// </summary>
        public override Vector3 CalculateImpactVector(Vector3 impacteePosition) => Impulse.Along(_axis);

        /// <summary>
        /// The trigger is the CIRCUMSCRIBING box of the swept-so-far cylinder: `2R × 2R × depth`,
        /// centred on the sweep's axial midpoint, with local +Z the sweep axis. Prisms come off the
        /// exact Burst sweep; this shape is only what VESSEL and Astro League BALL contacts resolve
        /// through, and Unity ships no cylinder collider.
        ///
        /// It circumscribes rather than inscribes, deliberately. An inscribed SPHERE was the first
        /// shape here and it is only honest while the plate is at least as long as it is wide: its
        /// radius is `min(depth/2, R)`, so the moment the cylinder is squat (the Scarab's is 45
        /// wide and 54 long) the sphere caps at 27 and silently loses 40% of the plate's reach —
        /// a ball plainly inside the punch the player was shown does nothing. Between the two
        /// errors, over-reach is the survivable one: the box's only excess is the four corners of
        /// the square cross-section (27% of area, none of it further than √2·R out), against an
        /// under-reach that reads as the weapon being broken.
        /// </summary>
        void ShapeTriggerBox(float depth)
        {
            if (!_triggerBox) return;

            // ZERO DEPTH IS THE ZERO STATE, ON EVERY AXIS - not a full-width pane of no thickness.
            // The base spherical blast reaches this posture for free (it zeroes transform.localScale
            // in Initialize, which zeroes its collider with it); this one drives the collider
            // directly on a scale-1 root, so it has to say so. It matters because a blast can be
            // FROZEN here: AOEExplosion's cancellation contract deliberately leaves a cancelled
            // explosion alive at its current size, and the pre-visual early-out runs before the
            // sweep has advanced at all. Shaped as 2R x 2R x epsilon that would park an invisible
            // 90x90 trigger pane in the arena for the rest of the match.
            if (depth <= 0f)
            {
                _triggerBox.size = new Vector3(1e-4f, 1e-4f, 1e-4f);
                _triggerBox.center = Vector3.zero;
                return;
            }

            Vector3 lossy = transform.lossyScale;
            float sx = Mathf.Max(Mathf.Abs(lossy.x), 1e-4f);
            float sy = Mathf.Max(Mathf.Abs(lossy.y), 1e-4f);
            float sz = Mathf.Max(Mathf.Abs(lossy.z), 1e-4f);
            // A box scales componentwise (unlike a sphere or a capsule), so each axis divides out
            // its OWN factor and there is no anisotropy trap to fall into here.
            _triggerBox.size = new Vector3(_radius * 2f / sx, _radius * 2f / sy, depth / sz);
            _triggerBox.center = new Vector3(0f, 0f, depth * 0.5f / sz);
        }

        /// <summary>
        /// Draws the swept-so-far cylinder. The child is rolled +90 degrees about X so the built-in
        /// cylinder's local +Y runs along the root's +Z (the sweep axis); its local scale is
        /// therefore (diameter, halfLength, diameter) and its offset half the current depth.
        /// </summary>
        void ShapePlateVisual(float depth)
        {
            if (!plateVisual) return;
            plateVisual.localRotation = Quaternion.Euler(90f, 0f, 0f);
            plateVisual.localPosition = new Vector3(0f, 0f, depth * 0.5f);
            plateVisual.localScale = new Vector3(_radius * 2f, depth * 0.5f, _radius * 2f);
        }

        protected override async UniTaskVoid ExplodeAsync(CancellationToken ct)
        {
            var impactor = _explosionImpactor;
            bool colliderExcludedForBatch = false;
            try
            {
                impactor?.BeginBatchProcessing();
                if (impactor != null && impactor.IsBatchProcessing)
                {
                    ApplyPrismExclusion();
                    colliderExcludedForBatch = true;
                }
                else
                {
                    RestorePrismExclusion();
                }

                await UniTask.Delay(TimeSpan.FromSeconds(ExplosionDelay), DelayType.DeltaTime,
                                    PlayerLoopTiming.Update, ct);

                if (!this || ct.IsCancellationRequested)
                {
                    // Cancelled before the blast ever rendered or damaged anything. The base
                    // class's "leave a cancelled explosion frozen at its current size" contract
                    // exists so a VISIBLE blast does not pop out of the world; there is nothing
                    // visible here yet, so retire the trigger rather than freeze an inert husk
                    // holding a live vessel/ball hitbox.
                    impactor?.EndBatchProcessing();
                    if (_triggerCollider) _triggerCollider.enabled = false;
                    return;
                }

                _platePropertyBlock ??= new MaterialPropertyBlock();
                if (meshRenderer)
                {
                    // sharedMaterial + MaterialPropertyBlock rather than .material: the override
                    // material is the vessel's shared AOE asset, so cloning it per blast would
                    // allocate, and per-instance opacity is what the block is for (the spherical
                    // blast learned this - concurrent blasts fighting over one _Opacity flickered).
                    if (Material) meshRenderer.sharedMaterial = Material;
                    meshRenderer.enabled = true;
                }

                // Axial depth already swept. Each frame damages the slab between this and the new
                // depth, so successive slabs tile the swept cylinder EXACTLY - no coverage gaps at
                // any frame rate, and never past the visible end cap.
                float sweptTo = 0f;
                float elapsed = 0f;

                while (elapsed < ExplosionDuration)
                {
                    ct.ThrowIfCancellationRequested();
                    if (!this)
                    {
                        impactor?.EndBatchProcessing();
                        if (colliderExcludedForBatch) RestorePrismExclusion();
                        return;
                    }

                    elapsed += Time.deltaTime;
                    // Clamped so the final iteration's overshoot cannot push the plate past its
                    // authored reach - the slabs would then cover volume the player never saw.
                    float t = ExplosionDuration > 0f
                        ? Mathf.Min(elapsed / ExplosionDuration, 1f)
                        : 1f;
                    float depth = _length * t;   // LINEAR - see the class docs

                    ShapePlateVisual(depth);
                    ShapeTriggerBox(depth);

                    bool shouldContinue = impactor?.ProcessBatchCylinderFrame(
                        transform.position, _axis, sweptTo, depth, _radius, Impulse) ?? true;

                    sweptTo = Mathf.Max(sweptTo, depth);

                    if (!shouldContinue)
                    {
                        // Super-shielded enemy prism physically blocks the blast.
                        impactor?.EndBatchProcessing();
                        if (colliderExcludedForBatch) RestorePrismExclusion();
                        if (this) Destroy(gameObject);
                        return;
                    }

                    if (meshRenderer)
                    {
                        meshRenderer.GetPropertyBlock(_platePropertyBlock);
                        _platePropertyBlock.SetFloat(OpacityID, 1f - t);
                        meshRenderer.SetPropertyBlock(_platePropertyBlock);
                    }

                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }

                // HOLD THE TRIGGER AT FULL EXTENT FOR A BEAT. Prisms come off the exact Burst
                // sweep, but the VESSEL and the Astro League ball come off this trigger, and PhysX
                // only generates trigger events inside the physics step. The sweep loop runs in
                // Update, so the last iteration writes the largest shape and then falls straight
                // through to the retirement below - in the SAME frame, before any FixedUpdate ever
                // simulates it. On a long blast the shape one frame back is nearly as big and
                // nothing is lost; on this one (0.210s, ~5 physics steps) it is most of the volume, and a
                // punch that misses the ball it visibly engulfed is the whole mode failing. The
                // held volume is small and invisible for a few tens of milliseconds, which is a
                // cheaper price than a missed contact.
                ShapePlateVisual(_length);
                ShapeTriggerBox(_length);
                if (contactHoldSeconds > 0f)
                {
                    // Floored at TWO fixed steps. The authored 0.05 was sized against Unity's
                    // default 0.02 timestep; this project runs 0.04 (ProjectSettings/TimeManager),
                    // where 0.05 guarantees only ONE step - and one step is the difference between
                    // "the punch reliably catches the ball at its rim" and "usually". Deriving the
                    // floor from fixedDeltaTime means the guarantee survives someone retuning the
                    // timestep, instead of quietly becoming a wall-clock number that no longer
                    // means what it meant when it was chosen.
                    float hold = Mathf.Max(contactHoldSeconds, Time.fixedDeltaTime * 2f);
                    await UniTask.Delay(TimeSpan.FromSeconds(hold), DelayType.DeltaTime,
                                        PlayerLoopTiming.Update, ct);
                }

                // Work the blast owes but could not afford within its per-frame budget still has to
                // land - a prism's fate is decided by whether the blast CONTAINED it, never by how
                // long the VFX happened to run. Retire the blast from the world first, or the
                // trigger parks an invisible vessel hitbox here for the whole drain.
                _visualComplete = true;
                if (meshRenderer) meshRenderer.enabled = false;
                if (_triggerCollider) _triggerCollider.enabled = false;
                while (impactor != null && impactor.HasPendingBatchWork)
                {
                    ct.ThrowIfCancellationRequested();
                    impactor.DrainPendingBatchFrame(Impulse);
                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                }

                impactor?.EndBatchProcessing();
                if (colliderExcludedForBatch) RestorePrismExclusion();
                if (this) Destroy(gameObject);
            }
            catch (OperationCanceledException)
            {
                impactor?.EndBatchProcessing();
                // Prism exclusion deliberately NOT restored - same reasoning as the base class: a
                // cancelled blast stays frozen where it is, and re-including TrailBlocks would make
                // PhysX refilter and fire OnTriggerEnter for every overlapping prism after the turn
                // ended. Once the visual is done there is no frozen plate to preserve, so tear down.
                if (_visualComplete && this) Destroy(gameObject);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                impactor?.EndBatchProcessing();
                if (colliderExcludedForBatch) RestorePrismExclusion();
                if (this) Destroy(gameObject);
            }
        }
    }
}
