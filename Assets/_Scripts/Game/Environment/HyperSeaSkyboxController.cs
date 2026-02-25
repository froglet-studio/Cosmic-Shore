using UnityEngine;

namespace CosmicShore.Game
{
    /// <summary>
    /// Sets the HyperSea procedural skybox as the active RenderSettings skybox.
    /// Bakes the Andromeda galaxy to a texture at startup to eliminate per-pixel
    /// noise computation and rastering artifacts.
    /// </summary>
    public class HyperSeaSkyboxController : MonoBehaviour
    {
        [SerializeField] Material hyperSeaSkyboxMaterial;

        [Header("Runtime Overrides")]
        [SerializeField] bool animateCoreDirection;
        [SerializeField] float coreOrbitSpeed = 0.01f;

        [Header("Andromeda Bake")]
        [SerializeField] int andromedaResolution = 256;

        Material runtimeMaterial;
        Material previousSkybox;
        RenderTexture andromedaRT;

        static readonly int GalacticNormalID = Shader.PropertyToID("_GalacticNormal");
        static readonly int CoreDirectionID = Shader.PropertyToID("_CoreDirection");
        static readonly int CoreBrightnessID = Shader.PropertyToID("_CoreBrightness");
        static readonly int GalacticBrightnessID = Shader.PropertyToID("_GalacticBrightness");
        static readonly int NebulaStrengthID = Shader.PropertyToID("_NebulaStrength");
        static readonly int StarBrightnessID = Shader.PropertyToID("_StarBrightness");
        static readonly int AndromedaTexID = Shader.PropertyToID("_AndromedaTex");

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

            BakeAndromeda();
        }

        void OnDisable()
        {
            RenderSettings.skybox = previousSkybox;

            if (runtimeMaterial != null)
            {
                Destroy(runtimeMaterial);
                runtimeMaterial = null;
            }

            if (andromedaRT != null)
            {
                andromedaRT.Release();
                Destroy(andromedaRT);
                andromedaRT = null;
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

        void BakeAndromeda()
        {
            var bakeShader = Shader.Find("Hidden/CosmicShore/HyperSeaAndromedaBake");
            if (bakeShader == null)
            {
                Debug.LogWarning("HyperSeaSkyboxController: Andromeda bake shader not found, skipping bake.");
                return;
            }

            andromedaRT = new RenderTexture(andromedaResolution, andromedaResolution, 0, RenderTextureFormat.ARGBHalf);
            andromedaRT.filterMode = FilterMode.Bilinear;
            andromedaRT.wrapMode = TextureWrapMode.Clamp;
            andromedaRT.Create();

            var bakeMat = new Material(bakeShader);
            bakeMat.SetFloat("_AndromedaSize", runtimeMaterial.GetFloat("_AndromedaSize"));
            bakeMat.SetColor("_AndromedaDiskColor", runtimeMaterial.GetColor("_AndromedaDiskColor"));
            bakeMat.SetColor("_AndromedaNucleusColor", runtimeMaterial.GetColor("_AndromedaNucleusColor"));
            bakeMat.SetFloat("_AndromedaBrightness", runtimeMaterial.GetFloat("_AndromedaBrightness"));

            Graphics.Blit(null, andromedaRT, bakeMat);

            runtimeMaterial.SetTexture(AndromedaTexID, andromedaRT);

            Destroy(bakeMat);
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
            runtimeMaterial.SetFloat(CoreBrightnessID, 1.5f * multiplier);
            runtimeMaterial.SetFloat(GalacticBrightnessID, 0.6f * multiplier);
            runtimeMaterial.SetFloat(NebulaStrengthID, 0.35f * multiplier);
            runtimeMaterial.SetFloat(StarBrightnessID, 2.5f * multiplier);
        }
    }
}
