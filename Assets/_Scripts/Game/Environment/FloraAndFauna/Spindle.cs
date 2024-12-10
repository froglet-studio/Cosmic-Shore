using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore
{
    public class Spindle : MonoBehaviour
    {
        public Renderer RenderedObject;
        [SerializeField] Spindle parentSpindle;
        public LifeForm LifeForm;
        [SerializeField] bool retainSpindle = false;

        HashSet<HealthBlock> healthBlocks = new HashSet<HealthBlock>();
        HashSet<Spindle> spindles = new HashSet<Spindle>();

        private Material originalMaterial; // Store the original shared material
        private Material temporaryMaterial; // Temporary material for animations

        private IEnumerator Start()
        {
            if (RenderedObject.sharedMaterial == null)
            {
                Debug.LogError($"{gameObject.name}: RenderedObject does not have a valid material at Start.");
                yield break;
            }

            // Cache the original shared material
            originalMaterial = RenderedObject.sharedMaterial;

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

        private void UseTemporaryMaterial()
        {
            // Create a new temporary material based on the original material
            temporaryMaterial = new Material(originalMaterial);
            RenderedObject.material = temporaryMaterial;
        }

        private void RestoreOriginalMaterial()
        {
            // Restore the original shared material
            RenderedObject.material = originalMaterial;

            // Clean up the temporary material
            if (temporaryMaterial != null)
            {
                Destroy(temporaryMaterial);
                temporaryMaterial = null;
            }
        }

        IEnumerator EvaporateCoroutine()
        {
            UseTemporaryMaterial(); // Switch to the temporary material

            float deathAnimation = 0f;
            float animationSpeed = 1f;
            while (deathAnimation < 1f)
            {
                temporaryMaterial.SetFloat("_DeathAnimation", deathAnimation);
                deathAnimation += Time.deltaTime * animationSpeed;
                yield return null;
            }
            Destroy(gameObject);          
        }

        IEnumerator CondenseCoroutine()
        {
            UseTemporaryMaterial(); // Switch to the temporary material

            float deathAnimation = 1f;
            float animationSpeed = 1f;
            while (deathAnimation > 0f)
            {
                temporaryMaterial.SetFloat("_DeathAnimation", deathAnimation);
                deathAnimation -= Time.deltaTime * animationSpeed;
                yield return null;
            }

            temporaryMaterial.SetFloat("_DeathAnimation", 0);
            RestoreOriginalMaterial(); // Switch back to the original material
        }

        void DisableSpindle()
        {
            RestoreOriginalMaterial();

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