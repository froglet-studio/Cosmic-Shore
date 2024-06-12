using System.Collections;
using UnityEngine;

namespace CosmicShore
{
    public class Spindle : MonoBehaviour
    {
        public GameObject cylinder;
        public GameObject renderedObject;
        public float Length = 1f;
        Spindle parentSpindle;
        public LifeForm LifeForm;

        void Awake()
        {
            if (cylinder) Length = cylinder.transform.localScale.y;
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
            StartCoroutine(Evaporate());
        }

        IEnumerator Evaporate()
        {
            
            MeshRenderer meshRenderer = renderedObject.GetComponent<MeshRenderer>();
            float deathAnimation = 0f;
            float animationSpeed = 1f;
            while (deathAnimation < 1f)
            {
                meshRenderer.material.SetFloat("_DeathAnimation", deathAnimation);
                deathAnimation += Time.deltaTime * animationSpeed;
                yield return null;
            }
            transform.parent.TryGetComponent(out parentSpindle);
            Destroy(gameObject);          
        }


        private void OnDestroy()
        {
            LifeForm.RemoveSpindle(this);
            if (parentSpindle) parentSpindle.CheckForLife();
            else LifeForm.CheckIfDead();
        }

    }
}
