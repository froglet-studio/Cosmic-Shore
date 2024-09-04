using System.Collections;
using UnityEngine;

namespace CosmicShore
{
    public class Spindle : MonoBehaviour
    {
        public GameObject cylinder;
        public MeshRenderer RenderedObject;
        Spindle parentSpindle;
        public LifeForm LifeForm;

        private void Start()
        {
            StartCoroutine(CondenseCoroutine());
            if (LifeForm) LifeForm.AddSpindle(this);
        }

        public void CheckForLife()
        {
            HealthBlock healthBlock;
            healthBlock = GetComponentInChildren<HealthBlock>();
            if ((!healthBlock || healthBlock && healthBlock.destroyed) && GetComponentsInChildren<Spindle>().Length <= 1)
            {
                EvaporateSpindle();
            }
        }

        public void EvaporateSpindle()
        {
            StartCoroutine(EvaporateCoroutine());
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
            transform.parent.TryGetComponent(out parentSpindle);
            Destroy(gameObject);          
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

        private void OnDestroy()
        {
            // check if scene is still loaded
            if (gameObject.scene.isLoaded)
            {
                LifeForm.RemoveSpindle(this);
                if (parentSpindle) parentSpindle.CheckForLife();
                else 
                {
                    LifeForm.CheckIfDead();
                }
            }
        }
    }
}