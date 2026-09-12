using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using CosmicShore.Gameplay;
using CosmicShore.Utility;

namespace CosmicShore.Gameplay
{
    public class Spindle : MonoBehaviour
    {
        private static readonly int PhaseOffsetID = Shader.PropertyToID("_Phase");
        private static readonly int DeathStartTimeID = Shader.PropertyToID("_DeathStartTime");
        private static readonly int DeathDurationID = Shader.PropertyToID("_DeathDuration");
        private static readonly int DeathDirectionID = Shader.PropertyToID("_DeathDirection");

        // Condense/evaporate fades stamp _DeathStartTime / _DeathDuration /
        // _DeathDirection ONCE through a shared scratch MaterialPropertyBlock.
        // The GPU runs the course off _PrismClock (PrismDeathClock in SpindleGraph
        // / AnimatedSpindleGraph). Trade-off, stated honestly: a renderer WITH an
        // MPB is still excluded from the SRP Batcher for the fade's ~1s — this
        // migration does NOT recover batching DURING the fade (unique staggered
        // StartTimes cannot share quantized fade materials without collapsing the
        // ecology-LOCKED wither order). What it recovers is (1) zero per-frame
        // CPU and (2) SRP Batcher AFTER settle via SetPropertyBlock(null). If a
        // capture shows the unbatched fade window regressing, bucket the fade
        // into a few shared quantized-fade materials like the phase variants —
        // that would be a look trade against ordered wither.
        static MaterialPropertyBlock s_fadeMpb;

        const float DeathFadeDuration = 1f;
        const float DeathDirectionEvaporate = 1f;
        const float DeathDirectionCondense = -1f;

        // Spindle sway is desynced with a small set of SHARED phase-variant materials chosen
        // by world position, NOT a per-renderer MaterialPropertyBlock. A per-renderer MPB
        // excludes the renderer from the SRP Batcher — that is why hundreds of spindles
        // (tadpole bodies, gyroid branches) each drew as their own draw call. Every spindle
        // that lands in the same phase bucket shares one material and batches into a single
        // draw. Purely cosmetic (animation phase only); cached per base material so the count
        // is bounded by the handful of distinct spindle materials.
        const int PhaseVariantCount = 8;
        static readonly Dictionary<Material, Material[]> PhaseVariants = new();

        public Renderer RenderedObject;

        // A spindle may carry MORE THAN ONE piece of branch geometry, and every piece has to
        // live and die on the same clock as the first: the gyroid's branch is a MIRRORED PAIR
        // of half-branches meeting at the prism (Docs/ECOSYSTEM.md §34.12), so a fade driven
        // through RenderedObject alone would condense one half in and evaporate one half out
        // while the other POPPED - a continuity-of-existence violation, on a spindle whose
        // whole point is symmetry. Listed explicitly rather than swept with
        // GetComponentsInChildren because the flora parents its HEALTH PRISM under the spindle
        // root, so a sweep would capture the prism's renderer and fade the mass along with the
        // branch. Empty on every single-renderer spindle; those behave exactly as before.
        // NOTE: attribute and declaration stay on ONE line - the repo's serialized-field parity
        // check is line-based, so a wrapped attribute hides the field from it (a silent false pass).
        [SerializeField, Tooltip("Extra branch renderers beyond RenderedObject. They share its sway phase bucket and take the same condense/evaporate fade, so a multi-part spindle can never half-pop. Leave empty for a single-part spindle.")] Renderer[] additionalRenderedObjects;

        [SerializeField] Spindle parentSpindle;
        public LifeForm LifeForm;
        [SerializeField] bool retainSpindle = false;

        HashSet<HealthPrism> healthBlocks = new HashSet<HealthPrism>();
        HashSet<Spindle> spindles = new HashSet<Spindle>();

        // RenderedObject + additionalRenderedObjects, flattened once. Parallel arrays, one
        // slot per renderer: its captured base material and the shared phase variant it draws
        // with. Every slot resolves its variant from the SPINDLE ROOT's position, so the parts
        // of one spindle always land in the same phase bucket and sway together - bucketing
        // per-renderer would desync a mirrored pair and tear it apart at the joint.
        Renderer[] _renderers;
        Material[] _phaseBaseMaterials;
        Material[] _phaseVariants;

        bool deregistered;
        bool dying = false;

        [SerializeField] bool permanentWither = true;
        bool isPermanentlyWithered = false;

        // ── Ordered death wither ─────────────────────────────────────────────
        // A dying lifeform spends its spindles ONE AT A TIME, in an order the death
        // itself dictates (Docs/ECOSYSTEM.md §26): outside-in for starvation, from the
        // heart outward for a joust. Two couplings in the ordinary spindle lifecycle
        // fight that, and both are structural rather than cosmetic:
        //   • ForceWither RECURSES into child spindles, so withering an inner spindle
        //     first would collapse the whole creature in a single step.
        //   • Destroying a spindle GameObject destroys its child spindles with it.
        // Isolation breaks both up front - and suspends CheckForLife, so handing this
        // spindle's prisms to the skeleton cannot wither it out of turn.
        bool isolatedForOrderedWither;

        void CleanupDeadRefs()
        {
            healthBlocks.RemoveWhere(h => !h);
            spindles.RemoveWhere(s => !s);
        }

        void Awake() => CacheRenderers();

        /// <summary>
        /// Flattens <see cref="RenderedObject"/> + <see cref="additionalRenderedObjects"/> into
        /// the array every visual path drives. Idempotent and called defensively from each entry
        /// point as well as from Awake, so no Awake/OnEnable ordering assumption is load-bearing.
        /// </summary>
        void CacheRenderers()
        {
            if (_renderers != null) return;

            int extra = 0;
            if (additionalRenderedObjects != null)
                for (int i = 0; i < additionalRenderedObjects.Length; i++)
                    if (additionalRenderedObjects[i] && additionalRenderedObjects[i] != RenderedObject) extra++;

            _renderers = new Renderer[(RenderedObject ? 1 : 0) + extra];
            int n = 0;
            if (RenderedObject) _renderers[n++] = RenderedObject;
            if (additionalRenderedObjects != null)
                for (int i = 0; i < additionalRenderedObjects.Length && n < _renderers.Length; i++)
                {
                    var extraRenderer = additionalRenderedObjects[i];
                    if (extraRenderer && extraRenderer != RenderedObject) _renderers[n++] = extraRenderer;
                }

            _phaseBaseMaterials = new Material[_renderers.Length];
            _phaseVariants = new Material[_renderers.Length];
        }

        void SetRenderersEnabled(bool value)
        {
            CacheRenderers();
            for (int i = 0; i < _renderers.Length; i++)
                if (_renderers[i]) _renderers[i].enabled = value;
        }

        void OnEnable()
        {
            // pooled spindles must be allowed to deregister again later
            if (!isPermanentlyWithered)
                deregistered = false;

            if (!isPermanentlyWithered) return;

            SetRenderersEnabled(false);
            CancelFadeSettle();
        }

        void Start()
        {
            if (isPermanentlyWithered)
                return;

            if (RenderedObject == null || RenderedObject.sharedMaterial == null)
            {
                CSDebug.LogError($"{gameObject.name}: RenderedObject does not have a valid material at Start.");
                return;
            }

            // Desync the sway via a shared phase-variant material (see PhaseVariants) so the
            // spindle stays SRP-batchable — no per-renderer MaterialPropertyBlock. Capture each
            // base material once so pooled reuse never layers variants-on-variants, and bucket
            // EVERY part off the spindle root's position so a multi-part spindle sways as one.
            CacheRenderers();
            for (int i = 0; i < _renderers.Length; i++)
            {
                var partRenderer = _renderers[i];
                if (!partRenderer) continue;
                if (_phaseBaseMaterials[i] == null) _phaseBaseMaterials[i] = partRenderer.sharedMaterial;
                _phaseVariants[i] = GetPhaseVariant(_phaseBaseMaterials[i], transform.position);
                if (_phaseVariants[i]) partRenderer.sharedMaterial = _phaseVariants[i];
            }
            if (!dying)
                StampCondense();

            if (LifeForm) LifeForm.AddSpindle(this);
            parentSpindle ??= transform.parent.GetComponentInParent<Spindle>();
            if (parentSpindle) parentSpindle.AddSpindle(this);
        }

        // A shared material identical to baseMat but with a fixed _Phase, bucketed by world
        // position: same bucket -> same material -> one SRP batch. Created lazily and cached
        // per base material, so the total is bounded by the distinct spindle materials in play.
        static Material GetPhaseVariant(Material baseMat, Vector3 worldPos)
        {
            if (baseMat == null) return null;

            // variants[0] == null catches destroyed (fake-null) materials: with Enter Play
            // Mode Options' domain reload disabled, the static dictionary survives play-mode
            // exit while its runtime-created materials are destroyed — rebuild in that case.
            if (!PhaseVariants.TryGetValue(baseMat, out var variants) || variants[0] == null)
            {
                variants = new Material[PhaseVariantCount];
                for (int i = 0; i < PhaseVariantCount; i++)
                {
                    variants[i] = new Material(baseMat) { name = $"{baseMat.name}_Phase{i}" };
                    variants[i].SetFloat(PhaseOffsetID, i / (float)PhaseVariantCount * Mathf.PI * 2f);
                }
                PhaseVariants[baseMat] = variants;
            }

            // Cheap position hash -> stable per-spindle bucket that scatters neighbours.
            float h = Mathf.Sin(worldPos.x * 12.9898f + worldPos.y * 78.233f + worldPos.z * 37.719f) * 43758.5453f;
            int idx = (int)((h - Mathf.Floor(h)) * PhaseVariantCount);
            return variants[Mathf.Clamp(idx, 0, PhaseVariantCount - 1)];
        }

        public void AddHealthBlock(HealthPrism healthPrism)
        {
            if (isPermanentlyWithered) return;
            if (!healthPrism) return;

            healthBlocks.Add(healthPrism);
            healthPrism.LifeForm = LifeForm;
        }

        public void RemoveHealthBlock(HealthPrism healthPrism)
        {
            if (!healthPrism) return;
            healthBlocks.Remove(healthPrism);
            CheckForLife();
        }

        public void AddSpindle(Spindle spindle)
        {
            if (isPermanentlyWithered) return;
            if (!spindle) return;

            spindles.Add(spindle);
            spindle.parentSpindle = this;
        }

        public void RemoveSpindle(Spindle spindle)
        {
            if (!spindle) return;
            spindles.Remove(spindle);
            CheckForLife();
        }

        /// <summary>
        /// Sets this spindle aside for an ORDERED death wither (see the isolation notes):
        /// detaches it from its parent and children - logically AND in the hierarchy, so it
        /// can be destroyed without taking anything else with it - and suspends
        /// <see cref="CheckForLife"/> so losing its prisms to the skeleton doesn't evaporate
        /// it before its turn. The caller then walks the isolated spindles in whatever order
        /// the death dictates, calling <see cref="ForceWither(float)"/> on each with a
        /// start-time offset. Idempotent; a spindle already dying or withered is left alone.
        /// </summary>
        public void IsolateForOrderedWither(Transform detachedParent)
        {
            if (dying || isPermanentlyWithered || isolatedForOrderedWither) return;
            isolatedForOrderedWither = true;

            if (parentSpindle)
            {
                parentSpindle.spindles.Remove(this);
                parentSpindle = null;
            }

            foreach (var child in spindles)
                if (child) child.parentSpindle = null;
            spindles.Clear();

            if (transform.parent != detachedParent)
                transform.SetParent(detachedParent, true);
        }

        public void CheckForLife()
        {
            if (dying || isPermanentlyWithered || isolatedForOrderedWither) return;

            CleanupDeadRefs();

            if (healthBlocks.Count > 0 || spindles.Count > 0) return;

            dying = true;
            if (permanentWither) isPermanentlyWithered = true;
            EvaporateSpindle();
        }

        void EvaporateSpindle() => StampEvaporate(0f);

        void RestoreOriginalMaterial()
        {
            // Clearing the property block is what restores SRP batching; the shared
            // phase-variant material itself was never swapped out. The null guard on the
            // variant matters on the error path: Start bails before assigning one when the
            // material is invalid, and writing that null back would blank the renderer.
            CacheRenderers();
            for (int i = 0; i < _renderers.Length; i++)
            {
                var partRenderer = _renderers[i];
                if (!partRenderer) continue;
                if (_phaseVariants[i]) partRenderer.sharedMaterial = _phaseVariants[i];
                partRenderer.SetPropertyBlock(null);
            }
        }

        void CancelFadeSettle() => PrismTimerManager.Instance?.CancelScheduledActions(this);

        void StampCondense()
        {
            StampDeathFade(PrismClock.Now, DeathFadeDuration, DeathDirectionCondense, OnCondenseSettled);
        }

        void StampEvaporate(float delay)
        {
            StampDeathFade(PrismClock.Now + delay, DeathFadeDuration, DeathDirectionEvaporate, OnEvaporateSettled);
        }

        void StampDeathFade(float startTime, float duration, float direction, Action onSettle)
        {
            CancelFadeSettle();
            CacheRenderers();
            s_fadeMpb ??= new MaterialPropertyBlock();
            s_fadeMpb.Clear();
            s_fadeMpb.SetFloat(DeathStartTimeID, startTime);
            s_fadeMpb.SetFloat(DeathDurationID, duration);
            s_fadeMpb.SetFloat(DeathDirectionID, direction);
            for (int i = 0; i < _renderers.Length; i++)
            {
                var partRenderer = _renderers[i];
                if (!partRenderer) continue;
                var mat = partRenderer.sharedMaterial;
                if (mat && !mat.HasProperty(DeathStartTimeID))
                    PrismClockDiagnostics.WarnUnwiredMaterial(mat, "_DeathStartTime", this);
                partRenderer.SetPropertyBlock(s_fadeMpb);
            }

            float delay = Mathf.Max(0f, startTime + duration - PrismClock.Now);
            PrismTimerManager.EnsureInstance().ScheduleAction(this, delay, onSettle);
        }

        void OnCondenseSettled()
        {
            RestoreOriginalMaterial();
        }

        void OnEvaporateSettled()
        {
            // Must run even if the renderer is gone: bailing here leaves a dying=true
            // spindle registered forever and stalls LifeForm.DieCoroutine's empty-tracker wait.
            RestoreOriginalMaterial();
            SetRenderersEnabled(false);
            DisableSpindle();

            if (retainSpindle)
                gameObject.SetActive(false);
            else
                Destroy(gameObject);
        }

        /// <param name="evaporateDelay">
        /// Seconds until this spindle's fade STARTS. Ordered wither stamps every
        /// spindle in one pass with <c>i * interval</c> so starvation stays
        /// extremity-first and a joust stays heart-outward — never a per-frame cascade.
        /// </param>
        public void ForceWither(float evaporateDelay = 0f)
        {
            if (dying || isPermanentlyWithered) return;

            dying = true;
            if (permanentWither) isPermanentlyWithered = true;

            foreach (var child in spindles.ToArray())
            {
                if (child) child.ForceWither(evaporateDelay);
            }

            StampEvaporate(evaporateDelay);
        }

        void DisableSpindle()
        {
            RestoreOriginalMaterial();

            if (!gameObject.scene.isLoaded) return;

            if (parentSpindle)
            {
                parentSpindle.RemoveSpindle(this);
                parentSpindle.CheckForLife();
            }

            if (LifeForm)
            {
                LifeForm.RemoveSpindle(this);
                LifeForm.CheckIfDead();
            }
        }

        void OnDisable()
        {
            if (deregistered) return;

            // only deregister if we are truly gone (dying/perma-wither) or being unloaded
            if (!dying && !isPermanentlyWithered && gameObject.scene.isLoaded) return;

            deregistered = true;

            // During scene unload, only remove references - don't trigger the death
            // cascade (CheckForLife/CheckIfDead) which explodes prisms, accesses
            // disposed NativeArrays, and spawns new GameObjects during teardown.
            bool sceneUnloading = !gameObject.scene.isLoaded;

            if (parentSpindle)
            {
                parentSpindle.RemoveSpindle(this);
                if (!sceneUnloading) parentSpindle.CheckForLife();
            }

            if (LifeForm)
            {
                LifeForm.RemoveSpindle(this);
                if (!sceneUnloading) LifeForm.CheckIfDead();
            }
        }

        void OnDestroy()
        {
            CancelFadeSettle();
            if (deregistered) return;
            deregistered = true;
            DisableSpindle();
        }
    }
}