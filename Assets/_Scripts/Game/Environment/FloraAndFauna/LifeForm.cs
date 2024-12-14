using CosmicShore.Environment.FlowField;
using System.Collections;
using System.Collections.Generic;
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

        HashSet<HealthBlock> healthBlocks = new HashSet<HealthBlock>();
        protected HashSet<Spindle> spindles = new HashSet<Spindle>();

        protected Crystal crystal;
        protected Node node;
        public Teams Team;

        protected virtual void Start()
        {
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
            if (spindles.Count == 0 || (mature && healthBlocks.Count <= minHealthBlocks))
                Die();
        }

        void  CheckIfMature()
        {
            if (healthBlocks.Count >= healthBlocksForMaturity)
                mature = true;
        }

        protected virtual void Die()
        {
            crystal.ActivateCrystal();
            StopAllCoroutines();
            StartCoroutine(DieCoroutine());
        }

        private IEnumerator DieCoroutine()
        {
            yield return new WaitForSeconds(1f);
            StatsManager.Instance.LifeformDestroyed(node.ID);
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
    }
}