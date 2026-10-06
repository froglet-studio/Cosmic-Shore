using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;

namespace CosmicShore.Client
{
    /// <summary>
    /// The race's authored theme data (rung 5) — client-side factory, not a port: it
    /// transcribes the Unity project's wired theme assets into the ported SO types so the
    /// REAL ThemeManager pipeline (the single writer of
    /// <see cref="ThemeManagerDataContainerSO.TeamMaterialSets"/> and of
    /// <c>GameFeedAPI.ColorSet</c>) runs on the game's actual palette.
    ///
    /// Sources (the assets ThemeManagerDataContainer.asset references — see
    /// <see cref="ColorSetAssetPath"/> / <see cref="MaterialSetAssetPath"/>):
    ///   • ColorSet  = OriginalColorSetSO.asset (guid 8d1c1bdcb636a424d9c54fbd1afc952f)
    ///   • Materials = OriginalMaterialSet.asset (guid 2cc7d7c117bfd194eb0377c17b230c14)
    /// Every color below is the asset's exact serialized value (HDR intensities &gt; 1
    /// preserved — the renderer tone-maps). The base materials are the engine's data-only
    /// property stores named after the .mat files the original set references; their
    /// domain-facing color properties are written by ThemeManager.GenerateDomainMaterialSet,
    /// which is the only part of them the port reads.
    /// </summary>
    public static class SkimRaceTheme
    {
        public const string ColorSetAssetPath = "Assets/_SO_Assets/Color Palettes/OriginalColorSetSO.asset";
        public const string MaterialSetAssetPath = "Assets/_SO_Assets/MaterialSets/OriginalMaterialSet.asset";
        public const string ContainerAssetPath = "Assets/_SO_Assets/ThemeManagerDataContainer.asset";

        /// <summary>
        /// The wired container, exactly as ThemeManagerDataContainer.asset authors it:
        /// the OriginalColorSetSO palette + the OriginalMaterialSet base materials.
        /// Spawn a <see cref="ThemeManager"/> with this container so its Awake generates
        /// the four per-domain material sets (the original Bootstrap-scene flow).
        /// </summary>
        public static ThemeManagerDataContainerSO CreateContainer()
        {
            var container = ScriptableObject.CreateInstance<ThemeManagerDataContainerSO>();
            container.name = "ThemeManagerDataContainer";
            container.ColorSet = CreateColorSet();
            container.BaseMaterialSet = CreateBaseMaterialSet();
            return container;
        }

        /// <summary>Verbatim transcription of OriginalColorSetSO.asset (see <see cref="ColorSetAssetPath"/>).</summary>
        public static SO_ColorSet CreateColorSet()
        {
            var colorSet = ScriptableObject.CreateInstance<SO_ColorSet>();
            colorSet.name = "OriginalColorSetSO";

            colorSet.JadeColors = new DomainColorSet
            {
                ShipColor1 = new Color(0f, 0.04901961f, 0.09411765f, 1f),
                ShipColor2 = new Color(0f, 0.3882353f, 0.7490196f, 1f),
                OutsideBlockColor = new Color(0f, 0.16f, 0.34f, 1f),
                ShieldedOutsideBlockColor = new Color(0.086804256f, 0.23667915f, 0.48427665f, 1f),
                SuperShieldedOutsideBlockColor = new Color(0.03529412f, 0.20392157f, 0.36862746f, 1f),
                InsideBlockColor = new Color(0f, 0.5884546f, 1.1353014f, 1f),
                ShieldedInsideBlockColor = new Color(0.40942162f, 0.64190876f, 1.2167859f, 1f),
                SuperShieldedInsideBlockColor = new Color(1.012825f, 1.1209937f, 1.4980391f, 1f),
                AOETextureColor = new Color(0.36039874f, 1.860437f, 1.0082682f, 0f),
                AOEFresnelColor = new Color(0f, 0.121500015f, 0.135f, 0f),
                AOEConicColor = new Color(0.03529412f, 0.30980393f, 0.29803923f, 1f),
                AOEConicEdgeColor = new Color(0.019607844f, 0.56078434f, 0.7921569f, 0f),
                SpikeLightColor = new Color(0.08627451f, 0.69803923f, 0.7254902f, 0f),
                SpikeDarkColor = new Color(0.24313726f, 0.5019608f, 0.78431374f, 1f),
                SkimmerColor = new Color(0.05490196f, 0.7529412f, 0.7137255f, 1f),
                DullCrystalColor = new Color(0f, 0f, 0f, 0.9411765f),
                BrightCrystalColor = new Color(0.05490196f, 0.50980395f, 0.7490196f, 0.9411765f),
                TrailHighlightColor = new Color(0.05490196f, 0.7529412f, 0.7137255f, 1f),
                TrailCoreColor = new Color(0f, 0.3882353f, 0.7490196f, 1f),
            };

            colorSet.RubyColors = new DomainColorSet
            {
                ShipColor1 = new Color(0.03529412f, 0f, 0.09411765f, 1f),
                ShipColor2 = new Color(0.27450982f, 0f, 0.7490196f, 1f),
                OutsideBlockColor = new Color(0.11906711f, 0.006783697f, 0.30817598f, 1f),
                ShieldedOutsideBlockColor = new Color(0.34509805f, 0f, 2.1960785f, 1f),
                SuperShieldedOutsideBlockColor = new Color(0.21176471f, 0.12156863f, 0.70980394f, 1f),
                InsideBlockColor = new Color(0.5490196f, 0f, 1.4980392f, 1f),
                ShieldedInsideBlockColor = new Color(0.20340309f, 0.03169036f, 1.2596958f, 1f),
                SuperShieldedInsideBlockColor = new Color(0.83832616f, 0.73017627f, 1.4980392f, 1f),
                AOETextureColor = new Color(4f, 0f, 3.958115f, 0f),
                AOEFresnelColor = new Color(0.060094018f, 0f, 0.16078432f, 0f),
                AOEConicColor = new Color(0.16078432f, 0.023529412f, 0.29411766f, 1f),
                AOEConicEdgeColor = new Color(0.28627452f, 0.09803922f, 0.65882355f, 0f),
                SpikeLightColor = new Color(0.78431374f, 0f, 0.7647059f, 0f),
                SpikeDarkColor = new Color(0.4117647f, 0.050980393f, 0.9529412f, 1f),
                SkimmerColor = new Color(0.21953903f, 0f, 0.5471698f, 0f),
                DullCrystalColor = new Color(0f, 0f, 0f, 0.9411765f),
                BrightCrystalColor = new Color(0.6392157f, 0.05490196f, 0.7490196f, 0.9411765f),
                TrailHighlightColor = new Color(0.7843137f, 0f, 0.7647059f, 1f),
                TrailCoreColor = new Color(0.27450982f, 0f, 0.7490196f, 1f),
            };

            colorSet.GoldColors = new DomainColorSet
            {
                ShipColor1 = new Color(1.2203892f, 0.6122575f, 0.03070152f, 1f),
                ShipColor2 = new Color(0.47058824f, 0.23529412f, 0.043137256f, 1f),
                OutsideBlockColor = new Color(0.23899364f, 0.124818474f, 0f, 1f),
                ShieldedOutsideBlockColor = new Color(1.0440251f, 0.5677132f, 0.12804069f, 1f),
                SuperShieldedOutsideBlockColor = new Color(0.59607846f, 0.28627452f, 0.003921569f, 1f),
                InsideBlockColor = new Color(1.4980392f, 0.66787237f, 0.08886676f, 1f),
                ShieldedInsideBlockColor = new Color(1.2596956f, 0.8630464f, 0.24956217f, 1f),
                SuperShieldedInsideBlockColor = new Color(1.4980395f, 1.1186504f, 0.5323221f, 1f),
                AOETextureColor = new Color(2.0590868f, 1.6091012f, 0.44200292f, 0f),
                AOEFresnelColor = new Color(0.16078432f, 0.12418869f, 0f, 0f),
                AOEConicColor = new Color(0.37254903f, 0.40392157f, 0.047058824f, 1f),
                AOEConicEdgeColor = new Color(0.6431373f, 0.32156864f, 0.03529412f, 0f),
                SpikeLightColor = new Color(0.85882354f, 0.6313726f, 0f, 0f),
                SpikeDarkColor = new Color(0.4627451f, 0.3529412f, 0f, 1f),
                SkimmerColor = new Color(0.7490196f, 0.42745098f, 0.05490196f, 0f),
                DullCrystalColor = new Color(0f, 0f, 0f, 0.9411765f),
                BrightCrystalColor = new Color(0.7490196f, 0.5238843f, 0.054901935f, 0.9411765f),
                TrailHighlightColor = new Color(1f, 0.65654284f, 0f, 1f),
                TrailCoreColor = new Color(1f, 0f, 0f, 1f),
            };

            colorSet.BlueColors = new DomainColorSet
            {
                ShipColor1 = new Color(0f, 0f, 0.48235294f, 1f),
                ShipColor2 = new Color(0f, 0f, 0.5019608f, 1f),
                OutsideBlockColor = new Color(0.023529412f, 0.023529412f, 0.34901962f, 1f),
                ShieldedOutsideBlockColor = new Color(0f, 0f, 0.54901963f, 1f),
                SuperShieldedOutsideBlockColor = new Color(0f, 0f, 0.54901963f, 1f),
                InsideBlockColor = new Color(0.46681717f, 0.53749484f, 1.2167859f, 0.9411765f),
                ShieldedInsideBlockColor = new Color(1.1131275f, 1.1268682f, 1.2596959f, 0.9411765f),
                SuperShieldedInsideBlockColor = new Color(1.3237392f, 1.3400798f, 1.4980394f, 0.9411765f),
                AOETextureColor = new Color(0.4790567f, 0.44200292f, 2.0590868f, 0f),
                AOEFresnelColor = new Color(0.0026707277f, 0f, 0.16078432f, 0f),
                AOEConicColor = new Color(0f, 0f, 0f, 0f),
                AOEConicEdgeColor = new Color(0f, 0f, 0f, 0f),
                SpikeLightColor = new Color(0.8f, 0.8117647f, 1f, 0f),
                SpikeDarkColor = new Color(0.023529412f, 0.03137255f, 0.32156864f, 1f),
                SkimmerColor = new Color(0.019607844f, 0.007843138f, 0.91764706f, 0f),
                DullCrystalColor = new Color(0f, 0.02745098f, 0.5058824f, 0.9411765f),
                BrightCrystalColor = new Color(0.07058824f, 0.05490196f, 0.7490196f, 0.9411765f),
                TrailHighlightColor = new Color(0.4f, 0.5f, 1f, 1f),
                TrailCoreColor = new Color(0f, 0f, 0.5019608f, 1f),
            };

            colorSet.EnvironmentColors = new EnvironmentColorSet
            {
                SkyColor = new Color(0f, 0f, 0.102f, 0f),
                LightColor = new Color(0f, 0f, 0f, 0f),
                DarkColor = new Color(0f, 0f, 0f, 0f),
                BrightCTA = new Color(0f, 0f, 0f, 0f),
                DarkCTA = new Color(0f, 0f, 0f, 0f),
                Danger = new Color(0.72350526f, 0.04628531f, 0.020476442f, 0f),
            };

            return colorSet;
        }

        /// <summary>
        /// The base material set, slot for slot as OriginalMaterialSet.asset authors it
        /// (see <see cref="MaterialSetAssetPath"/>). The engine's materials are data-only
        /// property stores, so each slot is a fresh material named after the .mat asset the
        /// original references; ThemeManager writes every color the port reads onto the
        /// per-domain copies. The silhouette slot is a placeholder GameObject standing in
        /// for Assets/_Graphics/Silhouettes/BlockSilhouette.prefab.
        /// </summary>
        public static SO_MaterialSet CreateBaseMaterialSet()
        {
            var set = ScriptableObject.CreateInstance<SO_MaterialSet>();
            set.name = "OriginalMaterialSet";

            Material Mat(string assetName) => new((Shader)null) { name = assetName };

            set.ShipMaterial = Mat("GreenVesselMaterial");
            set.BlockMaterial = Mat("PrismMaterial");
            set.TransparentBlockMaterial = Mat("TransparentPrismMaterial");
            set.CrystalMaterial = Mat("ActiveMassCrystalMaterial");
            set.CrystalMaterial1 = Mat("ActiveMassCrystalMaterial 1");
            set.CrystalMaterial2 = Mat("ActiveMassCrystalMaterial 2");
            set.CrystalMaterial3 = Mat("ActiveMassCrystalMaterial 3");
            set.ExplodingBlockMaterial = Mat("ExplodingBlockMaterial");
            set.ShieldedBlockMaterial = Mat("ShieldedBlockMaterial");
            set.TransparentShieldedBlockMaterial = Mat("TransparentShieldedBlockMaterial");
            set.SuperShieldedBlockMaterial = Mat("SuperShieldedBlockMaterial");
            set.TransparentSuperShieldedBlockMaterial = Mat("TransparentSuperShieldedBlockMaterial");
            set.DangerousBlockMaterial = Mat("DangerBlockMaterial");
            set.TransparentDangerousBlockMaterial = Mat("TransparentDangerBlockMaterial");
            set.BlockSilhouettePrefab = new GameObject("BlockSilhouette");
            set.AOEExplosionMaterial = Mat("RippleMaterialGreen");
            set.AOEConicExplosionMaterial = Mat("GreenConicExplosionMaterial");
            set.SpikeMaterial = Mat("GreenSpikeMaterial");
            set.SkimmerMaterial = Mat("FresnelMaterial");

            return set;
        }

        /// <summary>
        /// THE renderer's color source for a prism slab: the prism's live
        /// <see cref="MeshRenderer.sharedMaterial"/> — the material the real
        /// PrismTeamManager/PrismStateManager pipeline assigned from
        /// <see cref="ThemeManagerDataContainerSO.TeamMaterialSets"/> — read back as the
        /// block shader's <c>_BrightColor</c> (InsideBlockColor) / <c>_DarkColor</c>
        /// (OutsideBlockColor) pair. Team changes (steal) and state changes (shielded /
        /// dangerous) swap the shared material, so this follows them live. Falls back to
        /// the domain's block material for a prism whose material hasn't been assigned yet
        /// (pre-Awake) — same source, one hop earlier.
        /// </summary>
        public static (Color bright, Color dark) PrismDrawColors(Prism prism, ThemeManagerDataContainerSO theme)
        {
            var meshRenderer = prism.GetComponent<MeshRenderer>();
            var material = meshRenderer != null ? meshRenderer.sharedMaterial : null;
            material ??= theme.GetTeamBlockMaterial(prism.Domain);
            return (material.GetColor("_BrightColor"), material.GetColor("_DarkColor"));
        }

        /// <summary>
        /// The renderer's color source for a TEAM crystal station: the domain's crystal
        /// material from the theme (<c>_BrightCrystalColor</c> / <c>_DullCrystalColor</c>) —
        /// the same material the original Crystal.ChangeDomain lerps its models to, making
        /// the domain lock readable before contact (NEXT UP item 2). Uncommitted (Blue)
        /// stations keep their per-element prefab tint — the original's defaultMaterial
        /// path — which the window's element table supplies.
        /// </summary>
        public static (Color bright, Color dull) TeamCrystalDrawColors(Domains domain, ThemeManagerDataContainerSO theme)
        {
            var material = theme.GetTeamCrystalMaterial(domain, 0);
            return (material.GetColor("_BrightCrystalColor"), material.GetColor("_DullCrystalColor"));
        }
    }
}
