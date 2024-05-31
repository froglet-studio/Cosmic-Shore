using System.Collections;
using UnityEngine;

namespace CosmicShore
{
    public class Spindle : MonoBehaviour
    {
        public GameObject cylinder;
        public GameObject renderedObject;
        public float Length = 1f;

        void Awake()
        {
            Length = cylinder.transform.localScale.y;
        }

        public void CheckForLife()
        {
            //Debug.Log($"Checking spindle for life: GetComponentsInChildren<HealthBlock>().length = {GetComponentsInChildren<HealthBlock>().Length} GetComponentsInChildren<Spindle>().Length = {GetComponentsInChildren<Spindle>().Length}");
            if (GetComponentsInChildren<HealthBlock>().Length < 1 && GetComponentsInChildren<Spindle>().Length <= 1)
            {
                Debug.Log("Spindle.Evaporating");
                StartCoroutine(Evaporate());
            }
        }

        IEnumerator Evaporate()
        {
            MeshRenderer meshRenderer = renderedObject.GetComponent<MeshRenderer>();
            float deathAnimation = 0f;
            float animationSpeed = 1f;
            while (deathAnimation < 1f)
            {
                meshRenderer.material.color = Color.Lerp(meshRenderer.material.color, Color.clear, 0.1f);
                yield return new WaitForSeconds(0.1f);
            }
            Destroy(gameObject);
        }

    }

}
