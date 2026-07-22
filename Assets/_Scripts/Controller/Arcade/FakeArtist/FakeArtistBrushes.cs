using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Fake Artist trail-identity palette: up to 12 players are distinguished by
    /// prism-trail look alone - 6 colors x 2 states (normal / shielded octahedron).
    ///
    /// Colors 1-4 are the real domains (Jade, Ruby, Gold, Blue - all four get generated
    /// material sets from ThemeManager). Colors 5-6 are SYNTHETIC domain values minted at
    /// mode start: <see cref="FireDomain"/> wears the danger-prism palette
    /// (EnvironmentColors.Danger) WITHOUT the danger gameplay, and <see cref="LimeDomain"/>
    /// wears the CTA palette (EnvironmentColors.BrightCTA/DarkCTA - the crystal
    /// call-to-action tint, which has no prism material until we mint one).
    ///
    /// Registering the synthetic sets into ThemeManagerDataContainerSO.TeamMaterialSets is
    /// the ONLY override that survives the whole repaint machinery (PrismTeamManager,
    /// PrismStateManager, MaterialPropertyAnimator.ValidateMaterials, ShipHelper) - every
    /// repaint site is a plain dictionary lookup with no enum validation, and
    /// NetworkVariable&lt;Domains&gt; serializes the raw value, so a server-side
    /// NetDomain.Value = (Domains)5 replicates and paints identically on every peer.
    ///
    /// SYNTHETIC-DOMAIN BLAST RADIUS (why this stays mode-local): SO_ColorSet lookups fail
    /// for values >= 5, so trail-ribbon colors must be applied manually
    /// (<see cref="TryGetTrailColors"/>), explosion VFX tints fall back, GetDomainUIColor
    /// returns gray (use <see cref="UIColor"/> in mode-owned UI), and cell density grids /
    /// fauna diets don't bucket these prisms. Never use synthetic domains in modes with
    /// Cell control or domain-aggregated scoring.
    /// </summary>
    public static class FakeArtistBrushes
    {
        public const int MaxBrushes = 12;

        /// <summary>Synthetic "fire" paint domain - the danger-prism look without the danger gameplay.</summary>
        public const Domains FireDomain = (Domains)5;

        /// <summary>Synthetic "lime" paint domain - the crystal CTA tint as a prism material.</summary>
        public const Domains LimeDomain = (Domains)6;

        // Slot order: the 4 real domains first (fully supported everywhere), then the
        // two synthetic colors; slots 6-11 repeat the colors with the shielded state.
        static readonly Domains[] ColorOrder =
        {
            Domains.Jade, Domains.Ruby, Domains.Gold, Domains.Blue, FireDomain, LimeDomain,
        };

        /// <summary>One player's trail identity.</summary>
        public readonly struct Brush
        {
            public readonly Domains PaintDomain;
            public readonly bool Shielded;

            public Brush(Domains paintDomain, bool shielded)
            {
                PaintDomain = paintDomain;
                Shielded = shielded;
            }
        }

        /// <summary>Deterministic brush for a roster slot (0-11).</summary>
        public static Brush ForSlot(int slot)
        {
            int wrapped = ((slot % MaxBrushes) + MaxBrushes) % MaxBrushes;
            return new Brush(ColorOrder[wrapped % ColorOrder.Length], wrapped >= ColorOrder.Length);
        }

        public static string ColorName(Domains paintDomain) => paintDomain switch
        {
            Domains.Jade => "Jade",
            Domains.Ruby => "Ruby",
            Domains.Gold => "Gold",
            Domains.Blue => "Blue",
            FireDomain => "Fire",
            LimeDomain => "Lime",
            _ => paintDomain.ToString(),
        };

        public static string DisplayName(Brush brush) =>
            brush.Shielded ? $"Shielded {ColorName(brush.PaintDomain)}" : ColorName(brush.PaintDomain);

        /// <summary>
        /// Flat UI color for a brush color - the shared GetDomainUIColor for real domains,
        /// the environment palette for the synthetic two (which the shared lookup grays out).
        /// </summary>
        public static Color UIColor(ThemeManagerDataContainerSO themeData, Domains paintDomain)
        {
            if (themeData == null || themeData.ColorSet == null) return Color.gray;
            if (paintDomain == FireDomain) return themeData.ColorSet.EnvironmentColors.Danger;
            if (paintDomain == LimeDomain) return themeData.ColorSet.EnvironmentColors.BrightCTA;
            return themeData.GetDomainUIColor(paintDomain);
        }

        /// <summary>
        /// Trail-ribbon (VesselTrailCustomization) colors. ShipHelper.SetShipProperties
        /// applies these itself for real domains; the synthetic two need a manual
        /// vessel.SetTrailColors call with these values on every peer.
        /// </summary>
        public static bool TryGetTrailColors(ThemeManagerDataContainerSO themeData, Domains paintDomain,
            out Color highlight, out Color core)
        {
            highlight = Color.gray;
            core = Color.gray;
            if (themeData == null || themeData.ColorSet == null) return false;

            if (paintDomain == FireDomain)
            {
                var bright = themeData.ColorSet.EnvironmentColors.Danger;
                highlight = bright;
                core = Darken(bright, 0.3f);
                return true;
            }
            if (paintDomain == LimeDomain)
            {
                highlight = themeData.ColorSet.EnvironmentColors.BrightCTA;
                core = themeData.ColorSet.EnvironmentColors.DarkCTA;
                return true;
            }

            if (themeData.ColorSet.TryGetColorSetByDomain(paintDomain, out var colorSet))
            {
                highlight = colorSet.TrailHighlightColor;
                core = colorSet.TrailCoreColor;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Idempotently mints and registers the two synthetic material sets (fire, lime)
        /// into <see cref="ThemeManagerDataContainerSO.TeamMaterialSets"/> on THIS peer.
        /// Call from the mode controller before any NetDomain is set to a synthetic value
        /// (client peers: before the replicated value's repaint lands - scene Awake works
        /// because deltas apply after spawn processing). The persistent ThemeManager only
        /// regenerates the four real domains, so stale synthetic keys from a previous
        /// session are re-minted here for the current dictionary instance.
        /// </summary>
        public static void EnsureSyntheticSetsRegistered(ThemeManagerDataContainerSO themeData)
        {
            if (themeData == null || themeData.TeamMaterialSets == null || themeData.ColorSet == null)
            {
                Debug.LogError("[FakeArtistBrushes] Theme data unavailable - synthetic brush sets not registered.");
                return;
            }

            var env = themeData.ColorSet.EnvironmentColors;

            if (!themeData.TeamMaterialSets.ContainsKey(FireDomain))
                themeData.TeamMaterialSets[FireDomain] =
                    MintMaterialSet(themeData, "FakeArtistFire", SyntheticColorSet(env.Danger, Darken(env.Danger, 0.25f)));

            if (!themeData.TeamMaterialSets.ContainsKey(LimeDomain))
                themeData.TeamMaterialSets[LimeDomain] =
                    MintMaterialSet(themeData, "FakeArtistLime", SyntheticColorSet(env.BrightCTA, env.DarkCTA));
        }

        static Color Darken(Color c, float factor)
        {
            var d = c * factor;
            d.a = c.a;
            return d;
        }

        static Color TowardWhite(Color c, float t)
        {
            var w = Color.Lerp(c, Color.white, t);
            w.a = c.a;
            return w;
        }

        /// <summary>
        /// Builds a DomainColorSet for a synthetic paint color from a bright/dark pair,
        /// deriving the shield tints the same direction the authored domains do
        /// (shielded = toward white, super-shielded = further toward white).
        /// </summary>
        static DomainColorSet SyntheticColorSet(Color bright, Color dark)
        {
            return new DomainColorSet
            {
                ShipColor1 = bright,
                ShipColor2 = dark,
                InsideBlockColor = bright,
                OutsideBlockColor = dark,
                ShieldedInsideBlockColor = TowardWhite(bright, 0.35f),
                ShieldedOutsideBlockColor = TowardWhite(dark, 0.2f),
                SuperShieldedInsideBlockColor = TowardWhite(bright, 0.65f),
                SuperShieldedOutsideBlockColor = TowardWhite(dark, 0.45f),
                AOETextureColor = bright,
                AOEFresnelColor = TowardWhite(bright, 0.4f),
                AOEConicColor = bright,
                AOEConicEdgeColor = TowardWhite(bright, 0.5f),
                SpikeLightColor = bright,
                SpikeDarkColor = dark,
                SkimmerColor = bright,
                DullCrystalColor = dark,
                BrightCrystalColor = bright,
                TrailHighlightColor = bright,
                TrailCoreColor = dark,
            };
        }

        /// <summary>
        /// Mirrors ThemeManager.GenerateDomainMaterialSet: clone every material from the
        /// base set and tint the domain-specific ones, so the synthetic sets behave
        /// identically through PrismTeamManager / PrismStateManager / ShipHelper repaints.
        /// </summary>
        static SO_MaterialSet MintMaterialSet(ThemeManagerDataContainerSO themeData, string setName, DomainColorSet colorSet)
        {
            var baseSet = themeData.BaseMaterialSet;
            var materialSet = ScriptableObject.CreateInstance<SO_MaterialSet>();
            materialSet.name = $"{setName}TeamMaterialSet";

            materialSet.ShipMaterial = new Material(baseSet.ShipMaterial);
            materialSet.BlockMaterial = new Material(baseSet.BlockMaterial);
            materialSet.TransparentBlockMaterial = new Material(baseSet.TransparentBlockMaterial);
            materialSet.CrystalMaterial = new Material(baseSet.CrystalMaterial);
            materialSet.CrystalMaterial1 = new Material(baseSet.CrystalMaterial1);
            materialSet.CrystalMaterial2 = new Material(baseSet.CrystalMaterial2);
            materialSet.CrystalMaterial3 = new Material(baseSet.CrystalMaterial3);
            materialSet.ExplodingBlockMaterial = new Material(baseSet.ExplodingBlockMaterial);
            materialSet.ShieldedBlockMaterial = new Material(baseSet.ShieldedBlockMaterial);
            materialSet.TransparentShieldedBlockMaterial = new Material(baseSet.TransparentShieldedBlockMaterial);
            materialSet.SuperShieldedBlockMaterial = new Material(baseSet.SuperShieldedBlockMaterial);
            materialSet.TransparentSuperShieldedBlockMaterial = new Material(baseSet.TransparentSuperShieldedBlockMaterial);
            materialSet.DangerousBlockMaterial = new Material(baseSet.DangerousBlockMaterial);
            materialSet.TransparentDangerousBlockMaterial = new Material(baseSet.TransparentDangerousBlockMaterial);
            materialSet.AOEExplosionMaterial = new Material(baseSet.AOEExplosionMaterial);
            materialSet.AOEConicExplosionMaterial = new Material(baseSet.AOEConicExplosionMaterial);
            materialSet.SpikeMaterial = new Material(baseSet.SpikeMaterial);
            materialSet.SkimmerMaterial = new Material(baseSet.SkimmerMaterial);
            materialSet.BlockSilhouettePrefab = baseSet.BlockSilhouettePrefab;

            materialSet.BlockMaterial.SetColor("_BrightColor", colorSet.InsideBlockColor);
            materialSet.BlockMaterial.SetColor("_DarkColor", colorSet.OutsideBlockColor);
            materialSet.TransparentBlockMaterial.SetColor("_BrightColor", colorSet.InsideBlockColor);
            materialSet.TransparentBlockMaterial.SetColor("_DarkColor", colorSet.OutsideBlockColor);

            materialSet.CrystalMaterial.SetColor("_BrightCrystalColor", colorSet.BrightCrystalColor);
            materialSet.CrystalMaterial.SetColor("_DullCrystalColor", colorSet.DullCrystalColor);
            materialSet.CrystalMaterial1.SetColor("_BrightCrystalColor", colorSet.BrightCrystalColor);
            materialSet.CrystalMaterial1.SetColor("_DullCrystalColor", colorSet.DullCrystalColor);
            materialSet.CrystalMaterial2.SetColor("_BrightCrystalColor", colorSet.BrightCrystalColor);
            materialSet.CrystalMaterial2.SetColor("_DullCrystalColor", colorSet.DullCrystalColor);
            materialSet.CrystalMaterial3.SetColor("_BrightCrystalColor", colorSet.BrightCrystalColor);
            materialSet.CrystalMaterial3.SetColor("_DullCrystalColor", colorSet.DullCrystalColor);

            materialSet.ExplodingBlockMaterial.SetColor("_BrightColor", colorSet.InsideBlockColor);
            materialSet.ExplodingBlockMaterial.SetColor("_DarkColor", colorSet.OutsideBlockColor);

            materialSet.DangerousBlockMaterial.SetColor("_BrightColor", themeData.ColorSet.EnvironmentColors.Danger);
            materialSet.DangerousBlockMaterial.SetColor("_DarkColor", colorSet.OutsideBlockColor);
            materialSet.TransparentDangerousBlockMaterial.SetColor("_BrightColor", themeData.ColorSet.EnvironmentColors.Danger);
            materialSet.TransparentDangerousBlockMaterial.SetColor("_DarkColor", colorSet.OutsideBlockColor);

            materialSet.ShieldedBlockMaterial.SetColor("_BrightColor", colorSet.ShieldedInsideBlockColor);
            materialSet.ShieldedBlockMaterial.SetColor("_DarkColor", colorSet.ShieldedOutsideBlockColor);
            materialSet.TransparentShieldedBlockMaterial.SetColor("_BrightColor", colorSet.ShieldedInsideBlockColor);
            materialSet.TransparentShieldedBlockMaterial.SetColor("_DarkColor", colorSet.ShieldedOutsideBlockColor);

            materialSet.SuperShieldedBlockMaterial.SetColor("_BrightColor", colorSet.SuperShieldedInsideBlockColor);
            materialSet.SuperShieldedBlockMaterial.SetColor("_DarkColor", colorSet.SuperShieldedOutsideBlockColor);
            materialSet.TransparentSuperShieldedBlockMaterial.SetColor("_BrightColor", colorSet.SuperShieldedInsideBlockColor);
            materialSet.TransparentSuperShieldedBlockMaterial.SetColor("_DarkColor", colorSet.SuperShieldedOutsideBlockColor);

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
        /// Per-spawn brush integrity for a freshly-laid trail prism (call from
        /// VesselPrismController.OnBlockSpawned on EVERY peer). Repairs pooled-flag
        /// leakage - PrismProperties flags persist across pool lives and Prism.Initialize
        /// re-engages from them - and enforces the brush's shielded state. A prism that
        /// already matches its brush is left untouched (no redundant repaint).
        /// </summary>
        public static void EnforceBrushOnSpawnedPrism(Prism prism, bool shieldedBrush)
        {
            if (!prism || prism.prismProperties == null) return;

            var props = prism.prismProperties;
            bool dirty = props.IsDangerous
                         || props.IsSuperShielded
                         || props.IsShielded != shieldedBrush;
            if (!dirty) return;

            PrismKinds.Clear(prism);
            if (shieldedBrush)
                prism.ActivateShield();
        }
    }
}
