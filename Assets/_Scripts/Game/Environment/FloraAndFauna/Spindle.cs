using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Game;
using CosmicShore.Utility;

namespace CosmicShore
{
    public class Spindle : MonoBehaviour
    {
        private static readonly int PhaseOffsetID = Shader.PropertyToID("_Phase");
        private static readonly int DeathAnimationID = Shader.PropertyToID("_DeathAnimation");
        private MaterialPropertyBlock propertyBlock;

        public Renderer RenderedObject;
        [SerializeField] Spindle parentSpindle;
        public LifeForm LifeForm;
        [SerializeField] bool retainSpindle = false;

        HashSet<HealthPrism> healthBlocks = new HashSet<HealthPrism>();
        HashSet<Spindle> spindles = new HashSet<Spindle>();

        Coroutine condenseCoroutine;

        bool deregistered;
        bool dying = false;

        [SerializeField] bool permanentWither = true;
        bool isPermanentlyWithered = false;

        bool isPooled;
        bool startedOnce;

        public event Action<Spindle> OnReturnToPool;

        static readonly System.Predicate<HealthPrism> s_deadHealthPrism = h => !h;
        static readonly System.Predicate<Spindle> s_deadSpindle = s => !s;

        void CleanupDeadRefs()
        {
            healthBlocks.RemoveWhere(s_deadHealthPrism);
            spindles.RemoveWhere(s_deadSpindle);
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

            startedOnce = true;
            propertyBlock = new MaterialPropertyBlock();

            float randomOffset = Random.Range(0f, Mathf.PI * 2f);
            RenderedObject.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(PhaseOffsetID, randomOffset);
            RenderedObject.SetPropertyBlock(propertyBlock);

            condenseCoroutine = StartCoroutine(CondenseCoroutine());

            if (LifeForm) LifeForm.AddSpindle(this);
            parentSpindle ??= transform.parent.GetComponentInParent<Spindle>();
            if (parentSpindle) parentSpindle.AddSpindle(this);
        }

        /// <summary>
        /// Called by SpindlePoolManager when this spindle is taken from the pool.
        /// Re-runs the initialization that Start() would normally do.
        /// </summary>
        public void InitializeFromPool()
        {
            isPooled = true;

            if (!startedOnce) return; // Start() hasn't run yet — let it handle init

            // Re-initialize state for reuse
            if (RenderedObject == null || RenderedObject.sharedMaterial == null) return;

            propertyBlock ??= new MaterialPropertyBlock();

            float randomOffset = Random.Range(0f, Mathf.PI * 2f);
            RenderedObject.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(PhaseOffsetID, randomOffset);
            RenderedObject.SetPropertyBlock(propertyBlock);

            if (RenderedObject) RenderedObject.enabled = true;

            condenseCoroutine = StartCoroutine(CondenseCoroutine());
        }

        /// <summary>
        /// Clears all state so this spindle can be reused from the pool.
        /// Called by SpindlePoolManager.Release().
        /// </summary>
        public void ResetForPool()
        {
            StopAllCoroutines();
            condenseCoroutine = null;

            dying = false;
            isPermanentlyWithered = false;
            deregistered = false;

            parentSpindle = null;
            LifeForm = null;

            healthBlocks.Clear();
            spindles.Clear();

            ClearDeathAnimation();
        }

        /// <summary>
        /// Returns this spindle to the pool instead of destroying it.
        /// Falls back to Destroy if no pool is available.
        /// </summary>
        public void ReturnToPool()
        {
            if (isPooled && SpindlePoolManager.Instance)
            {
                OnReturnToPool?.Invoke(this);
                // Pool manager will call ResetForPool and reparent
                SpindlePoolManager.Instance.Release(this);
            }
            else
            {
                Destroy(gameObject);
            }
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

        void SetDeathAnimation(float value)
        {
            if (!RenderedObject) return;
            propertyBlock ??= new MaterialPropertyBlock();
            RenderedObject.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(DeathAnimationID, value);
            RenderedObject.SetPropertyBlock(propertyBlock);
        }

        void ClearDeathAnimation()
        {
            if (!RenderedObject) return;
            propertyBlock ??= new MaterialPropertyBlock();
            RenderedObject.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(DeathAnimationID, 0f);
            RenderedObject.SetPropertyBlock(propertyBlock);
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

                if (!RenderedObject) yield break;

                SetDeathAnimation(deathAnimation);
                deathAnimation += Time.deltaTime * animationSpeed;
            }

            ClearDeathAnimation();
            if (RenderedObject) RenderedObject.enabled = false;

            DisableSpindle();

            if (retainSpindle)
            {
                gameObject.SetActive(false);
            }
            else
            {
                ReturnToPool();
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
                SetDeathAnimation(deathAnimation);
                deathAnimation -= Time.deltaTime * animationSpeed;
                yield return null;
            }

            ClearDeathAnimation();
        }

        private static readonly List<Spindle> s_forceWitherScratch = new List<Spindle>(64);

        public void ForceWither()
        {
            if (dying || isPermanentlyWithered) return;

            dying = true;
            if (permanentWither) isPermanentlyWithered = true;

            s_forceWitherScratch.Clear();
            foreach (var child in spindles)
            {
                if (child) s_forceWitherScratch.Add(child);
            }
            for (int i = 0; i < s_forceWitherScratch.Count; i++)
            {
                s_forceWitherScratch[i].ForceWither();
            }
            s_forceWitherScratch.Clear();

            EvaporateSpindle();
        }

        void DisableSpindle()
        {
            ClearDeathAnimation();

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
