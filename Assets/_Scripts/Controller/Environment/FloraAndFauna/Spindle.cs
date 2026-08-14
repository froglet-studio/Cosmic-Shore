using System.Collections;
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
        private static readonly int DeathAnimationID = Shader.PropertyToID("_DeathAnimation");

        // Condense/evaporate fades write _DeathAnimation through a shared scratch
        // MaterialPropertyBlock (SetPropertyBlock copies it into the renderer, so one
        // static scratch serves every spindle). The old path cloned a Material per
        // animation (new Material + renderer.material — the banned clone pattern) and
        // Destroyed it after; with dozens of spindles condensing at once that was
        // constant material create/destroy churn. Trade-off, stated honestly: a clone
        // kept the renderer SRP-Batcher-compatible (same shader), while an MPB
        // excludes it for the fade's ~1s duration (see the header comment above) —
        // the win is zero material churn, at the cost of unbatched draws during
        // fades. If a capture shows the unbatched fade draws regressing, bucket the
        // fade into a few shared quantized-fade materials like the phase variants.
        // Clearing the block on completion (SetPropertyBlock(null)) returns the
        // spindle to its shared phase-variant material and SRP batching.
        static MaterialPropertyBlock s_fadeMpb;

        // Spindle sway is desynced with a small set of SHARED phase-variant materials chosen
        // by world position, NOT a per-renderer MaterialPropertyBlock. A per-renderer MPB
        // excludes the renderer from the SRP Batcher — that is why hundreds of spindles
        // (tadpole bodies, gyroid branches) each drew as their own draw call. Every spindle
        // that lands in the same phase bucket shares one material and batches into a single
        // draw. Purely cosmetic (animation phase only); cached per base material so the count
        // is bounded by the handful of distinct spindle materials.
        const int PhaseVariantCount = 8;
        static readonly Dictionary<Material, Material[]> PhaseVariants = new();
        Material _phaseBaseMaterial;   // captured once so pooled reuse never layers variants-on-variants

        public Renderer RenderedObject;
        [SerializeField] Spindle parentSpindle;
        public LifeForm LifeForm;
        [SerializeField] bool retainSpindle = false;

        HashSet<HealthPrism> healthBlocks = new HashSet<HealthPrism>();
        HashSet<Spindle> spindles = new HashSet<Spindle>();

        Material originalMaterial;
        Coroutine condenseCoroutine;

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

        void OnEnable()
        {
            // pooled spindles must be allowed to deregister again later
            if (!isPermanentlyWithered)
                deregistered = false;

            if (!isPermanentlyWithered) return;

            if (RenderedObject) RenderedObject.enabled = false;
            StopAllCoroutines();
        }

        IEnumerator Start()
        {
            if (isPermanentlyWithered)
                yield break;

            if (RenderedObject == null || RenderedObject.sharedMaterial == null)
            {
                CSDebug.LogError($"{gameObject.name}: RenderedObject does not have a valid material at Start.");
                yield break;
            }

            // Desync the sway via a shared phase-variant material (see PhaseVariants) so the
            // spindle stays SRP-batchable — no per-renderer MaterialPropertyBlock. Capture the
            // base material once so pooled reuse never layers variants-on-variants.
            if (_phaseBaseMaterial == null) _phaseBaseMaterial = RenderedObject.sharedMaterial;
            originalMaterial = GetPhaseVariant(_phaseBaseMaterial, transform.position);
            RenderedObject.sharedMaterial = originalMaterial;
            condenseCoroutine = StartCoroutine(CondenseCoroutine());

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
        /// the death dictates, calling <see cref="ForceWither"/> on each. Idempotent; a
        /// spindle already dying or withered is left alone.
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

        private void EvaporateSpindle()
        {
            if (gameObject && gameObject.activeInHierarchy)
                StartCoroutine(EvaporateCoroutine());
        }

        void RestoreOriginalMaterial()
        {
            // Clearing the property block is what restores SRP batching; the shared
            // phase-variant material itself was never swapped out.
            if (RenderedObject)
            {
                RenderedObject.sharedMaterial = originalMaterial;
                RenderedObject.SetPropertyBlock(null);
            }
        }

        void SetFadeValue(float deathAnimation)
        {
            if (!RenderedObject) return;
            s_fadeMpb ??= new MaterialPropertyBlock();
            s_fadeMpb.SetFloat(DeathAnimationID, deathAnimation);
            RenderedObject.SetPropertyBlock(s_fadeMpb);
        }

        IEnumerator EvaporateCoroutine()
        {
            if (condenseCoroutine != null)
            {
                StopCoroutine(condenseCoroutine);
                condenseCoroutine = null;
            }

            float deathAnimation = 0f;
            float animationSpeed = 1f;
            while (deathAnimation < 1f)
            {
                yield return null;

                // No early-out when the renderer is gone: SetFadeValue and
                // RestoreOriginalMaterial null-guard, and the loop MUST run to
                // completion so DisableSpindle/Destroy below always finalize the
                // lifecycle — bailing here leaves a dying=true spindle registered
                // forever and stalls LifeForm.DieCoroutine's empty-tracker wait.
                SetFadeValue(deathAnimation);
                deathAnimation += Time.deltaTime * animationSpeed;
            }

            RestoreOriginalMaterial();
            if (RenderedObject) RenderedObject.enabled = false;

            DisableSpindle();

            if (retainSpindle)
            {
                gameObject.SetActive(false);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        IEnumerator CondenseCoroutine()
        {
            if (isPermanentlyWithered) yield break;

            float deathAnimation = 1f;
            float animationSpeed = 1f;
            while (deathAnimation > 0f)
            {
                if (isPermanentlyWithered) yield break;
                SetFadeValue(deathAnimation);
                deathAnimation -= Time.deltaTime * animationSpeed;
                yield return null;
            }

            RestoreOriginalMaterial();
        }

        public void ForceWither()
        {
            if (dying || isPermanentlyWithered) return;

            dying = true;
            if (permanentWither) isPermanentlyWithered = true;

            foreach (var child in spindles.ToArray())
            {
                if (child) child.ForceWither();
            }

            EvaporateSpindle();
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
            if (deregistered) return;
            deregistered = true;
            DisableSpindle();
        }
    }
}