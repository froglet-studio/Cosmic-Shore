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

        protected List<HealthBlock> healthBlocks = new List<HealthBlock>();
        protected List<Spindle> spindles = new List<Spindle>();

        protected Crystal crystal;
        [SerializeField] protected Material activeCrystalMaterial; // TODO: make a crytal material set that pulls from the element
        
        protected Node node;

        protected virtual void Start()
        {
            crystal = GetComponentInChildren<Crystal>();
            node = NodeControlManager.Instance.GetNodeByPosition(transform.position);
        }

        public void AddHealthBlock(HealthBlock healthBlock)
        {
            healthBlocks.Add(healthBlock);
            healthBlock.LifeForm = this;
        }

        public void AddSpindle(Spindle spindle)
        {
            spindles.Add(spindle);
            spindle.LifeForm = this;
        }

        public void RemoveSpindle(Spindle spindle)
        {
            spindles.Remove(spindle);
        }

        public void RemoveHealthBlock(HealthBlock healthBlock)
        {
            healthBlocks.Remove(healthBlock);
            //CheckIfDead();
        }

        public void CheckIfDead()
        {
            Debug.Log($"Lifeform.spindles.Count {spindles.Count}");
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

