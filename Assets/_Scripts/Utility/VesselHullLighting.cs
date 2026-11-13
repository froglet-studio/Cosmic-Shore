using System.Collections.Generic;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// The CPU half of the HULL LIGHT (<c>VesselHullLighting.hlsl</c>, Docs/VesselModels/COLOR.md section 2a): every
    /// vessel hull lit by the world it flies in.
    ///
    /// Once per frame it reads the live skybox material - the galactic core's direction, colour and brightness, the
    /// galactic band's plane and emission, the sky's cell glow - and publishes them as shader globals with the
    /// tuning from <see cref="VesselHullLightingConfigSO"/>. Nine global writes a frame, none of them per vessel.
    /// Reading the material LIVE is what makes the hull light follow the colour set: WorldColorTrace repaints the
    /// skybox from the worn set, so the core's colour on a hull is always the core's colour in the sky.
    ///
    /// A scene whose sky is not the HyperSea sky (no <c>_CoreDirection</c>) still lights its hulls: a key from
    /// above and in front, white, and the scene's ambient sky colour as the fill, with no band.
    ///
    /// Per vessel it holds one thing, the pilot's domain colour for the rim (<c>_VesselHullDomain</c>), written by
    /// <see cref="StampDomain"/> through a MaterialPropertyBlock with get-modify-set (the vision band and the Echo
    /// Sight write the same blocks). Unlike the vision band's tint it is never cleared for the local pilot: the
    /// rim is a look, not an identification aid, and your own ship should look its best.
    /// </summary>
    public static class VesselHullLighting
    {
        static readonly int KeyId = Shader.PropertyToID("_VesselHullKey");
        static readonly int KeyColorId = Shader.PropertyToID("_VesselHullKeyColor");
        static readonly int SkyColorId = Shader.PropertyToID("_VesselHullSkyColor");
        static readonly int BandId = Shader.PropertyToID("_VesselHullBand");
        static readonly int BandColorId = Shader.PropertyToID("_VesselHullBandColor");
        static readonly int ShapeId = Shader.PropertyToID("_VesselHullShape");
        static readonly int SpecularId = Shader.PropertyToID("_VesselHullSpecular");
        static readonly int ReflectId = Shader.PropertyToID("_VesselHullReflect");
        static readonly int RimId = Shader.PropertyToID("_VesselHullRim");

        /// <summary>The per-renderer domain colour for the rim. Alpha 1 marks "stamped".</summary>
        public static readonly int DomainId = Shader.PropertyToID("_VesselHullDomain");

        // The HyperSea skybox's own properties (HyperSeaSkybox.shader).
        static readonly int SkyCoreDirectionId = Shader.PropertyToID("_CoreDirection");
        static readonly int SkyCoreColorId = Shader.PropertyToID("_CoreColor");
        static readonly int SkyCoreBrightnessId = Shader.PropertyToID("_CoreBrightness");
        static readonly int SkyGalacticNormalId = Shader.PropertyToID("_GalacticNormal");
        static readonly int SkyGalacticEmissionId = Shader.PropertyToID("_GalacticEmission");
        static readonly int SkyGalacticBrightnessId = Shader.PropertyToID("_GalacticBrightness");
        static readonly int SkyCellOverlayColorId = Shader.PropertyToID("_CellOverlayColor");
        static readonly int SkyCellOverlayStrengthId = Shader.PropertyToID("_CellOverlayStrength");

        /// <summary>Key direction for a sky that names no core: above, ahead and to the side.</summary>
        static readonly Vector3 FallbackKeyDirection = new Vector3(0.45f, 0.75f, -0.5f).normalized;

        const float FallbackKeyBrightness = 0.6f;

        const string ConfigResourcePath = "VesselHullLightingConfig";

        static VesselHullLightingConfigSO _config;
        static bool _configResolved;
        static MaterialPropertyBlock _block;
        static readonly List<Material> _scratchMaterials = new();

        public static VesselHullLightingConfigSO Config
        {
            get
            {
                if (!_configResolved)
                {
                    _config = Resources.Load<VesselHullLightingConfigSO>(ConfigResourcePath);
                    if (_config == null) _config = ScriptableObject.CreateInstance<VesselHullLightingConfigSO>();
                    _configResolved = true;
                }
                return _config;
            }
        }

        /// <summary>
        /// A live override of the config's strength, for A/B captures (the trailer director's before and after, the
        /// look sheet). Null = the config decides. Never set by gameplay.
        /// </summary>
        public static float? StrengthOverride { get; set; }

        /// <summary>Forget the cached config, so the next frame reloads it (tests, domain reload).</summary>
        public static void InvalidateConfig()
        {
            _config = null;
            _configResolved = false;
        }

        /// <summary>
        /// Stamps the pilot's domain colour on every hull renderer under <paramref name="root"/> for the rim. Only
        /// slots on the wired graph take it (the property gate is the filter). Inactive renderers included, as the
        /// vision band does, so a morph or rig swap that switches one on is already stamped.
        /// </summary>
        public static void StampDomain(Transform root, Color domainColor)
        {
            if (root == null) return;
            domainColor.a = 1f;
            _block ??= new MaterialPropertyBlock();

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                var renderer = renderers[r];
                if (!renderer) continue;
                renderer.GetSharedMaterials(_scratchMaterials);
                for (int m = 0; m < _scratchMaterials.Count; m++)
                {
                    var material = _scratchMaterials[m];
                    if (!material || !material.HasColor(DomainId)) continue;
                    renderer.GetPropertyBlock(_block, m);
                    _block.SetColor(DomainId, domainColor);
                    renderer.SetPropertyBlock(_block, m);
                }
            }
            _scratchMaterials.Clear();
        }

        /// <summary>
        /// The sky light as the shader receives it, for tests and tools: key direction, key colour (linear,
        /// times brightness), sky colour (linear), band normal and band colour (linear). Pure: reads the
        /// material, writes nothing.
        /// </summary>
        public static void ReadSky(Material sky, out Vector3 keyDirection, out Color keyColor, out Color skyColor,
            out Vector3 bandNormal, out Color bandColor)
        {
            if (sky != null && sky.HasVector(SkyCoreDirectionId) && sky.HasColor(SkyCoreColorId))
            {
                Vector3 core = sky.GetVector(SkyCoreDirectionId);
                keyDirection = core.sqrMagnitude > 1e-6f ? core.normalized : FallbackKeyDirection;
                float brightness = sky.HasFloat(SkyCoreBrightnessId) ? sky.GetFloat(SkyCoreBrightnessId) : 1f;
                keyColor = Linear(sky.GetColor(SkyCoreColorId)) * brightness;

                Vector3 plane = sky.HasVector(SkyGalacticNormalId) ? (Vector3)sky.GetVector(SkyGalacticNormalId) : Vector3.up;
                bandNormal = plane.sqrMagnitude > 1e-6f ? plane.normalized : Vector3.up;
                float bandBrightness = sky.HasFloat(SkyGalacticBrightnessId) ? sky.GetFloat(SkyGalacticBrightnessId) : 1f;
                bandColor = sky.HasColor(SkyGalacticEmissionId)
                    ? Linear(sky.GetColor(SkyGalacticEmissionId)) * bandBrightness
                    : Color.black;

                float cellStrength = sky.HasFloat(SkyCellOverlayStrengthId) ? sky.GetFloat(SkyCellOverlayStrengthId) : 1f;
                skyColor = sky.HasColor(SkyCellOverlayColorId)
                    ? Linear(sky.GetColor(SkyCellOverlayColorId)) * cellStrength
                    : Linear(RenderSettings.ambientSkyColor);
            }
            else
            {
                keyDirection = FallbackKeyDirection;
                keyColor = Color.white * FallbackKeyBrightness;
                skyColor = Linear(RenderSettings.ambientSkyColor);
                bandNormal = Vector3.up;
                bandColor = Color.black;
            }

            keyColor.a = skyColor.a = bandColor.a = 1f;
        }

        static Color Linear(Color c) => QualitySettings.activeColorSpace == ColorSpace.Linear ? c.linear : c;

        static void Publish()
        {
            var config = Config;
            float strength = Mathf.Clamp01(StrengthOverride ?? config.Strength);
            if (strength <= 0f)
            {
                PublishOff();
                return;
            }

            ReadSky(RenderSettings.skybox, out var key, out var keyColor, out var skyColor, out var band, out var bandColor);

            // SetGlobalVector, not SetGlobalColor: the colours are already linear (ReadSky converted them), and
            // SetGlobalColor would convert a second time.
            Shader.SetGlobalVector(KeyId, new Vector4(key.x, key.y, key.z, strength));
            Shader.SetGlobalVector(KeyColorId, keyColor);
            Shader.SetGlobalVector(SkyColorId, skyColor);
            Shader.SetGlobalVector(BandId, new Vector4(band.x, band.y, band.z, config.BandWidth));
            Shader.SetGlobalVector(BandColorId, bandColor);
            Shader.SetGlobalVector(ShapeId, config.PackShape());
            Shader.SetGlobalVector(SpecularId, config.PackSpecular());
            Shader.SetGlobalVector(ReflectId, config.PackReflect());
            Shader.SetGlobalVector(RimId, config.PackRim());
        }

        /// <summary>w = 0 on the key is the shader's "off": the hull renders exactly as the unlit graph did.</summary>
        static void PublishOff() => Shader.SetGlobalVector(KeyId, Vector4.zero);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void InstallPublisher()
        {
            InvalidateConfig();
            StrengthOverride = null;
            // Globals survive play-mode exit in the editor; start from off so nothing stale lights a hull.
            PublishOff();
            var go = new GameObject("[VesselHullLighting]") { hideFlags = HideFlags.HideInHierarchy };
            Object.DontDestroyOnLoad(go);
            go.AddComponent<Publisher>();
        }

        sealed class Publisher : MonoBehaviour
        {
            // Every frame, so a colour-set change, a scene's own sky or an edit to the config asset is live.
            void LateUpdate() => Publish();

            void OnDisable() => PublishOff();
        }
    }
}
