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

        Material originalMaterial; // Store the original shared material
        Material temporaryMaterial; // Temporary material for animations
        Coroutine condenseCoroutine;

        IEnumerator Start()
        {
            if (RenderedObject.sharedMaterial == null)
            {
                Debug.LogError($"{gameObject.name}: RenderedObject does not have a valid material at Start.");
                yield break;
            }

            // Cache the original shared material
            originalMaterial = RenderedObject.sharedMaterial;

            condenseCoroutine = StartCoroutine(CondenseCoroutine());
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
            if (gameObject.activeInHierarchy)
                StartCoroutine(EvaporateCoroutine());
        }

        void RestoreOriginalMaterial()
        {
            //Debug.Log($"TemporaryMaterial - RestoreOriginalMaterial:{gameObject.GetInstanceID()}");
            // Restore the original shared material
            RenderedObject.material = originalMaterial;

            // Clean up the temporary material
            if (temporaryMaterial != null)
            {
                Destroy(temporaryMaterial);
                temporaryMaterial = null;
            }
        }


        void UseTemporaryMaterial()
        {
            //Debug.Log($"TemporaryMaterial - UseTemporaryMaterial:{gameObject.GetInstanceID()}");

            // Create a new temporary material based on the original material
            temporaryMaterial = new Material(originalMaterial);
            RenderedObject.material = temporaryMaterial;
        }

        IEnumerator EvaporateCoroutine()
        {
            if (condenseCoroutine != null)
            {
                StopCoroutine(condenseCoroutine);
                condenseCoroutine = null;
            }

            //Debug.Log($"TemporaryMaterial - EvaporateCoroutine:{gameObject.GetInstanceID()}");
            UseTemporaryMaterial(); // Switch to the temporary material

            float deathAnimation = 0f;
            float animationSpeed = 1f;
            while (deathAnimation < 1f)
            {
                yield return null;

                if (temporaryMaterial == null)
                {
                    //Debug.LogError($"TemporaryMaterial creation failed: {gameObject.GetInstanceID()}");
                    yield break;
                }

                temporaryMaterial.SetFloat("_DeathAnimation", deathAnimation);
                deathAnimation += Time.deltaTime * animationSpeed;
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
            //Debug.Log($"TemporaryMaterial - CondenseCoroutine:{gameObject.GetInstanceID()}");
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
            //Debug.Log($"TemporaryMaterial - DisableSpindle:{gameObject.GetInstanceID()}");
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

        void OnDestroy()
        {
            //Debug.Log($"TemporaryMaterial - OnDestroy:{gameObject.GetInstanceID()}");
            DisableSpindle();
        }
    }
}