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
    /// GEOMETRY LIVES IN WORLD UNITS ON A SCALE-1 ROOT. The cone drives a non-uniformly scaled
    /// container and then has to divide that scale back out of its capsule trigger, anisotropically
    /// (see <c>AOEConicExplosion.UpdateCapsuleTrigger</c>). This root is never scaled: the mesh
    /// child carries the cylinder's shape and the trigger sphere is written in world units, so
    /// there is no lossy scale to undo and the two cannot drift apart.
    /// </summary>
    public class AOECylindricalExplosion : AOEExplosion
    {
        [Header("Plate")]
        [Tooltip("How long the sweep is, as a multiple of the plate's RADIUS. Shipped at 2 — the " +
                 "cylinder is exactly as long as it is wide, so its axial span equals its " +
                 "diameter. An explicit HeightOverride on the InitializeStruct wins over this.")]
        [SerializeField, Min(0.05f)] float lengthPerRadius = 2f;

        [Tooltip("Sweep SPEED in world units/second. The plate's LENGTH is derived from the " +
                 "emitting hull, so holding the SPEED fixed (and letting the duration follow) is " +
                 "what keeps a resized punch feeling identical - it is the wavefront speed every " +
                 "impulse downstream is built from. 0 falls back to the authored " +
                 "ExplosionDuration. An explicit DurationOverride still wins over both.")]
        [SerializeField, Min(0f)] float sweepSpeed = 257.14f;

        [Tooltip("Seconds to hold the trigger at FULL extent after the sweep finishes, before " +
                 "retiring it. Trigger events are generated in the physics step, and the sweep " +
                 "loop runs in Update - so the final, largest shape would otherwise be written " +
                 "and disabled inside one frame, never simulated. At the shipped ~0.07s sweep " +
                 "that is a large fraction of the blast. 0 disables the hold.")]
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

        SphereCollider _triggerSphere;
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
            // were held fixed instead. Sizing the Scarab's plate off its collider shortened the
            // reach 5x; at a fixed 0.35s that would have dropped the play-tested 257 u/s wavefront
            // to 51 u/s and taken the ball kick from 86% of its top speed back to 17% - undoing
            // SCARAB.md's "a punch should read as a punch, not a bloom" pass without touching a
            // number anyone would think to look at. It also keeps the plate AHEAD of the dash it
            // rides (the hull covers 5.6u while the plate sweeps 18; at 0.35s it would cover 28
            // and the punch would trail behind the ship that threw it).
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

            _triggerSphere = _triggerCollider as SphereCollider;
            _visualComplete = false;
            if (_triggerCollider) _triggerCollider.enabled = true;
            ShapeTriggerSphere(0f);
            ShapePlateVisual(0f);

            if (meshRenderer) meshRenderer.enabled = false;

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (!_triggerSphere)
                Debug.LogWarning(
                    "[AOECylindricalExplosion] The trigger is not a SphereCollider, so vessel and " +
                    "ball contacts will use the authored collider's own (unswept) shape while the " +
                    "prism sweep uses the plate. Swap the trigger to a sphere.", this);
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
        /// The trigger is the INSCRIBED sphere of the swept-so-far cylinder: centred on the sweep's
        /// axial midpoint, radius min(depth/2, plate radius). Unity ships no cylinder collider, and
        /// under-reaching at the rim is the safe direction — the prism sweep is exact either way,
        /// and a circumscribing sphere would shove vessels and the ball from outside the volume the
        /// player is shown. At the shipped length (2× radius) the final sphere touches both end
        /// caps and the side wall.
        /// </summary>
        void ShapeTriggerSphere(float depth)
        {
            if (!_triggerSphere) return;
            float lossy = Mathf.Max(Mathf.Abs(transform.lossyScale.x),
                          Mathf.Max(Mathf.Abs(transform.lossyScale.y),
                                    Mathf.Abs(transform.lossyScale.z)));
            lossy = Mathf.Max(lossy, 1e-4f);
            float half = depth * 0.5f;
            _triggerSphere.radius = Mathf.Min(half, _radius) / lossy;
            _triggerSphere.center = new Vector3(0f, 0f, half / lossy);
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
                    impactor?.EndBatchProcessing();
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
                    ShapeTriggerSphere(depth);

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
                // nothing is lost; on this one (~0.07s, ~4 frames) it is most of the volume, and a
                // punch that misses the ball it visibly engulfed is the whole mode failing. The
                // held volume is small and invisible for a few tens of milliseconds, which is a
                // cheaper price than a missed contact.
                ShapePlateVisual(_length);
                ShapeTriggerSphere(_length);
                if (contactHoldSeconds > 0f)
                    await UniTask.Delay(TimeSpan.FromSeconds(contactHoldSeconds), DelayType.DeltaTime,
                                        PlayerLoopTiming.Update, ct);

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
