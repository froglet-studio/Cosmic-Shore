using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.App.UI.Elements.Hangar
{
    /// <summary>
    /// Displays a single vessel ability in the detail view.
    /// </summary>
    public class HangarAbilityCard : MonoBehaviour
    {
        [SerializeField] private Image abilityIcon;
        [SerializeField] private TMP_Text abilityName;
        [SerializeField] private TMP_Text abilityDescription;

        public void Configure(SO_ShipAbility ability)
        {
            if (ability == null) return;

            if (abilityIcon && ability.IconActive)
                abilityIcon.sprite = ability.IconActive;

            if (abilityName)
                abilityName.text = ability.Name;

            if (abilityDescription)
                abilityDescription.text = ability.Description;
        }
    }
}
