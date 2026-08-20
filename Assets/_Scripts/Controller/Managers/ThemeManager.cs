using UnityEngine;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;


namespace CosmicShore.Gameplay
{

    public class ThemeManager : MonoBehaviour
    {
        [SerializeField] ThemeManagerDataContainerSO _dataContainer;

        void Awake()
        {
            var GreenTeamMaterialSet = GenerateDomainMaterialSet(_dataContainer.ColorSet.JadeColors, "Green");
            var RedTeamMaterialSet = GenerateDomainMaterialSet(_dataContainer.ColorSet.RubyColors, "Red");
            var GoldTeamMaterialSet = GenerateDomainMaterialSet(_dataContainer.ColorSet.GoldColors, "Gold");
            var BlueTeamMaterialSet = GenerateDomainMaterialSet(_dataContainer.ColorSet.BlueColors, "Blue");

            _dataContainer.TeamMaterialSets = new() {
                { Domains.Jade, GreenTeamMaterialSet },
                { Domains.Ruby,   RedTeamMaterialSet },
                { Domains.Gold,  GoldTeamMaterialSet },
                { Domains.Blue,  BlueTeamMaterialSet },
            };

            // Hand the ColorSet to the static game-toast API so it colors domain names
            // from the same single source the vessels and prisms use (R5).
            GameToastAPI.ColorSet = _dataContainer.ColorSet;
        }

        SO_MaterialSet GenerateDomainMaterialSet(DomainColorSet colorSet, string domainName)
        {
            SO_MaterialSet materialSet = ScriptableObject.CreateInstance<SO_MaterialSet>();
            materialSet.name = $"{domainName}TeamMaterialSet";

            // Copy all materials from the base set
            materialSet.ShipMaterial = new Material(_dataContainer.BaseMaterialSet.ShipMaterial);
            materialSet.BlockMaterial = new Material(_dataContainer.BaseMaterialSet.BlockMaterial);
            materialSet.TransparentBlockMaterial = new Material(_dataContainer.BaseMaterialSet.TransparentBlockMaterial);
            materialSet.CrystalMaterial = new Material(_dataContainer.BaseMaterialSet.CrystalMaterial);
            materialSet.CrystalMaterial1 = new Material(_dataContainer.BaseMaterialSet.CrystalMaterial1);
            materialSet.CrystalMaterial2 = new Material(_dataContainer.BaseMaterialSet.CrystalMaterial2);
            materialSet.CrystalMaterial3 = new Material(_dataContainer.BaseMaterialSet.CrystalMaterial3);
            materialSet.ExplodingBlockMaterial = new Material(_dataContainer.BaseMaterialSet.ExplodingBlockMaterial);
            materialSet.ShieldedBlockMaterial = new Material(_dataContainer.BaseMaterialSet.ShieldedBlockMaterial);
            materialSet.TransparentShieldedBlockMaterial = new Material(_dataContainer.BaseMaterialSet.TransparentShieldedBlockMaterial);
            materialSet.SuperShieldedBlockMaterial = new Material(_dataContainer.BaseMaterialSet.SuperShieldedBlockMaterial);
            materialSet.TransparentSuperShieldedBlockMaterial = new Material(_dataContainer.BaseMaterialSet.TransparentSuperShieldedBlockMaterial);
            materialSet.DangerousBlockMaterial = new Material(_dataContainer.BaseMaterialSet.DangerousBlockMaterial);
            materialSet.TransparentDangerousBlockMaterial = new Material(_dataContainer.BaseMaterialSet.TransparentDangerousBlockMaterial);
            materialSet.AOEExplosionMaterial = new Material(_dataContainer.BaseMaterialSet.AOEExplosionMaterial);
            materialSet.AOEConicExplosionMaterial = new Material(_dataContainer.BaseMaterialSet.AOEConicExplosionMaterial);
            materialSet.SpikeMaterial = new Material(_dataContainer.BaseMaterialSet.SpikeMaterial);
            materialSet.SkimmerMaterial = new Material(_dataContainer.BaseMaterialSet.SkimmerMaterial);

            // Set colors for materials that use domain-specific colors.
            //
            // The four prism TIERS are painted from SO_ColorSet.GetPrismKindColors - the single
            // definition of "what is a prism of this kind wearing". PrismFactory tints the death
            // debris from the same method, so a prism's debris can never disagree with the prism
            // (a danger prism exploding into plain-domain-coloured debris was exactly that
            // disagreement). Do not re-inline a tier's colour pair here.
            PaintPrismTier(materialSet.BlockMaterial, materialSet.TransparentBlockMaterial,
                           colorSet, PrismKind.Plain);
            PaintPrismTier(materialSet.DangerousBlockMaterial, materialSet.TransparentDangerousBlockMaterial,
                           colorSet, PrismKind.Danger);
            PaintPrismTier(materialSet.ShieldedBlockMaterial, materialSet.TransparentShieldedBlockMaterial,
                           colorSet, PrismKind.Shielded);
            PaintPrismTier(materialSet.SuperShieldedBlockMaterial, materialSet.TransparentSuperShieldedBlockMaterial,
                           colorSet, PrismKind.SuperShielded);

            materialSet.CrystalMaterial.SetColor("_BrightCrystalColor", colorSet.BrightCrystalColor);
            materialSet.CrystalMaterial.SetColor("_DullCrystalColor", colorSet.DullCrystalColor);
            materialSet.CrystalMaterial1.SetColor("_BrightCrystalColor", colorSet.BrightCrystalColor);
            materialSet.CrystalMaterial1.SetColor("_DullCrystalColor", colorSet.DullCrystalColor);
            materialSet.CrystalMaterial2.SetColor("_BrightCrystalColor", colorSet.BrightCrystalColor);
            materialSet.CrystalMaterial2.SetColor("_DullCrystalColor", colorSet.DullCrystalColor);
            materialSet.CrystalMaterial3.SetColor("_BrightCrystalColor", colorSet.BrightCrystalColor);
            materialSet.CrystalMaterial3.SetColor("_DullCrystalColor", colorSet.DullCrystalColor);
            
            // The pooled debris prefab's own shared material is the one the batched debris path
            // actually draws with (PrismDebris reads mesh/material off it) and its colours arrive
            // as PER-ENTITY overrides keyed on the dying prism's kind - so this per-domain copy is
            // never consumed. Kept painted at the PLAIN tier for parity with the other materials.
            materialSet.ExplodingBlockMaterial.SetColor("_BrightColor", colorSet.InsideBlockColor);
            materialSet.ExplodingBlockMaterial.SetColor("_DarkColor", colorSet.OutsideBlockColor);

            materialSet.ShipMaterial.SetColor("_Color1", colorSet.ShipColor1);
            materialSet.ShipMaterial.SetColor("_Color2", colorSet.ShipColor2);

            materialSet.AOEExplosionMaterial.SetColor("_TextureColor", colorSet.AOETextureColor);
            materialSet.AOEExplosionMaterial.SetColor("_FresnelColor", colorSet.AOEFresnelColor);

            materialSet.AOEConicExplosionMaterial.SetColor("_Color", colorSet.AOEConicColor);
            materialSet.AOEConicExplosionMaterial.SetColor("_EdgeColor", colorSet.AOEConicEdgeColor);

            materialSet.SpikeMaterial.SetColor("_LightColor", colorSet.SpikeLightColor);
            materialSet.SpikeMaterial.SetColor("_DarkColor", colorSet.SpikeDarkColor);

            materialSet.SkimmerMaterial.SetColor("_Color", colorSet.SkimmerColor);

            return materialSet;
        }

        /// <summary>
        /// Paints one prism tier's opaque + transparent material pair from the shared
        /// <see cref="SO_ColorSet.GetPrismKindColors"/> composition.
        /// </summary>
        void PaintPrismTier(Material opaque, Material transparent, DomainColorSet colorSet, PrismKind kind)
        {
            _dataContainer.ColorSet.GetPrismKindColors(colorSet, kind, out var bright, out var dark);

            opaque.SetColor("_BrightColor", bright);
            opaque.SetColor("_DarkColor", dark);
            transparent.SetColor("_BrightColor", bright);
            transparent.SetColor("_DarkColor", dark);
        }
    }
}
