using UnityEngine;

namespace CosmicShore.Game
{
    /// <summary>
    /// Sets the HyperSea procedural skybox as the active RenderSettings skybox.
    /// Place on any GameObject in your scene. Assign the HyperSeaSkybox material.
    /// </summary>
    public class HyperSeaSkyboxController : MonoBehaviour
    {
        [SerializeField] Material hyperSeaSkyboxMaterial;

        [Header("Runtime Overrides")]
        [SerializeField] bool animateCoreDirection;
        [SerializeField] float coreOrbitSpeed = 0.01f;

        Material runtimeMaterial;
        Material previousSkybox;

        static readonly int GalacticNormalID = Shader.PropertyToID("_GalacticNormal");
        static readonly int CoreDirectionID = Shader.PropertyToID("_CoreDirection");
        static readonly int CoreBrightnessID = Shader.PropertyToID("_CoreBrightness");
        static readonly int GalacticBrightnessID = Shader.PropertyToID("_GalacticBrightness");
        static readonly int NebulaStrengthID = Shader.PropertyToID("_NebulaStrength");
        static readonly int StarBrightnessID = Shader.PropertyToID("_StarBrightness");

        void OnEnable()
        {
            if (hyperSeaSkyboxMaterial == null)
            {
                var shader = Shader.Find("CosmicShore/HyperSeaSkybox");
                if (shader == null)
                {
                    Debug.LogError("HyperSeaSkyboxController: Could not find CosmicShore/HyperSeaSkybox shader.");
                    return;
                }
                runtimeMaterial = new Material(shader);
            }
            else
            {
                runtimeMaterial = new Material(hyperSeaSkyboxMaterial);
            }

            previousSkybox = RenderSettings.skybox;
            RenderSettings.skybox = runtimeMaterial;
        }

        void OnDisable()
        {
            RenderSettings.skybox = previousSkybox;

            if (runtimeMaterial != null)
            {
                Destroy(runtimeMaterial);
                runtimeMaterial = null;
            }
        }

        void Update()
        {
            if (runtimeMaterial == null) return;

            if (animateCoreDirection)
            {
                float angle = Time.time * coreOrbitSpeed;
                Vector4 coreDir = new Vector4(
                    Mathf.Cos(angle),
                    -0.1f + 0.05f * Mathf.Sin(angle * 0.7f),
                    Mathf.Sin(angle),
                    0f
                );
                runtimeMaterial.SetVector(CoreDirectionID, coreDir);
            }
        }

        /// <summary>
        /// Set the galactic plane orientation at runtime.
        /// </summary>
        public void SetGalacticNormal(Vector3 normal)
        {
            if (runtimeMaterial != null)
                runtimeMaterial.SetVector(GalacticNormalID, normal.normalized);
        }

        /// <summary>
        /// Set the galactic core direction at runtime.
        /// </summary>
        public void SetCoreDirection(Vector3 direction)
        {
            if (runtimeMaterial != null)
                runtimeMaterial.SetVector(CoreDirectionID, direction.normalized);
        }

        /// <summary>
        /// Set overall brightness multiplier for the skybox.
        /// </summary>
        public void SetBrightness(float multiplier)
        {
            if (runtimeMaterial == null) return;
            runtimeMaterial.SetFloat(CoreBrightnessID, 5f * multiplier);
            runtimeMaterial.SetFloat(GalacticBrightnessID, 1.4f * multiplier);
            runtimeMaterial.SetFloat(NebulaStrengthID, 0.55f * multiplier);
            runtimeMaterial.SetFloat(StarBrightnessID, 2.5f * multiplier);
        }
    }
}
