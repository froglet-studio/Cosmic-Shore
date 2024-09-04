using CosmicShore.Environment.FlowField;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore
{
    public abstract class LifeForm : MonoBehaviour
    {
        [SerializeField] protected HealthBlock healthBlock;
        [SerializeField] protected Spindle spindle;
        [SerializeField] protected int minHealthBlocks = 0;

        HashSet<HealthBlock> healthBlocks = new HashSet<HealthBlock>();
        protected HashSet<Spindle> spindles = new HashSet<Spindle>();

        protected Crystal crystal;
        protected Node node;
        public Teams Team;

        protected virtual void Start()
        {
            crystal = GetComponentInChildren<Crystal>();
            node = NodeControlManager.Instance.GetNodeByPosition(transform.position);
        }

        public virtual void AddHealthBlock(HealthBlock healthBlock)
        {
            healthBlocks.Add(healthBlock);
            healthBlock.Team = Team;
            healthBlock.LifeForm = this;
            healthBlock.ownerId = $"{this} + {healthBlock} + {healthBlocks.Count}";
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
        }

        public void CheckIfDead()
        {
            if (spindles.Count == 0)
                Die();
        }

        protected virtual void Die()
        {
            crystal.ActivateCrystal();
            StopAllCoroutines();
            StartCoroutine(DieCoroutine());
        }

        private IEnumerator DieCoroutine()
        {
            foreach (Spindle spindle in spindles)
            {
                spindle.EvaporateSpindle();
            }

            yield return new WaitForSeconds(1f);

            Destroy(gameObject);
        }
    }
}

