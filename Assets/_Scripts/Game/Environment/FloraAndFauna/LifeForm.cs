using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
        [SerializeField]
        protected CellDataSO cellData;

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

        HashSet<HealthPrism> healthBlocks = new HashSet<HealthPrism>();

        [SerializeField] ScriptableEventInt onLifeFormCreated;
        [SerializeField] ScriptableEventInt onLifeFormDestroyed;

        [SerializeField] private bool autoInitialize = true;
        private bool initialized;

        protected virtual void Start()
        {
            if (!autoInitialize || initialized) return;

            // if something else already initialized us, this.cell will be set
            if (cell == null)
                cell = CellControlManager.Instance.GetNearestCell(transform.position);

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

            onLifeFormCreated?.Raise(cell.ID);
        }

        /// <summary>
        /// Binds any Spindles / HealthPrisms already present in the prefab hierarchy.
        /// IMPORTANT: We do NOT override HealthPrism.TargetScale here.
        /// TargetScale should be authored on the prefab HealthPrism itself (or overridden by Flora).
        /// </summary>
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
                hp.Initialize("fauna");
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

            //spindle.OnWitherStarted += HandleSpindleWitherStarted;
        }
        
        private void HandleSpindleWitherStarted(Spindle spindle)
        {
            // Whimper / change behavior / retreat / enraged / etc.
            //  route this through ScriptableEvents too.
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
        }

        public virtual void RemoveHealthBlock(HealthPrism healthPrism)
        {
            if (!healthPrism) return;
            healthBlocks.Remove(healthPrism);
            CheckIfDead();
        }

        public void CheckIfDead()
        {
            if (!dying && (spindles.Count == 0 || (mature && healthBlocks.Count <= minHealthBlocks)))
            {
                dying = true;
                Die();
            }
        }

        void CheckIfMature()
        {
            if (healthBlocks.Count >= healthBlocksForMaturity)
                mature = true;
        }

        protected virtual void Die()
        {
            if (crystal) crystal.ActivateCrystal();

            foreach (HealthPrism healthBlock in healthBlocks.ToArray())
            {
                Debug.LogWarning("Post death health block created!. Should not happen!");
                healthBlock.Damage(Random.onUnitSphere, Domains.None, "Guy Fawkes", true);
            }

            StopAllCoroutines();
            StartCoroutine(DieCoroutine());

            onLifeFormDestroyed?.Raise(cell.ID);
        }

        private IEnumerator DieCoroutine()
        {
            while (spindles.Count > 0)
                yield return null;

            Destroy(gameObject);
        }

        public GameObject GetGameObject() => gameObject;

        public void SetTeam(Domains domain)
        {
            this.domain = domain;

            // propagate to any already-existing health prisms (prefab embedded or spawned)
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
                        if (healthBlocks.Contains(healthBlock))
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
    }
}
