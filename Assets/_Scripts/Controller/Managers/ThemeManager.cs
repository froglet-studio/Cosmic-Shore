using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Core;
using CosmicShore.ECS;
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

        /// <summary>The live manager, for the Profile's colour-set picker. Null until it wakes.</summary>
        public static ThemeManager Instance { get; private set; }

        /// <summary>Raised after a colour set is applied and every domain material is repainted (§123).</summary>
        public static event System.Action<SO_ColorSet> ColorSetApplied;

        /// <summary>
        /// The palette a surface should paint with: the live one (the player's pick) when the manager is up, else
        /// the one the surface was authored with. For the few menu views that hold a palette of their own.
        /// </summary>
        public static SO_ColorSet LiveOr(SO_ColorSet authored) =>
            Data && Data.ColorSet ? Data.ColorSet : authored;

        // The palette the container was authored with. The player's pick goes into the container's slot at run time
        // (that slot is what every reader follows), and this is put back when the manager goes, so a play session in
        // the editor never leaves the asset in memory pointing at another set.
        SO_ColorSet _authoredColorSet;

        void Awake()
        {
            Instance = this;
            _authoredColorSet = _dataContainer.ColorSet;
            // The set made for the player's colour-vision mode, if one is on; else their pick, if it is in the catalog
            // and unlocked; else the authored palette (Nocturne).
            var picked = ResolveColorSet(_dataContainer.Catalog, ColorSetPreference.Load(),
                                         AccessibilitySettings.ColorblindMode);
            if (picked) _dataContainer.ColorSet = picked;
            Equipped = _dataContainer.ColorSet;

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

            HandOffColorSet();

            // Last, so Data is never observable before the material sets above are in it.
            Data = _dataContainer;
        }

        void OnEnable() => AccessibilitySettings.OnColorblindModeChanged += OnColorblindModeChanged;
        void OnDisable() => AccessibilitySettings.OnColorblindModeChanged -= OnColorblindModeChanged;

        void OnDestroy()
        {
            if (_dataContainer && _authoredColorSet) _dataContainer.ColorSet = _authoredColorSet;
            if (Instance == this) Instance = null;
        }

        /// <summary>The catalog entry saved under <paramref name="savedId"/>, if it is there and unlocked.</summary>
        public static SO_ColorSet PickedColorSet(ColorSetCatalogSO catalog, string savedId)
        {
            if (!catalog) return null;
            var entry = catalog.Get(catalog.IndexOfId(savedId));
            return entry != null && entry.unlocked ? entry.colorSet : null;
        }

        /// <summary>
        /// The set to wear at start: the unlocked set made for <paramref name="vision"/> when a colour-vision mode is
        /// on and the catalog has one (an accessibility need outranks a look), else <see cref="PickedColorSet"/>.
        /// Null means the authored palette.
        /// </summary>
        public static SO_ColorSet ResolveColorSet(ColorSetCatalogSO catalog, string savedId, ColorblindModeSetting vision)
        {
            if (!catalog) return null;
            var made = catalog.Get(catalog.IndexOfVision(vision));
            return made != null && made.unlocked && made.colorSet ? made.colorSet : PickedColorSet(catalog, savedId);
        }

        // Settings > General > Colorblind mode: wear the set made for the mode at once. Off takes a vision set back
        // to the authored palette and leaves a set picked for its looks alone.
        void OnColorblindModeChanged(ColorblindModeSetting mode)
        {
            var catalog = _dataContainer ? _dataContainer.Catalog : null;
            if (!catalog) return;
            ColorSetCatalogSO.Entry entry;
            if (mode == ColorblindModeSetting.Off)
            {
                var worn = catalog.Get(catalog.IndexOf(Equipped));
                if (worn == null || worn.vision == ColorblindModeSetting.Off) return;
                entry = catalog.Get(catalog.IndexOf(_authoredColorSet));
            }
            else entry = catalog.Get(catalog.IndexOfVision(mode));
            if (entry == null || !entry.unlocked || !entry.colorSet || entry.colorSet == Equipped) return;
            ApplyColorSet(entry.colorSet, entry.id);
        }

        /// <summary>The set the player wears: the saved pick, or the authored palette. What a preview returns to.</summary>
        public SO_ColorSet Equipped { get; private set; }

        /// <summary>
        /// Wears <paramref name="set"/>: shows it (<see cref="PreviewColorSet"/>, a no-op when it is already showing),
        /// saves the pick under <paramref name="savedId"/>, makes it <see cref="Equipped"/> and raises
        /// <see cref="ColorSetApplied"/>. Returns false when there is nothing to apply.
        /// </summary>
        public bool ApplyColorSet(SO_ColorSet set, string savedId)
        {
            if (!PreviewColorSet(set)) return false;
            ColorSetPreference.Save(savedId);
            Equipped = set;
            // The colour-vision mode follows what is worn (a set picked in the Profile turns a mode off, a vision set
            // turns its mode on), so the two settings never disagree. Equipped is set first: the mode's change
            // handler then finds the set already worn and stops.
            var catalog = _dataContainer.Catalog;
            var worn = catalog ? catalog.Get(catalog.IndexOfId(savedId)) : null;
            var vision = worn?.vision ?? ColorblindModeSetting.Off;
            if (AccessibilitySettings.ColorblindMode != vision) AccessibilitySettings.ColorblindMode = vision;
            ColorSetApplied?.Invoke(set);
            return true;
        }

        /// <summary>
        /// Shows <paramref name="set"/> without saving it - the Profile's Colour sets tab previews the set being browsed
        /// on the real cell behind it (Rev 54 A, §126), and puts <see cref="Equipped"/> back when the player leaves
        /// without applying. Puts the set in the container's slot and repaints the four domain material sets IN PLACE,
        /// so every prism, vessel and effect drawn with them takes the new colours at once - no cell rebuild - then
        /// hands it to the statics again. Returns false when it cannot (no set, the manager not up).
        /// </summary>
        public bool PreviewColorSet(SO_ColorSet set)
        {
            if (!set || _dataContainer.TeamMaterialSets == null) return false;
            if (set == _dataContainer.ColorSet) return true;

            _dataContainer.ColorSet = set;
            var repainted = new List<Material>(64);
            foreach (var pair in _dataContainer.TeamMaterialSets)
                if (pair.Value && set.TryGetColorSetByDomain(pair.Key, out var domainColors))
                {
                    PaintDomainMaterialSet(pair.Value, domainColors, pair.Key);
                    CollectMaterials(pair.Value, repainted);
                }
            HandOffColorSet();
            // What copied its colours out of a material when it spawned does not see a repaint in place: the prism
            // render entities and the crystals' tint blocks (§124). Vessels and settled MeshRenderer prisms draw the
            // shared material itself and change with it.
            PrismRenderService.RestampColors(repainted);
            Crystal.RetintAll();
            return true;
        }

        static void CollectMaterials(SO_MaterialSet set, List<Material> into)
        {
            foreach (var m in new[]
                     {
                         set.BlockMaterial, set.TransparentBlockMaterial, set.ShieldedBlockMaterial,
                         set.TransparentShieldedBlockMaterial, set.SuperShieldedBlockMaterial,
                         set.TransparentSuperShieldedBlockMaterial, set.DangerousBlockMaterial,
                         set.TransparentDangerousBlockMaterial, set.ExplodingBlockMaterial, set.CrystalMaterial,
                         set.CrystalMaterial1, set.CrystalMaterial2, set.CrystalMaterial3, set.CrystalMaterial4,
                     })
                if (m) into.Add(m);
        }

        void HandOffColorSet()
        {
            // Hand the ColorSet to the static game-toast API so it colors domain names
            // from the same single source the vessels and prisms use (R5).
            GameToastAPI.ColorSet = _dataContainer.ColorSet;

            // Same hand-off, same reason: PrismLit is a static that resolves a light's DOMAIN
            // tint and cannot be injected. Until this line runs a light falls back to white, and
            // this manager is a Bootstrap DI singleton, so nothing that can fire has woken yet.
            PrismLit.ColorSet = _dataContainer.ColorSet;
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

            PaintDomainMaterialSet(materialSet, colorSet, domain);
            return materialSet;
        }

        /// <summary>
        /// Paints one domain's material set from its colours. Run once on the fresh clones at Awake and again on the
        /// SAME clones when a colour set is applied (<see cref="ApplyColorSet"/>), which is what lets live prisms
        /// change colour without being rebuilt.
        /// </summary>
        void PaintDomainMaterialSet(SO_MaterialSet materialSet, DomainColorSet colorSet, Domains domain)
        {
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
