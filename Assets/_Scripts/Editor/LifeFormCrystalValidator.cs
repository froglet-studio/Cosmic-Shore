using CosmicShore.Gameplay;
using UnityEditor;
using UnityEngine;

namespace CosmicShore.Editor
{
    /// <summary>
    /// Validates the lifeform invariant at author time: every flora / fauna prefab must carry
    /// exactly one elemental crystal (Charge / Mass / Space / Time) - the powerup it drops on
    /// death. The runtime guard (<see cref="LifeFormCrystal"/>) fixes/provisions violations at
    /// play time and logs them; this surfaces the same prefabs in the editor so they get fixed
    /// properly. Run via Tools > Cosmic Shore > Validate Lifeform Crystals.
    /// </summary>
    public static class LifeFormCrystalValidator
    {
        [MenuItem("Tools/Cosmic Shore/Validate Lifeform Crystals")]
        public static void Validate()
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
