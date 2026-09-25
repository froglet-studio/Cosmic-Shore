using UnityEngine;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;


namespace CosmicShore.Gameplay
{

    public class ThemeManager : MonoBehaviour
    {
        [SerializeField] ThemeManagerDataContainerSO _dataContainer;

        /// <summary>
        /// The live theme, for the handful of statics that cannot be injected — the same reason
        /// <c>PrismLit.ColorSet</c> and <c>GameToastAPI.ColorSet</c> are handed off below, one
        /// level up: those need the palette, this needs the whole container (the per-domain
        /// MATERIAL sets are built here at Awake and exist nowhere on disk). Read-only to everyone
        /// else; null until this manager wakes, and it is a Bootstrap DI singleton, so nothing
        /// that can legitimately ask has woken yet.
        /// </summary>
        public static ThemeManagerDataContainerSO Data { get; private set; }

        void Awake()
        {
            var GreenTeamMaterialSet = GenerateDomainMaterialSet(_dataContainer.ColorSet.JadeColors, Domains.Jade, "Green");
            var RedTeamMaterialSet = GenerateDomainMaterialSet(_dataContainer.ColorSet.RubyColors, Domains.Ruby, "Red");
            var GoldTeamMaterialSet = GenerateDomainMaterialSet(_dataContainer.ColorSet.GoldColors, Domains.Gold, "Gold");
            var BlueTeamMaterialSet = GenerateDomainMaterialSet(_dataContainer.ColorSet.BlueColors, Domains.Blue, "Blue");

            _dataContainer.TeamMaterialSets = new() {
                { Domains.Jade, GreenTeamMaterialSet },
                { Domains.Ruby,   RedTeamMaterialSet },
                { Domains.Gold,  GoldTeamMaterialSet },
                { Domains.Blue,  BlueTeamMaterialSet },
            };

            // Hand the ColorSet to the static game-toast API so it colors domain names
            // from the same single source the vessels and prisms use (R5).
            GameToastAPI.ColorSet = _dataContainer.ColorSet;

            // Same hand-off, same reason: PrismLit is a static that resolves a light's DOMAIN
            // tint and cannot be injected. Until this line runs a light falls back to white, and
            // this manager is a Bootstrap DI singleton, so nothing that can fire has woken yet.
            PrismLit.ColorSet = _dataContainer.ColorSet;

            // Last, so Data is never observable before the material sets above are in it.
            Data = _dataContainer;
        }

        SO_MaterialSet GenerateDomainMaterialSet(DomainColorSet colorSet, Domains domain, string domainName)
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
                           colorSet, domain, PrismKind.Plain);
            PaintPrismTier(materialSet.DangerousBlockMaterial, materialSet.TransparentDangerousBlockMaterial,
                           colorSet, domain, PrismKind.Danger);
            PaintPrismTier(materialSet.ShieldedBlockMaterial, materialSet.TransparentShieldedBlockMaterial,
                           colorSet, domain, PrismKind.Shielded);
            PaintPrismTier(materialSet.SuperShieldedBlockMaterial, materialSet.TransparentSuperShieldedBlockMaterial,
                           colorSet, domain, PrismKind.SuperShielded);

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
        /// <see cref="SO_ColorSet.GetPrismKindColors"/> composition, and stamps the pair with the
        /// DOMAIN it belongs to.
        ///
        /// <c>_PrismLitDomain</c> is how the LIT fundamental's domain gate knows whose mass a
        /// prism is (<see cref="PrismLit"/>, <c>PrismDestructionSight.hlsl</c>). It lives on the
        /// MATERIAL because these clones already exist one per domain — so a prism's material IS
        /// its domain, a stolen prism carries its new one the instant the swap lands, and the gate
        /// costs no per-instance override and nothing per frame. Anything drawn with a material
        /// nobody stamps (the pooled debris material, a tool-scene prism) reads 0, which the
        /// shader treats as "this has no domain" and no gated light reaches.
        /// </summary>
        void PaintPrismTier(Material opaque, Material transparent, DomainColorSet colorSet,
                            Domains domain, PrismKind kind)
        {
            _dataContainer.ColorSet.GetPrismKindColors(colorSet, kind, out var bright, out var dark);

            opaque.SetColor("_BrightColor", bright);
            opaque.SetColor("_DarkColor", dark);
            opaque.SetFloat(PrismLitDomainId, (int)domain);
            transparent.SetColor("_BrightColor", bright);
            transparent.SetColor("_DarkColor", dark);
            transparent.SetFloat(PrismLitDomainId, (int)domain);
        }

        static readonly int PrismLitDomainId = Shader.PropertyToID("_PrismLitDomain");
    }
}
