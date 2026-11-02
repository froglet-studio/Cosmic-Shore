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

        static Material CloneOrNull(Material material) => material ? new Material(material) : null;

        SO_MaterialSet GenerateDomainMaterialSet(DomainColorSet colorSet, Domains domain, string domainName)
        {
            SO_MaterialSet materialSet = ScriptableObject.CreateInstance<SO_MaterialSet>();
            materialSet.name = $"{domainName}TeamMaterialSet";

            // Copy all materials from the base set
            materialSet.ShipMaterial = new Material(_dataContainer.BaseMaterialSet.ShipMaterial);
            materialSet.BlockMaterial = new Material(_dataContainer.BaseMaterialSet.BlockMaterial);
            materialSet.TransparentBlockMaterial = new Material(_dataContainer.BaseMaterialSet.TransparentBlockMaterial);
            // Crystal slots are optional: the base set authors one per omni crystal model (body, depth prisms) and
            // leaves the rest EMPTY (the retired tone/rim slots were emptied 2026-10-13 - cloning them made nine
            // material instances per domain set that no crystal drew). An empty slot must not throw here:
            // new Material(null) would take down every domain's whole material set with it.
            materialSet.CrystalMaterial = CloneOrNull(_dataContainer.BaseMaterialSet.CrystalMaterial);
            materialSet.CrystalMaterial1 = CloneOrNull(_dataContainer.BaseMaterialSet.CrystalMaterial1);
            materialSet.CrystalMaterial2 = CloneOrNull(_dataContainer.BaseMaterialSet.CrystalMaterial2);
            materialSet.CrystalMaterial3 = CloneOrNull(_dataContainer.BaseMaterialSet.CrystalMaterial3);
            materialSet.CrystalMaterial4 = CloneOrNull(_dataContainer.BaseMaterialSet.CrystalMaterial4);
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

            // The omni crystal's body and Shepard triangles are on the Spread Fresnel shader family,
            // which names its pair _BrightColor (silhouette) / _DarkColor (facing). The domain crystal
            // pair maps onto it one-to-one - Dull is authored black on Jade/Ruby/Gold, exactly the
            // lime body's near-black face - so a team omni is the lime omni in its domain's colour.
            // SetColor on a material without the property is a no-op, so the ShepardGraph-era
            // materials, which name theirs _Bright/_DullCrystalColor, take the first pair instead.
            foreach (var crystal in new[] { materialSet.CrystalMaterial, materialSet.CrystalMaterial1,
                                            materialSet.CrystalMaterial2, materialSet.CrystalMaterial3,
                                            materialSet.CrystalMaterial4 })
            {
                if (!crystal) continue;
                crystal.SetColor("_BrightCrystalColor", colorSet.BrightCrystalColor);
                crystal.SetColor("_DullCrystalColor", colorSet.DullCrystalColor);
                crystal.SetColor("_BrightColor", colorSet.BrightCrystalColor);
                crystal.SetColor("_DarkColor", colorSet.DullCrystalColor);
                // The free omni body wears each element's shapes in that element's shade of lime (Docs/PALETTE.md
                // §2.12); a team omni is the whole crystal in its domain's colour, so its clone turns the shades off.
                crystal.SetFloat("_ElementShades", 0f);
            }
            
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
