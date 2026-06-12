using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Core;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using Obvious.Soap;
using Reflex.Attributes;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;
using CosmicShore.Data;
namespace CosmicShore.Gameplay
{
    /// <summary>
    /// Abstract base for all lifeforms with health/spindle infrastructure (primarily Flora).
    /// Delegates health block tracking to <see cref="HealthBlockTracker"/> and
    /// spindle tracking to <see cref="SpindleTracker"/> for SRP compliance.
    /// </summary>
    public abstract class LifeForm : MonoBehaviour, ILifeFormEntity
    {
        [Inject] AudioSystem audioSystem;
        [Header("Data References")]
        [Inject] protected GameDataSO gameData;
        [SerializeField] protected CellRuntimeDataSO cellData;

        [Header("Health & Visuals")]
        [FormerlySerializedAs("healthBlock")]
        [SerializeField] protected HealthPrism healthPrism;
        [SerializeField] protected Spindle spindle;
        [SerializeField] protected HealthPrismPoolManager healthPrismPool;

        [Header("Lifecycle")]
        [SerializeField] int healthBlocksForMaturity = 1;
        [SerializeField] int minHealthBlocks = 0;
        [SerializeField] float shieldPeriod = 0;
        [SerializeField] private bool autoInitialize = true;

        [Header("Team")]
        [FormerlySerializedAs("Team")]
        public Domains domain;

        [Header("Events")]
        [SerializeField] ScriptableEventInt onLifeFormCreated;
        [SerializeField] ScriptableEventInt onLifeFormDestroyed;

        // --- Public contract (ILifeFormEntity) ---
        public Domains Domain => domain;
        public static event Action<string, int> OnLifeFormDeath;

        // --- Composition: extracted trackers (SRP) ---
        protected HealthBlockTracker healthTracker;
        protected SpindleTracker spindleTracker;

        // --- Internal state ---
        protected Crystal crystal;
        protected Cell cell;
        bool dying;
        bool isCleaningUp;
        bool initialized;

        // --- Lifecycle: Enable / Disable ---

        protected virtual void OnEnable()
        {
            if (gameData != null)
                gameData.OnShowGameEndScreen.OnRaised += HandleTurnEnded;
        }

        protected virtual void OnDisable()
        {
            if (gameData != null)
                gameData.OnShowGameEndScreen.OnRaised -= HandleTurnEnded;
        }

        // --- Lifecycle: Start / Initialize ---

        protected virtual void Start()
        {
            if (!autoInitialize || initialized) return;
            if (!cell)
                cell = cellData.Cell;
            Initialize(cell);
        }

        public virtual void Initialize(Cell cell)
        {
            if (initialized) return;
            initialized = true;

            // Auto-wire pool when spawned from prefab (prefab instances can't hold scene references)
            if (!healthPrismPool)
                healthPrismPool = HealthPrismPoolManager.Instance;

            this.cell = cell;
            // Pass cell to the tracker so Add/Remove/CleanupDeadRefs forward to
            // Cell.AddBlock/RemoveBlock and feed Cell.LiveBlockCount.
            healthTracker = new HealthBlockTracker(healthBlocksForMaturity, minHealthBlocks, cell);
            spindleTracker = new SpindleTracker();

            crystal = GetComponentInChildren<Crystal>();

            BindEmbeddedParts();

            if (shieldPeriod > 0)
                StartCoroutine(ShieldRegenCoroutine());

            if (cell != null)
                onLifeFormCreated?.Raise(cell.ID);
        }

        void BindEmbeddedParts()
        {
            foreach (var sp in GetComponentsInChildren<Spindle>(true))
            {
                if (!sp) continue;
                AddSpindle(sp);
            }

            foreach (var hp in GetComponentsInChildren<HealthPrism>(true))
            {
                if (!hp) continue;
                hp.IsEmbedded = true;
                hp.LifeForm = this;
                hp.ChangeTeam(domain);
                hp.Initialize("FaunaPrefab");
                AddHealthBlock(hp);
            }
        }

        protected HealthPrism GetHealthPrism(Vector3 position, Quaternion rotation, Transform parent = null)
        {
            if (!healthPrismPool)
            {
                Debug.LogError(
                    $"[{GetType().Name}] '{gameObject.name}' has no HealthPrismPoolManager assigned. " +
                    "All HealthPrisms must come from a pool. Add a HealthPrismPoolManager to the scene " +
                    "and assign it to the 'healthPrismPool' field on this LifeForm.", this);
                return null;
            }
            return healthPrismPool.Get(position, rotation, parent);
        }

        protected void ReturnHealthPrism(HealthPrism hp)
        {
            if (!hp) return;
            hp.LifeForm = null;
            hp.ReturnToPool();
        }

        // --- Health Block Management (delegates to HealthBlockTracker) ---

        public virtual void AddHealthBlock(HealthPrism healthPrism)
        {
            if (!healthPrism) return;
            healthTracker.Add(healthPrism, this, domain);
        }

        public virtual void RemoveHealthBlock(HealthPrism healthPrism, string killerName = "")
        {
            if (!healthPrism) return;
            healthTracker.Remove(healthPrism, killerName);
            spindleTracker.CleanupDeadRefs();
            CheckIfDead(killerName);
        }

        // --- Spindle Management (delegates to SpindleTracker) ---

        public void AddSpindle(Spindle spindle)
        {
            spindleTracker.Add(spindle, this);
        }

        public Spindle AddSpindle()
        {
            // Prefer the spindle pool — eliminates Instantiate/Destroy churn in growth loops
            Spindle newSpindle = SpindlePoolManager.Instance
                ? SpindlePoolManager.Instance.Get(transform.position, transform.rotation, transform)
                : spindleTracker.Instantiate(spindle, transform);
            spindleTracker.Add(newSpindle, this);
            return newSpindle;
        }

        public virtual void RemoveSpindle(Spindle spindle)
        {
            spindleTracker.Remove(spindle);
            CheckIfDead();
        }

        // --- Death / Lifecycle ---

        public void CheckIfDead(string killerName = "")
        {
            if (dying) return;

            healthTracker.CleanupDeadRefs();
            spindleTracker.CleanupDeadRefs();

            if (healthTracker.IsLethal())
            {
                dying = true;
                Die(killerName);
                return;
            }

            if (spindleTracker.IsEmpty())
            {
                dying = true;
                Die();
            }
        }

        protected virtual void Die(string killerName = "")
        {
            if (isCleaningUp) return;

            audioSystem?.PlayGameplaySFX(GameplaySFXCategory.CreatureDeath, transform.position);

            if (crystal && crystal.gameObject.activeInHierarchy && !isCleaningUp)
                crystal.ActivateCrystal();

            int cellId = cell ? cell.ID : -1;

            if (!string.IsNullOrEmpty(killerName))
                OnLifeFormDeath?.Invoke(killerName, cellId);

            healthTracker.DamageAll(Domains.Blue);
            spindleTracker.ForceWitherAll(gameObject);

            if (cell)
                cell.UnregisterSpawnedObject(gameObject);

            StopAllCoroutines();

            if (gameObject.activeInHierarchy)
                StartCoroutine(DieCoroutine(cellId));
            else if (!isCleaningUp)
                Destroy(gameObject);
        }

        IEnumerator DieCoroutine(int cellId)
        {
            while (true)
            {
                spindleTracker.CleanupDeadRefs();
                if (spindleTracker.IsEmpty()) break;
                yield return null;
            }

            if (!isCleaningUp)
            {
                onLifeFormDestroyed?.Raise(cellId);
                Destroy(gameObject);
            }
        }

        // --- Team Assignment ---

        public GameObject GetGameObject() => gameObject;

        public void SetTeam(Domains domain)
        {
            this.domain = domain;
            healthTracker?.SetTeam(domain);

            var allHealthPrisms = GetComponentsInChildren<HealthPrism>(true);
            foreach (var hp in allHealthPrisms)
                if (hp) hp.ChangeTeam(domain);
        }

        // --- Shield Regeneration ---

        IEnumerator ShieldRegenCoroutine()
        {
            var wait = new WaitForSeconds(shieldPeriod);
            var scratch = new List<HealthPrism>(16);
            while (shieldPeriod > 0)
            {
                scratch.Clear();
                foreach (var hp in healthTracker.All) scratch.Add(hp);
                if (scratch.Count > 0)
                {
                    for (int i = 0; i < scratch.Count; i++)
                    {
                        if (scratch[i]) scratch[i].ActivateShield();
                        yield return wait;
                    }
                }
                else
                {
                    yield return wait;
                }
            }
        }

        // --- Turn End Cleanup ---

        protected virtual void HandleTurnEnded()
        {
            isCleaningUp = true;
            StopAllCoroutines();
            // Return pooled health prisms before destroying the hierarchy.
            // Pool return deactivates each prism, and Prism.OnDisable drains it
            // from the cell's grids exactly once (UnregisterFromCell) — so
            // Cell.LiveBlockCount doesn't drift upward across rounds.
            if (healthTracker != null)
            {
                foreach (var hp in healthTracker.All.ToList())
                {
                    if (!hp) continue;
                    ReturnHealthPrism(hp);
                }
            }

            if (gameObject) Destroy(gameObject);
        }
    }
}
