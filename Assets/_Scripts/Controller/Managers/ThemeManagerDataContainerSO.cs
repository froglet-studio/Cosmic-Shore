using CosmicShore.ScriptableObjects;
using System.Collections.Generic;
using UnityEngine;
using CosmicShore.Utility;
using CosmicShore.Data;
namespace CosmicShore.Gameplay
{
    [CreateAssetMenu(fileName = "ThemeManagerDataContainer", menuName = "ScriptableObjects/DataContainers/ThemeManagerDataContainerSO")]
    public class ThemeManagerDataContainerSO : ScriptableObject
    {
        public SO_MaterialSet BaseMaterialSet;
        public SO_ColorSet ColorSet;

        public Dictionary<Domains, SO_MaterialSet> TeamMaterialSets { get; set; }

        /// <summary>
        /// Null-safe accessor for the single representative domain UI color
        /// (see <see cref="SO_ColorSet.GetDomainUIColor"/>) - the one source every UI
        /// surface uses, matching vessels and prisms. Neutral gray if no ColorSet is wired.
        /// </summary>
        public Color GetDomainUIColor(Domains domain) =>
            ColorSet != null ? ColorSet.GetDomainUIColor(domain) : Color.gray;

        /// <summary>
        /// Null-safe accessor for the translucent per-domain UI accent
        /// (see <see cref="SO_ColorSet.GetDomainUIAccentColor"/>) used by the Maelstrom cards and
        /// Connecting-panel rank. Neutral gray if no ColorSet is wired.
        /// </summary>
        public Color GetDomainUIAccentColor(Domains domain) =>
            ColorSet != null ? ColorSet.GetDomainUIAccentColor(domain) : Color.gray;

        /// <summary>
        /// Null-safe accessor for the DANGER tier at signal strength
        /// (see <see cref="SO_ColorSet.GetDangerSignalColor"/>), for a UI surface that has to say
        /// "this is danger mass". Returns alpha 0 when no ColorSet is wired OR when the palette
        /// authors no danger colour, so a caller keeps whatever it already had rather than painting
        /// something black - the same contract the accessor itself has.
        /// </summary>
        public Color GetDangerSignalColor() =>
            ColorSet != null ? ColorSet.GetDangerSignalColor() : new Color(0f, 0f, 0f, 0f);

        /// <summary>
        /// Null-safe accessor for the domain's SHIELDED base face at signal strength
        /// (see <see cref="SO_ColorSet.GetShieldedSignalColor"/>), for a UI surface that has to say
        /// "this is shielded mass, in this domain". Alpha 0 when no ColorSet is wired OR when the
        /// domain authors no shielded base, so a caller keeps whatever it already had.
        /// </summary>
        public Color GetShieldedSignalColor(Domains domain) =>
            ColorSet != null ? ColorSet.GetShieldedSignalColor(domain) : new Color(0f, 0f, 0f, 0f);

        public void SetBackgroundColor(Camera mainCamera)
        {
            if (mainCamera == null)
            {
                mainCamera = Camera.main;
                if (mainCamera == null)
                {
                    CSDebug.LogError("No camera found in the scene!");
                    return;
                }
            }

            mainCamera.backgroundColor = ColorSet.EnvironmentColors.SkyColor;
        }

        public Material GetTeamBlockMaterial(Domains domain)
        {
            return TeamMaterialSets[domain].BlockMaterial;
        }

        public Material GetTeamTransparentBlockMaterial(Domains domain)
        {
            return TeamMaterialSets[domain].TransparentBlockMaterial;
        }

        /// <summary>
        /// The per-domain crystal material that stands in for <paramref name="authored"/> - a crystal model's own
        /// default material - or null when there is none: no material set for this domain yet (ThemeManager fills
        /// TeamMaterialSets at runtime; anything that mints a crystal before that, or in a theme-less scene, keeps
        /// its authored material), or no slot of <see cref="BaseMaterialSet"/> holds that material.
        ///
        /// Keyed by the MATERIAL, never by the model's slot index (2026-10-13, refactor row F1 of
        /// Docs/Studios/OMNI_SHEPARD_LAB.md): the base set's slots are the omni crystal's (0 body, 1 depth prisms),
        /// so by index a Mass crystal given a domain would have put the omni BODY material on its depth-prism
        /// renderer - the wrong shader, its prisms frozen. A model whose material is not in the set keeps it.
        /// </summary>
        public Material GetTeamCrystalMaterial(Domains domain, Material authored)
        {
            int slot = CrystalSlotOf(BaseMaterialSet, authored);
            if (slot < 0) return null;
            if (TeamMaterialSets == null || !TeamMaterialSets.TryGetValue(domain, out var set) || set == null)
            {
                CSDebug.LogWarning($"No team material set for domain {domain}; crystal keeps its authored material.");
                return null;
            }
            return CrystalSlot(set, slot);
        }

        /// <summary>Which crystal slot of <paramref name="set"/> holds <paramref name="material"/>, or -1.</summary>
        public static int CrystalSlotOf(SO_MaterialSet set, Material material)
        {
            if (!set || !material) return -1;
            for (int slot = 0; slot < CrystalSlots; slot++)
                if (CrystalSlot(set, slot) == material) return slot;
            return -1;
        }

        const int CrystalSlots = 5;

        static Material CrystalSlot(SO_MaterialSet set, int slot) => slot switch
        {
            0 => set.CrystalMaterial,
            1 => set.CrystalMaterial1,
            2 => set.CrystalMaterial2,
            3 => set.CrystalMaterial3,
            4 => set.CrystalMaterial4,
            _ => null,
        };

        public Material GetTeamSpikeMaterial(Domains domain)
        {
            return TeamMaterialSets[domain].SpikeMaterial;
        }

        public Material GetTeamShieldedBlockMaterial(Domains domain)
        {
            return TeamMaterialSets[domain].ShieldedBlockMaterial;
        }

        public Material GetTeamTransparentShieldedBlockMaterial(Domains domain)
        {
            return TeamMaterialSets[domain].TransparentShieldedBlockMaterial;
        }

        public Material GetTeamDangerousBlockMaterial(Domains domain)
        {
            return TeamMaterialSets[domain].DangerousBlockMaterial;
        }

        public Material GetTeamTransparentDangerousBlockMaterial(Domains domain)
        {
            return TeamMaterialSets[domain].TransparentDangerousBlockMaterial;
        }

        public Material GetTeamSuperShieldedBlockMaterial(Domains domain)
        {
            return TeamMaterialSets[domain].SuperShieldedBlockMaterial;
        }

        public Material GetTeamTransparentSuperShieldedBlockMaterial(Domains domain)
        {
            return TeamMaterialSets[domain].TransparentSuperShieldedBlockMaterial;
        }
    }
}
