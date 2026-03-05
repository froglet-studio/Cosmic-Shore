using System;
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

        bool deregistered;
        bool dying = false;

        [SerializeField] bool permanentWither = true;
        bool isPermanentlyWithered = false;

        bool isPooled;
        bool startedOnce;

        // Per-instance animation state (driven by static tick)
        float animProgress;
        bool isEvaporating;
        bool isCondensing;

        public event Action<Spindle> OnReturnToPool;

        static readonly Predicate<HealthPrism> s_deadHealthPrism = h => !h;
        static readonly Predicate<Spindle> s_deadSpindle = s => !s;

        // ──────────────── Batched animation system ────────────────
        // Replaces per-spindle coroutines with a single Update pass.

        // When more than this many spindles are evaporating, new deaths
        // skip animation and clean up immediately. Players can't track
        // 50+ individual fade-outs anyway.
        const int MaxAnimatedEvaporations = 48;
        const int MaxAnimatedCondensations = 64;

        static readonly List<Spindle> s_evaporating = new List<Spindle>(256);
        static readonly List<Spindle> s_condensing = new List<Spindle>(256);
        static readonly List<Spindle> s_pendingLifeCheck = new List<Spindle>(64);
        static readonly HashSet<Spindle> s_pendingLifeCheckSet = new HashSet<Spindle>();
        static bool s_driverInstalled;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void InstallDriver()
        {
            if (s_driverInstalled) return;
            s_driverInstalled = true;
            var go = new GameObject("[SpindleAnimDriver]");
            go.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<SpindleAnimDriver>();
        }

        /// <summary>
        /// Single-pass update for all evaporating and condensing spindles.
        /// Called once per frame by SpindleAnimDriver.
        /// </summary>
        internal static void TickAllAnimations()
        {
            float dt = Time.deltaTime;

            // Tick evaporating (iterate backwards for safe removal)
            for (int i = s_evaporating.Count - 1; i >= 0; i--)
            {
                var s = s_evaporating[i];
                if (!s || !s.isEvaporating)
                {
                    SwapRemove(s_evaporating, i);
                    continue;
                }

                s.animProgress += dt;
                if (s.animProgress >= 1f)
                {
                    s.FinishEvaporation();
                    SwapRemove(s_evaporating, i);
                }
                else if (s.RenderedObject && s.RenderedObject.isVisible)
                {
                    // Only touch MaterialPropertyBlock for on-screen spindles
                    s.SetDeathAnimation(s.animProgress);
                }
            }

            // Tick condensing
            for (int i = s_condensing.Count - 1; i >= 0; i--)
            {
                var s = s_condensing[i];
                if (!s || !s.isCondensing)
                {
                    SwapRemove(s_condensing, i);
                    continue;
                }

                s.animProgress -= dt;
                if (s.animProgress <= 0f)
                {
                    s.isCondensing = false;
                    s.ClearDeathAnimation();
                    SwapRemove(s_condensing, i);
                }
                else if (s.RenderedObject && s.RenderedObject.isVisible)
                {
                    s.SetDeathAnimation(s.animProgress);
                }
            }
        }

        /// <summary>
        /// Processes deferred CheckForLife calls so the death cascade
        /// doesn't spike a single frame. Called in LateUpdate.
        /// </summary>
        internal static void FlushPendingLifeChecks()
        {
            if (s_pendingLifeCheck.Count == 0) return;

            // Process in a loop — new entries may be added during iteration
            // Cap iterations to prevent infinite cascade
            int iterations = 0;
            while (s_pendingLifeCheck.Count > 0 && iterations < 4)
            {
                int count = s_pendingLifeCheck.Count;
                for (int i = 0; i < count; i++)
                {
                    var s = s_pendingLifeCheck[i];
                    if (s) s.CheckForLifeImmediate();
                }
                s_pendingLifeCheck.RemoveRange(0, count);
                iterations++;
            }
            s_pendingLifeCheck.Clear();
            s_pendingLifeCheckSet.Clear();
        }

        static void SwapRemove<T>(List<T> list, int index)
        {
            int last = list.Count - 1;
            if (index < last) list[index] = list[last];
            list.RemoveAt(last);
        }

        // ──────────────── Lifecycle ────────────────

        void CleanupDeadRefs()
        {
            healthBlocks.RemoveWhere(s_deadHealthPrism);
            spindles.RemoveWhere(s_deadSpindle);
        }

        void OnEnable()
        {
            if (!isPermanentlyWithered)
                deregistered = false;

            if (!isPermanentlyWithered) return;

            if (RenderedObject) RenderedObject.enabled = false;
            CancelAnimations();
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

            startedOnce = true;
            propertyBlock = new MaterialPropertyBlock();

            float randomOffset = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            RenderedObject.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(PhaseOffsetID, randomOffset);
            RenderedObject.SetPropertyBlock(propertyBlock);

            BeginCondense();

            if (LifeForm) LifeForm.AddSpindle(this);
            parentSpindle ??= transform.parent.GetComponentInParent<Spindle>();
            if (parentSpindle) parentSpindle.AddSpindle(this);
        }

        /// <summary>
        /// Called by SpindlePoolManager when this spindle is taken from the pool.
        /// </summary>
        public void InitializeFromPool()
        {
            isPooled = true;

            if (!startedOnce) return;

            if (RenderedObject == null || RenderedObject.sharedMaterial == null) return;

            propertyBlock ??= new MaterialPropertyBlock();

            float randomOffset = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            RenderedObject.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(PhaseOffsetID, randomOffset);
            RenderedObject.SetPropertyBlock(propertyBlock);

            if (RenderedObject) RenderedObject.enabled = true;

            BeginCondense();
        }

        public void ResetForPool()
        {
            CancelAnimations();

            dying = false;
            isPermanentlyWithered = false;
            deregistered = false;

            parentSpindle = null;
            LifeForm = null;

            healthBlocks.Clear();
            spindles.Clear();

            ClearDeathAnimation();
        }

        public void ReturnToPool()
        {
            if (isPooled && SpindlePoolManager.Instance)
            {
                OnReturnToPool?.Invoke(this);
                SpindlePoolManager.Instance.Release(this);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        // ──────────────── Health / Structure ────────────────

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
            DeferCheckForLife();
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
            DeferCheckForLife();
        }

        void DeferCheckForLife()
        {
            if (dying || isPermanentlyWithered) return;
            // Deduplicate — same spindle may be queued multiple times per frame
            if (!s_pendingLifeCheckSet.Add(this)) return;
            s_pendingLifeCheck.Add(this);
        }

        public void CheckForLife()
        {
            DeferCheckForLife();
        }

        void CheckForLifeImmediate()
        {
            if (dying || isPermanentlyWithered) return;

            CleanupDeadRefs();

            if (healthBlocks.Count > 0 || spindles.Count > 0) return;

            dying = true;
            if (permanentWither) isPermanentlyWithered = true;
            BeginEvaporate();
        }

        // ──────────────── Animation ────────────────

        void BeginEvaporate()
        {
            if (!gameObject || !gameObject.activeInHierarchy) return;

            // Cancel any condense in progress
            if (isCondensing) isCondensing = false;

            // If too many spindles are already animating death, skip animation
            // and clean up immediately. The player cannot track 50+ individual fades.
            if (s_evaporating.Count >= MaxAnimatedEvaporations)
            {
                FinishEvaporation();
                return;
            }

            isEvaporating = true;
            animProgress = 0f;
            s_evaporating.Add(this);
        }

        void BeginCondense()
        {
            if (isPermanentlyWithered) return;

            // Cap simultaneous condense animations too
            if (s_condensing.Count >= MaxAnimatedCondensations)
            {
                ClearDeathAnimation();
                return;
            }

            isCondensing = true;
            animProgress = 1f;
            s_condensing.Add(this);
        }

        void CancelAnimations()
        {
            isEvaporating = false;
            isCondensing = false;
            // Lists are cleaned up lazily during TickAllAnimations
        }

        void FinishEvaporation()
        {
            isEvaporating = false;
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

        // ──────────────── Wither ────────────────

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

            BeginEvaporate();
        }

        void DisableSpindle()
        {
            ClearDeathAnimation();

            if (!gameObject.scene.isLoaded) return;

            if (parentSpindle)
            {
                parentSpindle.RemoveSpindle(this);
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

            if (!dying && !isPermanentlyWithered && gameObject.scene.isLoaded) return;

            deregistered = true;
            CancelAnimations();

            bool sceneUnloading = !gameObject.scene.isLoaded;

            if (parentSpindle)
            {
                parentSpindle.spindles.Remove(this);
                if (!sceneUnloading) parentSpindle.DeferCheckForLife();
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
            CancelAnimations();
            DisableSpindle();
        }
    }

    /// <summary>
    /// Lightweight driver that ticks all spindle animations in a single Update pass.
    /// Auto-created via RuntimeInitializeOnLoadMethod, hidden, persistent across scenes.
    /// </summary>
    internal class SpindleAnimDriver : MonoBehaviour
    {
        void Update() => Spindle.TickAllAnimations();
        void LateUpdate() => Spindle.FlushPendingLifeChecks();
    }
}
