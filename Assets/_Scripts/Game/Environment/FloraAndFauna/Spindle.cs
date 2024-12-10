using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore
{
    public class Spindle : MonoBehaviour
    {
        public GameObject cylinder;
        public Renderer RenderedObject;
        [SerializeField] Spindle parentSpindle;
        public LifeForm LifeForm;
        [SerializeField] bool retainSpindle = false;

        HashSet<HealthBlock> healthBlocks = new HashSet<HealthBlock>();
        HashSet<Spindle> spindles = new HashSet<Spindle>();

        private void Start()
        {
            StartCoroutine(CondenseCoroutine());
            if (LifeForm) LifeForm.AddSpindle(this);
            parentSpindle ??= transform.parent.GetComponentInParent<Spindle>();
            if (parentSpindle) parentSpindle.AddSpindle(this);
        }

        public void AddHealthBlock(HealthBlock healthBlock)
        {
            healthBlocks.Add(healthBlock);
            healthBlock.LifeForm = LifeForm;
        }

        public void RemoveHealthBlock(HealthBlock healthBlock)
        {
            healthBlocks.Remove(healthBlock);
        }

        public void AddSpindle(Spindle spindle)
        {
            spindles.Add(spindle);
            spindle.parentSpindle = this;
        }

        public void RemoveSpindle(Spindle spindle)
        {
            spindles.Remove(spindle);
        }

        public void CheckForLife()
        {
            if (healthBlocks.Count < 1 && spindles.Count < 1)
            {
                EvaporateSpindle();
            }
        }

        public void EvaporateSpindle()
        {
            if (gameObject.activeInHierarchy) StartCoroutine(EvaporateCoroutine());
        }

        IEnumerator EvaporateCoroutine()
        {
            float deathAnimation = 0f;
            float animationSpeed = 1f;
            while (deathAnimation < 1f)
            {
                RenderedObject.material.SetFloat("_DeathAnimation", deathAnimation);
                deathAnimation += Time.deltaTime * animationSpeed;
                yield return null;
            }

            if (retainSpindle) 
            {
                gameObject.SetActive(false); 
                DisableSpindle(); 
            }
            else Destroy(gameObject);          
        }

        IEnumerator CondenseCoroutine()
        {
            float deathAnimation = 1f;
            float animationSpeed = 1f;
            while (deathAnimation > 0f)
            {
                RenderedObject.material.SetFloat("_DeathAnimation", deathAnimation);
                deathAnimation -= Time.deltaTime * animationSpeed;
                yield return null;
            }
            RenderedObject.material.SetFloat("_DeathAnimation", 0);
        }

        void DisableSpindle()
        {
            // check if scene is still loaded
            if (gameObject.scene.isLoaded)
            {
                if (parentSpindle)
                {
                    parentSpindle.RemoveSpindle(this);
                    parentSpindle.CheckForLife();
                    LifeForm.RemoveSpindle(this);
                }
                else
                {
                    LifeForm.RemoveSpindle(this);
                    LifeForm.CheckIfDead();
                }
            }
        }

        private void OnDestroy()
        {
            DisableSpindle();
        }
    }
}