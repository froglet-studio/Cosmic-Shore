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
        /// The per-domain crystal material for model slot <paramref name="index"/>, or null when
        /// this domain has no material set yet. TeamMaterialSets is populated at runtime by
        /// ThemeManager, so anything that mints a crystal before that (or in a theme-less scene)
        /// gets null and keeps its authored material instead of a KeyNotFoundException.
        /// </summary>
        public Material GetTeamCrystalMaterial(Domains domain, int index)
        {
            if (TeamMaterialSets == null || !TeamMaterialSets.TryGetValue(domain, out var set) || set == null)
            {
                CSDebug.LogWarning($"No team material set for domain {domain}; crystal keeps its authored material.");
                return null;
            }

            switch (index)
            {
                case 0: return set.CrystalMaterial;
                case 1: return set.CrystalMaterial1;
                case 2: return set.CrystalMaterial2;
                case 3: return set.CrystalMaterial3;
                default:
                    CSDebug.LogWarning($"Invalid crystal material index {index} for domain {domain}. Returning default crystal material.");
                    break;
            }
            return set.CrystalMaterial;
        }

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
