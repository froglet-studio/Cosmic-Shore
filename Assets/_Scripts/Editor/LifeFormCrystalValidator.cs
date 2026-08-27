using System.Collections.Generic;
using System.Linq;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using UnityEditor;
using UnityEngine;
using CosmicShore.Editor.Froglet;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Validates the lifeform invariant at author time, in two halves.
    ///
    /// <para><b>1. Every flora / fauna prefab carries exactly one elemental crystal</b>
    /// (Charge / Mass / Space / Time) - the powerup it drops on death. The runtime guard
    /// (<see cref="LifeFormCrystal"/>) fixes/provisions violations at play time and logs them;
    /// this surfaces the same prefabs in the editor so they get fixed properly.</para>
    ///
    /// <para><b>2. Every lifeform config states how big that crystal is.</b> Since
    /// Docs/ECOSYSTEM.md §40 a heart's size is a property of the LIFEFORM - authored per
    /// element in that species' own variant tuning - not of a retired level curve. A config
    /// that authors none silently takes <see cref="LifeFormCrystal.DefaultHeartWorldScale"/>,
    /// which is the floor under an unsized config rather than a size anyone chose for that
    /// species. Nothing logs at runtime, nothing looks wrong on the asset, and the species
    /// just renders (and pays) the same heart as every other unsized one - heart world scale
    /// is read AS GAMEPLAY by the collect reward and the live domain fauna buff.</para>
    ///
    /// Read-only. Run via FrogletTools ▸ Validation ▸ Validate Lifeform Crystals.
    /// </summary>
    public static class LifeFormCrystalValidator
    {
        [MenuItem("FrogletTools/Validation/Validate Lifeform Crystals")]
        [FrogletTool(FrogletToolCategory.Validation, Importance = 4,
            Description = "Every lifeform must drop exactly one elemental crystal, at its own authored size.")]
        public static void Validate()
        {
            ValidatePrefabCrystals();
            ValidateAuthoredHeartSizes();
        }

        // ── 1. one elemental crystal per lifeform prefab ──────────────────────

        static void ValidatePrefabCrystals()
        {
            int checkedCount = 0, issues = 0;

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (!go) continue;

                // A "lifeform" prefab carries a LifeForm (flora) or a LightFauna (creature fauna).
                bool isLifeForm = go.GetComponentInChildren<LifeForm>(true) ||
                                  go.GetComponentInChildren<LightFauna>(true);
                if (!isLifeForm) continue;

                checkedCount++;
                var crystals = go.GetComponentsInChildren<Crystal>(true);
                if (crystals.Length == 0)
                {
                    Debug.LogWarning($"[LifeFormCrystal] {path}: lifeform has NO crystal - must carry one " +
                        "elemental crystal (Charge/Mass/Space/Time) to drop as a powerup on death.", go);
                    issues++;
                    continue;
                }

                if (!crystals[0].crystalProperties.IsElemental)
                {
                    Debug.LogWarning($"[LifeFormCrystal] {path}: crystal element is " +
                        $"'{crystals[0].crystalProperties.Element}' - must be one of Charge/Mass/Space/Time.", go);
                    issues++;
                }

                if (crystals.Length > 1)
                {
                    Debug.LogWarning($"[LifeFormCrystal] {path}: lifeform has {crystals.Length} crystals - " +
                        "a lifeform should carry exactly one elemental crystal.", go);
                    issues++;
                }
            }

            if (issues == 0)
                Debug.Log($"[LifeFormCrystal] OK - all {checkedCount} lifeform prefab(s) carry exactly one elemental crystal.");
            else
                Debug.LogWarning($"[LifeFormCrystal] {issues} issue(s) across {checkedCount} lifeform prefab(s) - see warnings above.");
        }

        // ── 2. every config states its own heart size ─────────────────────────

        static void ValidateAuthoredHeartSizes()
        {
            int checkedCount = 0, issues = 0;

            foreach (var config in LoadAll<FaunaConfigurationSO>())
            {
                // A config with no prefab spawns nothing, so it has no heart to size.
                if (!config.FaunaPrefab) continue;

                // A config whose identity comes from a PALETTE never reads its own variant
                // block - RollVariant swaps in the sibling's Element AND Variant - so the
                // heart is the sibling's to author, and the siblings are configs in their
                // own right and get checked here too. An EMPTY palette rolls the element
                // alone and keeps THIS config's Variant, so it is not excluded.
                if (config.SpreadElements && config.ElementPalette != null &&
                    config.ElementPalette.Any(s => s && s.Element != Element.None))
                    continue;

                checkedCount++;
                issues += ReportHeart(config, config.Variant?.Enabled ?? false,
                                      config.Variant?.HeartWorldScale ?? 0f, "creature");
            }

            foreach (var config in LoadAll<FloraConfigurationSO>())
            {
                if (!config.FloraPrefab) continue;

                if (config.SpreadElements && config.ElementPalette != null &&
                    config.ElementPalette.Any(s => s && s.Element != Element.None))
                    continue;

                checkedCount++;
                issues += ReportHeart(config, config.Variant?.Enabled ?? false,
                                      config.Variant?.HeartWorldScale ?? 0f, "plant");
            }

            if (issues == 0)
                Debug.Log($"[LifeFormCrystal] OK - all {checkedCount} lifeform config(s) author their own heart size.");
            else
                Debug.LogWarning($"[LifeFormCrystal] {issues} config(s) of {checkedCount} will render the " +
                    $"platform default heart ({LifeFormCrystal.DefaultHeartWorldScale} world scale) - see warnings above. " +
                    "Author the size with Tools/Build/author_lifeform_heart_sizes.py --write.");
        }

        /// <summary>One finding per config whose authored heart will not be read.</summary>
        static int ReportHeart(UnityEngine.Object config, bool variantEnabled,
            float heartWorldScale, string noun)
        {
            var path = AssetDatabase.GetAssetPath(config);

            if (!variantEnabled)
            {
                string tail = heartWorldScale > 0f
                    ? $"authors HeartWorldScale {heartWorldScale} inside a DISABLED Variant block, so the " +
                      "block is never applied (CellLifeSpawnerBase and Fauna.AssignLineage both gate on " +
                      "Variant.Enabled) and the heart"
                    : "has no Variant tuning enabled, so its heart";
                Debug.LogWarning($"[LifeFormCrystal] {path}: {tail} falls back to the platform default " +
                    $"({LifeFormCrystal.DefaultHeartWorldScale} world scale) instead of a size chosen for this " +
                    $"{noun}. A heart is a property of the lifeform (Docs/ECOSYSTEM.md §40).", config);
                return 1;
            }

            if (heartWorldScale <= 0f)
            {
                Debug.LogWarning($"[LifeFormCrystal] {path}: Variant authors no HeartWorldScale (0 is the " +
                    $"'not authored' sentinel), so this {noun}'s heart falls back to the platform default " +
                    $"({LifeFormCrystal.DefaultHeartWorldScale} world scale). Heart world scale is read as " +
                    "GAMEPLAY - the collect reward and the live domain fauna buff both size off it - so an " +
                    "unsized species pays the same as every other unsized species.", config);
                return 1;
            }

            return 0;
        }

        static IEnumerable<T> LoadAll<T>() where T : UnityEngine.Object =>
            AssetDatabase.FindAssets($"t:{typeof(T).Name}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(asset => asset);
    }
}
