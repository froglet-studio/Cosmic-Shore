using System;
using System.Collections;
using System.Linq;
using CosmicShore.Core;
using CosmicShore.Game;
using CosmicShore.Soap;
using Obvious.Soap;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;
using CosmicShore.Models.Enums;

namespace CosmicShore
{
    /// <summary>
    /// Abstract base for all lifeforms with health/spindle infrastructure (primarily Flora).
    /// Delegates health block tracking to <see cref="HealthBlockTracker"/> and
    /// spindle tracking to <see cref="SpindleTracker"/> for SRP compliance.
    /// </summary>
    public abstract class LifeForm : MonoBehaviour, ILifeFormEntity
    {
        [Header("Data References")]
        [SerializeField] protected GameDataSO gameData;
        [SerializeField] protected CellRuntimeDataSO cellData;

        [Header("Health & Visuals")]
        [FormerlySerializedAs("healthBlock")]
        [SerializeField] protected HealthPrism healthPrism;
        [SerializeField] protected Spindle spindle;

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

            healthTracker = new HealthBlockTracker(healthBlocksForMaturity, minHealthBlocks);
            spindleTracker = new SpindleTracker();

            crystal = GetComponentInChildren<Crystal>();
            this.cell = cell;

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
                hp.LifeForm = this;
                hp.ChangeTeam(domain);
                hp.Initialize("FaunaPrefab");
                AddHealthBlock(hp);
            }
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
            Spindle newSpindle = spindleTracker.Instantiate(spindle, transform);
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

            if (crystal && crystal.gameObject.activeInHierarchy)
                crystal.ActivateCrystal();

            int cellId = cell ? cell.ID : -1;

            if (!string.IsNullOrEmpty(killerName))
                OnLifeFormDeath?.Invoke(killerName, cellId);

            healthTracker.DamageAll(Domains.None);
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
            while (shieldPeriod > 0)
            {
                var blocks = healthTracker.All.ToList();
                if (blocks.Count > 0)
                {
                    foreach (var block in blocks)
                    {
                        if (block) block.ActivateShield();
                        yield return new WaitForSeconds(shieldPeriod);
                    }
                }
                else
                {
                    yield return new WaitForSeconds(shieldPeriod);
                }
            }
        }

        // --- Turn End Cleanup ---

        protected virtual void HandleTurnEnded()
        {
            isCleaningUp = true;
            StopAllCoroutines();
            if (gameObject) Destroy(gameObject);
        }
    }
}
