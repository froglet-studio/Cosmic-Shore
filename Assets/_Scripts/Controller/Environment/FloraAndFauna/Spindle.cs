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
        Material temporaryMaterial;
        Coroutine condenseCoroutine;

        bool deregistered;
        bool dying = false;

        [SerializeField] bool permanentWither = true;
        bool isPermanentlyWithered = false;

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

            if (!PhaseVariants.TryGetValue(baseMat, out var variants))
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

        public void CheckForLife()
        {
            if (dying || isPermanentlyWithered) return;

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
            if (RenderedObject) RenderedObject.sharedMaterial = originalMaterial;

            if (!temporaryMaterial) return;
            Destroy(temporaryMaterial);
            temporaryMaterial = null;
        }

        void UseTemporaryMaterial()
        {
            temporaryMaterial = new Material(originalMaterial);
            if (RenderedObject) RenderedObject.material = temporaryMaterial;
        }

        IEnumerator EvaporateCoroutine()
        {
            if (condenseCoroutine != null)
            {
                StopCoroutine(condenseCoroutine);
                condenseCoroutine = null;
            }

            UseTemporaryMaterial();

            float deathAnimation = 0f;
            float animationSpeed = 1f;
            while (deathAnimation < 1f)
            {
                yield return null;

                if (!temporaryMaterial) yield break;

                temporaryMaterial.SetFloat("_DeathAnimation", deathAnimation);
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

            UseTemporaryMaterial();

            float deathAnimation = 1f;
            float animationSpeed = 1f;
            while (deathAnimation > 0f)
            {
                if (isPermanentlyWithered) yield break;
                temporaryMaterial.SetFloat("_DeathAnimation", deathAnimation);
                deathAnimation -= Time.deltaTime * animationSpeed;
                yield return null;
            }

            temporaryMaterial.SetFloat("_DeathAnimation", 0);
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

            // During scene unload, only remove references — don't trigger the death
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