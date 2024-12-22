using CosmicShore.Environment.FlowField;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CosmicShore
{
    public abstract class LifeForm : MonoBehaviour, ITeamAssignable
    {
        [SerializeField] protected HealthBlock healthBlock;
        [SerializeField] protected Spindle spindle;

        [SerializeField] int healthBlocksForMaturity = 1; 
        [SerializeField] int minHealthBlocks = 0;
        bool mature = false;
        bool dying = false;
        [SerializeField] float shieldPeriod = 0;

        HashSet<HealthBlock> healthBlocks = new HashSet<HealthBlock>();
        protected HashSet<Spindle> spindles = new HashSet<Spindle>();

        protected Crystal crystal;
        protected Node node;
        public Teams Team;

        protected virtual void Start()
        {
            if (shieldPeriod > 0) StartCoroutine(ShieldRegen());
            crystal = GetComponentInChildren<Crystal>();
            node = NodeControlManager.Instance.GetNodeByPosition(transform.position);
            StatsManager.Instance.LifeformCreated(node.ID);
        }

        public virtual void AddHealthBlock(HealthBlock healthBlock)
        {
            healthBlocks.Add(healthBlock);
            healthBlock.ChangeTeam(Team);
            healthBlock.LifeForm = this;
            healthBlock.ownerId = $"{this} + {healthBlock} + {healthBlocks.Count}";
            CheckIfMature();
        }

        public void AddSpindle(Spindle spindle)
        {
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
            spindles.Remove(spindle);
        }

        public virtual void RemoveHealthBlock(HealthBlock healthBlock)
        { 
            healthBlocks.Remove(healthBlock);
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

        void  CheckIfMature()
        {
            if (healthBlocks.Count >= healthBlocksForMaturity)
                mature = true;
        }

        protected virtual void Die()
        {
            crystal.ActivateCrystal();
            StatsManager.Instance.LifeformDestroyed(node.ID);
            foreach (HealthBlock healthBlock in healthBlocks.ToArray())
            {
                healthBlock.Damage(Random.onUnitSphere, Teams.None, "Guy Fawkes", true);
            }
            StopAllCoroutines();
            StartCoroutine(DieCoroutine());
        }

        private IEnumerator DieCoroutine()
        {
            while (spindles.Count > 0)
            {
                yield return null;
            }
            Destroy(gameObject);
        }

        public GameObject GetGameObject()
        {
            return gameObject;
        }

        public void SetTeam(Teams team)
        {
            Team = team;
        }

        IEnumerator ShieldRegen()
        {
            while (shieldPeriod > 0)
            {
                // Create a snapshot of the current health blocks
                List<HealthBlock> currentBlocks = new List<HealthBlock>(healthBlocks);

                if (currentBlocks.Count > 0)
                {
                    foreach (HealthBlock healthBlock in currentBlocks)
                    {
                        // Check if the block still exists in the main list
                        if (healthBlocks.Contains(healthBlock))
                        {
                            healthBlock.ActivateShield();
                        }
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