using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using CosmicShore.Utility;
namespace CosmicShore.Utility
{
    public class MaterialInstancerSSU : InstancerSSU
    {
        void Awake()
        {
            if (TryGetComponent(out Graphic graphic))
            {
                graphic.material = Instantiate(graphic.material);
                runtimeMaterial = graphic.materialForRendering;
            }

            if (TryGetComponent(out Renderer renderer))
            {
                Material[] materials = renderer.sharedMaterials;
                for(int n = 0; n < materials.Length; n++)
                {
                    materials[n] = Instantiate(materials[n]);
                }

                renderer.materials = renderer.sharedMaterials = materials;
                runtimeMaterial = materials[0];
            }
        }
    }
}
