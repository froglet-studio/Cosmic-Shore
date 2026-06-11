using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.App.Systems.Audio;
using CosmicShore.Core;
using CosmicShore.Game;
using CosmicShore.Soap;
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
                hp.LifeForm = this;
                hp.ChangeTeam(domain);
                hp.Initialize("FaunaPrefab");
                AddHealthBlock(hp); // [Visual Note] Ensure we explicitly add it to tracking
            }
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
        
        void CleanupDeadRefs()
        {
            spindles.RemoveWhere(s => !s);
            healthBlocks.RemoveWhere(h => !h);
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

            foreach (var healthBlock in healthBlocks.ToArray())
            {
                if (!healthBlock) continue;
                healthBlock.Damage(Random.onUnitSphere, Domains.None, "Guy Fawkes", true);
            }

            var allSpindles = GetComponentsInChildren<Spindle>(true);
            foreach (var sp in allSpindles)
            {
                if (sp) sp.ForceWither();
            }
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
            while (shieldPeriod > 0)
            {
                List<HealthPrism> currentBlocks = new List<HealthPrism>(healthBlocks);
                if (currentBlocks.Count > 0)
                {
                    foreach (HealthPrism healthBlock in currentBlocks)
                    {
                        if (healthBlocks.Contains(healthBlock) && healthBlock)
                            healthBlock.ActivateShield();
                        yield return new WaitForSeconds(shieldPeriod);
                    }
                }
                else
                {
                    yield return new WaitForSeconds(shieldPeriod);
                }
            }
        }
        
        protected virtual void HandleTurnEnded()
        {
            isCleaningUp = true;
            StopAllCoroutines();
            // [Visual Note] Don't trigger death logic, just vanish
            if(gameObject) Destroy(gameObject);
        }
    }
}