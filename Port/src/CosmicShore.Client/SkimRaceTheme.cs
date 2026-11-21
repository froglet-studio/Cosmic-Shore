using System;
using System.IO;
using System.Linq;
using CosmicShore.Content;
using CosmicShore.Content.Serialization;
using CosmicShore.Content.Yaml;
using CosmicShore.Data;
using CosmicShore.Engine;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;

namespace CosmicShore.Client
{
    /// <summary>
    /// The race's authored theme data (rung 5) — client-side factory, not a port: it
    /// reads the Unity project's wired theme assets into the ported SO types so the
    /// REAL ThemeManager pipeline (the single writer of
    /// <see cref="ThemeManagerDataContainerSO.TeamMaterialSets"/> and of
    /// <c>GameFeedAPI.ColorSet</c>) runs on the game's actual palette.
    ///
    /// Sources (the assets ThemeManagerDataContainer.asset references — see
    /// <see cref="ContainerAssetPath"/> / <see cref="MaterialSetAssetPath"/>):
    ///   • ColorSet  = whichever SO_ColorSet the container's <c>ColorSet</c> slot names, resolved by
    ///     guid through the .meta files (<see cref="ResolveColorSetAssetPath"/>): NocturneColorSetSO
    ///     since 2026-11-03 (Docs/PALETTE.md §1), so a palette switch in Unity reaches the port
    ///     with no edit here. Until then this file carried a hand transcription of the original
    ///     palette (now PhoticColorSetSO), which went stale the day the default moved.
    ///   • Materials = OriginalMaterialSet.asset (guid 2cc7d7c117bfd194eb0377c17b230c14)
    /// Every color is the asset's exact serialized value (HDR intensities &gt; 1 preserved —
    /// the renderer tone-maps). The base materials are the engine's data-only property stores
    /// named after the .mat files the original set references; their domain-facing color
    /// properties are written by ThemeManager.GenerateDomainMaterialSet, which is the only
    /// part of them the port reads.
    /// </summary>
    public static class SkimRaceTheme
    {
        public const string MaterialSetAssetPath = "Assets/_SO_Assets/MaterialSets/OriginalMaterialSet.asset";
        public const string ContainerAssetPath = "Assets/_SO_Assets/ThemeManagerDataContainer.asset";

        /// <summary>
        /// The wired container, as ThemeManagerDataContainer.asset authors it: the palette its
        /// ColorSet slot names + the OriginalMaterialSet base materials.
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

        /// <summary>
        /// The live palette: the SO_ColorSet that ThemeManagerDataContainer.asset's <c>ColorSet</c>
        /// slot names, read field for field from its YAML. <paramref name="projectRoot"/> defaults
        /// to the Unity project this binary sits in (<see cref="AssetDatabase.FindProjectRoot"/>).
        /// Throws when the project, the container or the named palette cannot be found: a silent
        /// fallback is how the port kept painting the retired palette.
        /// </summary>
        public static SO_ColorSet CreateColorSet(string projectRoot = null)
        {
            projectRoot ??= AssetDatabase.FindProjectRoot()
                ?? throw new DirectoryNotFoundException("No Unity project (Assets/ + ProjectSettings/) above this binary; set COSMIC_SHORE_PROJECT.");
            string path = ResolveColorSetAssetPath(projectRoot, out string guid);

            var file = new AssetFile(path, guid, UnityYaml.ParseDocuments(File.ReadAllText(path)));
            var body = file.Documents.FirstOrDefault(d => d.Body?.Has("JadeColors") == true)?.Body
                ?? throw new InvalidDataException($"{path} (guid {guid}) is not an SO_ColorSet: it has no JadeColors.");

            var colorSet = ScriptableObject.CreateInstance<SO_ColorSet>();
            colorSet.name = Path.GetFileNameWithoutExtension(path);
            new SerializedReader(NoReferences.Instance).ReadInto(colorSet, body, file);
            return colorSet;
        }

        /// <summary>
        /// The full path of the palette ThemeManagerDataContainer.asset's <c>ColorSet</c> slot names,
        /// found the way Unity finds it: by the guid in a .meta file (the palette folder first, then
        /// all of Assets/). Throws, naming the guid, when no asset carries it.
        /// </summary>
        public static string ResolveColorSetAssetPath(string projectRoot, out string guid)
        {
            string containerPath = Path.Combine(projectRoot, ContainerAssetPath);
            if (!File.Exists(containerPath))
                throw new FileNotFoundException($"The theme container is missing: {containerPath}");
            var slot = UnityYaml.ParseDocuments(File.ReadAllText(containerPath))
                .Select(d => d.Body?["ColorSet"])
                .FirstOrDefault(n => n != null)
                ?? throw new InvalidDataException($"{ContainerAssetPath} has no ColorSet slot.");
            guid = ObjRef.From(slot).Guid;
            if (string.IsNullOrEmpty(guid))
                throw new InvalidDataException($"{ContainerAssetPath}'s ColorSet slot is empty.");

            string assets = Path.Combine(projectRoot, "Assets");
            foreach (var root in new[] { Path.Combine(assets, "_SO_Assets", "Color Palettes"), assets })
            {
                if (!Directory.Exists(root)) continue;
                foreach (var meta in Directory.EnumerateFiles(root, "*.asset.meta", SearchOption.AllDirectories))
                    if (MetaGuid(meta) == guid) return meta.Substring(0, meta.Length - ".meta".Length);
            }
            throw new InvalidDataException(
                $"{ContainerAssetPath}'s ColorSet names guid {guid}, which no asset under {assets} carries.");
        }

        static string MetaGuid(string metaPath)
        {
            foreach (var line in File.ReadLines(metaPath).Take(4))
                if (line.StartsWith("guid: ", StringComparison.Ordinal)) return line.Substring(6).Trim();
            return null;
        }

        /// <summary>A palette holds colours only; any object reference in it stays null.</summary>
        sealed class NoReferences : IReferenceResolver
        {
            public static readonly NoReferences Instance = new();
            public object Resolve(ObjRef reference, Type fieldType, AssetFile origin) => null;
            public void OnUnityEvent(object unityEvent, YNode persistentCalls, AssetFile origin) { }
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
