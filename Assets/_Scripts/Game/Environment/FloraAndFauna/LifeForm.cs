using CosmicShore.Environment.FlowField;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace CosmicShore
{
    public abstract class LifeForm : MonoBehaviour
    {

        [SerializeField] protected HealthBlock healthBlock;
        [SerializeField] protected Spindle spindle;
        [SerializeField] protected int minHealthBlocks = 0;

        private List<HealthBlock> healthBlocks = new List<HealthBlock>();
        private List<Spindle> spindles = new List<Spindle>();

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
        }

        public void RemoveHealthBlock(HealthBlock healthBlock)
        {
            healthBlocks.Remove(healthBlock);
            CheckSpindles();
            CheckIfDead();
            //Debug.Log("HealthBlocks: " + healthBlocks.Count);
        }

        void CheckSpindles()
        {
            foreach (Spindle spindle in spindles)
            {
                spindle.CheckForLife();
            }
        }

        void CheckIfDead()
        {
            if (healthBlocks.Count == minHealthBlocks)
            {
                Die();
            }
        }

        protected virtual void Die()
        {
            crystal.transform.parent = node.transform; // TODO: handle this with crystal.Activate()
            crystal.gameObject.GetComponent<SphereCollider>().enabled = true;
            crystal.enabled = true;

            crystal.GetComponentInChildren<SkinnedMeshRenderer>().material = activeCrystalMaterial; // TODO: make a crytal material set that this pulls from using the element
            StopAllCoroutines();
            Destroy(gameObject);
            return;
        }
    }
}

