using UnityEngine;

namespace CosmicShore.Game
{
    /// <summary>
    /// Sets the HyperSea procedural skybox as the active RenderSettings skybox.
    /// Bakes the Andromeda galaxy to a texture at startup to eliminate per-pixel
    /// noise computation and rastering artifacts.
    /// Bridges visual coherence between the photorealistic skybox and the
    /// geometric membrane via cellular overlay and atmospheric haze.
    /// </summary>
    public class HyperSeaSkyboxController : MonoBehaviour
    {
        [SerializeField] Material hyperSeaSkyboxMaterial;

        [Header("Runtime Overrides")]
        [SerializeField] bool animateCoreDirection;
        [SerializeField] float coreOrbitSpeed = 0.01f;

        [Header("Andromeda Bake")]
        [SerializeField] int andromedaResolution = 256;

        [Header("Membrane Aesthetic Bridge")]
        [Tooltip("When enabled, reads bright/dull colors from the membrane material and tints the skybox overlay and atmosphere to match.")]
        [SerializeField] bool syncWithMembrane = true;
        [Tooltip("The membrane material to sample palette colors from. If null, uses fallback colors from the skybox material.")]
        [SerializeField] Material membraneMaterialReference;
        [Tooltip("How strongly the membrane palette influences the skybox overlay (0 = skybox defaults, 1 = pure membrane colors).")]
        [SerializeField, Range(0f, 1f)] float membraneTintInfluence = 0.6f;

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
        static readonly int CellOverlayColorID = Shader.PropertyToID("_CellOverlayColor");
        static readonly int AtmosphereColorID = Shader.PropertyToID("_AtmosphereColor");
        static readonly int CellOverlayStrengthID = Shader.PropertyToID("_CellOverlayStrength");
        static readonly int AtmosphereStrengthID = Shader.PropertyToID("_AtmosphereStrength");

        // Membrane property IDs (SpindleGraph convention)
        static readonly int MembraneBrightColorID = Shader.PropertyToID("_BrightColor");
        static readonly int MembraneDullColorID = Shader.PropertyToID("_DullColor");

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
            SyncMembraneColors();
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

        /// <summary>
        /// Reads bright/dull colors from the membrane material and blends them into
        /// the skybox's cellular overlay and atmosphere bridge layers. This creates
        /// visual coherence between the geometric membrane and the photorealistic skybox.
        /// </summary>
        public void SyncMembraneColors()
        {
            if (runtimeMaterial == null || !syncWithMembrane) return;
            if (membraneMaterialReference == null) return;

            Color bright = membraneMaterialReference.GetColor(MembraneBrightColorID);
            Color dull = membraneMaterialReference.GetColor(MembraneDullColorID);

            // Blend membrane palette into the overlay and atmosphere
            Color baseCellColor = runtimeMaterial.GetColor(CellOverlayColorID);
            Color baseAtmoColor = runtimeMaterial.GetColor(AtmosphereColorID);

            Color cellTarget = Color.Lerp(baseCellColor, bright, membraneTintInfluence);
            Color atmoTarget = Color.Lerp(baseAtmoColor, dull, membraneTintInfluence);

            runtimeMaterial.SetColor(CellOverlayColorID, cellTarget);
            runtimeMaterial.SetColor(AtmosphereColorID, atmoTarget);
        }

        /// <summary>
        /// Set the cellular overlay strength (0 = invisible, 0.5 = maximum).
        /// Controls how visible the Voronoi geometric pattern is on the skybox.
        /// </summary>
        public void SetCellOverlayStrength(float strength)
        {
            if (runtimeMaterial != null)
                runtimeMaterial.SetFloat(CellOverlayStrengthID, Mathf.Clamp01(strength) * 0.5f);
        }

        /// <summary>
        /// Set the atmosphere bridge strength (0 = invisible, 1 = full).
        /// Controls the directional haze that connects membrane and skybox palettes.
        /// </summary>
        public void SetAtmosphereStrength(float strength)
        {
            if (runtimeMaterial != null)
                runtimeMaterial.SetFloat(AtmosphereStrengthID, Mathf.Clamp01(strength));
        }

        /// <summary>
        /// Assign a new membrane material reference at runtime and re-sync colors.
        /// Call this when the cell membrane changes (e.g., different cell config).
        /// </summary>
        public void SetMembraneMaterial(Material membrane)
        {
            membraneMaterialReference = membrane;
            SyncMembraneColors();
        }
    }
}
