using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// One ability in the Hangar's ability column (style-B menu redesign, item 2): its name, its
    /// description and the hairline under it. The last row's hairline is hidden by colour, so the
    /// row keeps its height and the column does not jump.
    /// </summary>
    public class HangarAbilityRow : MonoBehaviour
    {
        [Header("Parts")]
        [Tooltip("SO_VesselAbility.Name (H3).")]
        [SerializeField] private KitText nameText;
        [Tooltip("SO_VesselAbility.Description (Body small).")]
        [SerializeField] private KitText descriptionText;
        [Tooltip("The hairline under the row (a KitDivider).")]
        [SerializeField] private KitThemedGraphic divider;

        public void Bind(SO_VesselAbility ability, bool last)
        {
            if (nameText) nameText.SetText(ability ? ability.Name : string.Empty);
            if (descriptionText) descriptionText.SetText(ability ? ability.Description : string.Empty);
            if (divider) divider.SetToken(UIColorToken.SurfaceDark, last ? 0f : 1f);
        }
    }
}
