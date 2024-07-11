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

        protected HashSet<HealthBlock> healthBlocks = new HashSet<HealthBlock>();
        protected HashSet<Spindle> spindles = new HashSet<Spindle>();

        protected Crystal crystal;
        [SerializeField] protected Material activeCrystalMaterial; // TODO: make a crytal material set that pulls from the element
        
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
            spindles.Add(newSpindle);
            newSpindle.LifeForm = this;
            return newSpindle;
        }

        public void RemoveSpindle(Spindle spindle)
        {
            spindles.Remove(spindle);
        }

        public virtual void RemoveHealthBlock(HealthBlock healthBlock)
        { 
            healthBlocks.Remove(healthBlock);
            //CheckIfDead();
        }

        public void CheckIfDead()
        {
            if (spindles.Count == 0)
            {
                Die();
            }
        }

        protected virtual void Die()
        {
            crystal.ActivateCrystal(); // TODO: handle this with crystal.Activate()
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

