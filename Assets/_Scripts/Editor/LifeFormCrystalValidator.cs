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
    /// <para>Every heart is the one elemental crystal size
    /// (<see cref="CosmicShore.ScriptableObjects.ElementalCrystalSetSO.CrystalWorldScale"/>), so
    /// there is no per-config heart size left to check (that check was retired 2026-10-26).</para>
    ///
    /// Read-only. Run via FrogletTools ▸ Ecology ▸ Validate Lifeform Crystals.
    /// </summary>
    public static class LifeFormCrystalValidator
    {
        [MenuItem("FrogletTools/Ecology/Validate Lifeform Crystals", false, 142)]
        [FrogletTool(FrogletToolCategory.Ecology, Importance = 4,
            Description = "Every lifeform must drop exactly one elemental crystal.")]
        public static void Validate()
        {
            ValidatePrefabCrystals();
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
    }
}
