using System;
using System.Collections;
using System.Collections.Generic;
using CosmicShore.App.Systems.Audio;
using CosmicShore.Core;
using CosmicShore.Game;
using CosmicShore.Soap;
using CosmicShore.Utility;
using Obvious.Soap;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

namespace CosmicShore
{
    public abstract class LifeForm : MonoBehaviour, ITeamAssignable
    {
        [SerializeField] protected GameDataSO gameData;
        [SerializeField] protected CellRuntimeDataSO cellData;
        [FormerlySerializedAs("healthBlock")]
        [SerializeField] protected HealthPrism healthPrism;
        [SerializeField] protected Spindle spindle;
        [SerializeField] protected HealthPrismPoolManager healthPrismPool;
        [SerializeField] int healthBlocksForMaturity = 1;
        [SerializeField] int minHealthBlocks = 0;
        [SerializeField] float shieldPeriod = 0;
        [FormerlySerializedAs("Team")]
        public Domains domain;

        protected HashSet<Spindle> spindles = new HashSet<Spindle>();
        protected Crystal crystal;
        protected Cell cell;

        bool mature = false;
        bool dying = false;
        bool isCleaningUp = false;

        HashSet<HealthPrism> healthBlocks = new HashSet<HealthPrism>();

        // Scratch lists to avoid per-frame allocations in Die/HandleTurnEnded
        static readonly List<HealthPrism> s_healthScratch = new List<HealthPrism>(64);
        static readonly List<Spindle> s_spindleScratch = new List<Spindle>(32);

        [SerializeField] ScriptableEventInt onLifeFormCreated;
        [SerializeField] ScriptableEventInt onLifeFormDestroyed;
        public static event Action<string, int> OnLifeFormDeath;
        [SerializeField] private bool autoInitialize = true;
        private bool initialized;

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

            if (shieldPeriod > 0)
                StartCoroutine(ShieldRegen());

            crystal = GetComponentInChildren<Crystal>();
            this.cell = cell;

            BindEmbeddedParts();

            if (cell != null)
                onLifeFormCreated?.Raise(cell.ID);
        }

        private void BindEmbeddedParts()
        {
            var embeddedSpindles = GetComponentsInChildren<Spindle>(true);
            foreach (var sp in embeddedSpindles)
            {
                if (!sp) continue;
                AddSpindle(sp);
            }

            var embeddedHealthPrisms = GetComponentsInChildren<HealthPrism>(true);
            foreach (var hp in embeddedHealthPrisms)
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

        public virtual void AddHealthBlock(HealthPrism healthPrism)
        {
            if (!healthPrism) return;
            healthBlocks.Add(healthPrism);
            healthPrism.ChangeTeam(domain);
            healthPrism.LifeForm = this;
            healthPrism.ownerID = $"{this} + {healthPrism} + {healthBlocks.Count}";
            CheckIfMature();
        }
        
        public void AddSpindle(Spindle spindle)
        {
            if (!spindle) return;
            spindles.Add(spindle);
            spindle.LifeForm = this;
        }

        public Spindle AddSpindle()
        {
            Spindle newSpindle = Instantiate(spindle, transform.position, transform.rotation, transform);
            AddSpindle(newSpindle);
            return newSpindle;
        }

        public virtual void RemoveSpindle(Spindle spindle)
        {
            if (!spindle) return;
            spindles.Remove(spindle);
            CleanupDeadRefs();
            CheckIfDead();
        }

        public virtual void RemoveHealthBlock(HealthPrism healthPrism, string killerName = "")
        {
            if (!healthPrism) return;
    
            healthBlocks.Remove(healthPrism);
            CleanupDeadRefs();
            CheckIfDead(killerName);
        }
        
        static readonly System.Predicate<Spindle> s_deadSpindle = s => !s;
        static readonly System.Predicate<HealthPrism> s_deadHealthPrism = h => !h;

        void CleanupDeadRefs()
        {
            spindles.RemoveWhere(s_deadSpindle);
            healthBlocks.RemoveWhere(s_deadHealthPrism);
        }

        public void CheckIfDead(string killerName = "")
        {
            // [Visual Note] Safety: If we are already destroyed/cleaning up, don't run logic
            if (dying) return;

            CleanupDeadRefs();

            if(healthBlocks.Count <= minHealthBlocks)
            {
                dying = true;
                Die(killerName);
                return;
            }

            if (spindles.Count != 0) return;
            dying = true;
            Die();
        }

        void CheckIfMature()
        {
            if (healthBlocks.Count >= healthBlocksForMaturity)
                mature = true;
        }

        protected virtual void Die(string killerName = "")
        {
            if (isCleaningUp) return;

            AudioSystem.Instance.PlayGameplaySFX(GameplaySFXCategory.CreatureDeath);

            if (crystal && crystal.gameObject.activeInHierarchy && !isCleaningUp)
                crystal.ActivateCrystal();

            int cellId = cell ? cell.ID : -1;

            if (!string.IsNullOrEmpty(killerName))
                OnLifeFormDeath?.Invoke(killerName, cellId);

            s_healthScratch.Clear();
            foreach (var hp in healthBlocks) s_healthScratch.Add(hp);
            for (int i = 0; i < s_healthScratch.Count; i++)
            {
                if (!s_healthScratch[i]) continue;
                s_healthScratch[i].Damage(Random.onUnitSphere, Domains.None, "Guy Fawkes", true);
            }
            s_healthScratch.Clear();

            GetComponentsInChildren(true, s_spindleScratch);
            for (int i = 0; i < s_spindleScratch.Count; i++)
            {
                if (s_spindleScratch[i]) s_spindleScratch[i].ForceWither();
            }
            s_spindleScratch.Clear();
            if (cell)
            {
                cell.UnregisterSpawnedObject(gameObject);
            }
            StopAllCoroutines();
            if (gameObject.activeInHierarchy)
                StartCoroutine(DieCoroutine(cellId));
            else if (!isCleaningUp)
                Destroy(gameObject); // Instant destroy if inactive
        }

        private IEnumerator DieCoroutine(int cellId)
        {
            while (true)
            {
                CleanupDeadRefs();
                if (spindles.Count == 0) break;
                yield return null;
            }
            
            if(!isCleaningUp)
            {
                // [Visual Note] Use the cached ID, don't access cell.ID here as cell might be dead by now
                onLifeFormDestroyed?.Raise(cellId);
                Destroy(gameObject);
            }
        }
        
        public GameObject GetGameObject() => gameObject;

        public void SetTeam(Domains domain)
        {
            this.domain = domain;
            var allHealthPrisms = GetComponentsInChildren<HealthPrism>(true);
            foreach (var hp in allHealthPrisms)
                if (hp) hp.ChangeTeam(domain);
        }

        IEnumerator ShieldRegen()
        {
            var wait = new WaitForSeconds(shieldPeriod);
            var scratch = new List<HealthPrism>(16);
            while (shieldPeriod > 0)
            {
                scratch.Clear();
                foreach (var hp in healthBlocks) scratch.Add(hp);
                if (scratch.Count > 0)
                {
                    for (int i = 0; i < scratch.Count; i++)
                    {
                        if (healthBlocks.Contains(scratch[i]) && scratch[i])
                            scratch[i].ActivateShield();
                        yield return wait;
                    }
                }
                else
                {
                    yield return wait;
                }
            }
        }
        
        protected virtual void HandleTurnEnded()
        {
            isCleaningUp = true;
            StopAllCoroutines();

            // Return pooled health prisms before destroying the hierarchy
            s_healthScratch.Clear();
            foreach (var hp in healthBlocks) s_healthScratch.Add(hp);
            for (int i = 0; i < s_healthScratch.Count; i++)
            {
                if (!s_healthScratch[i]) continue;
                ReturnHealthPrism(s_healthScratch[i]);
            }
            s_healthScratch.Clear();
            healthBlocks.Clear();

            if(gameObject) Destroy(gameObject);
        }
    }
}