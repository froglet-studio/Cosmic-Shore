using CosmicShore.Core;
using Mono.CSharp;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore
{
    public class Spindle : MonoBehaviour
    {
        void Start()
        {
            CheckForLife();
        }

        public void CheckForLife()
        {
            if (GetComponentInChildren<HealthBlock>() == null && GetComponentInParent<HealthBlock>() == null)
            {
                Evaporate();
            }
        }

        IEnumerator Evaporate()
        {
            MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
            while (meshRenderer.material.color.a > 0.1f)
            {
                meshRenderer.material.color = Color.Lerp(meshRenderer.material.color, Color.clear, 0.1f);
                yield return new WaitForSeconds(0.1f);
            }
            Destroy(gameObject);
        }

    }

}
