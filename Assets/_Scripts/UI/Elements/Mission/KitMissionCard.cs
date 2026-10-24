using System;
using System.Globalization;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// One style-B mission card (Docs/MENU_REDESIGN_B.md section 17.2): the mission's art, "Mission -
    /// difficulty 1 - 9", its name, its threat count and its description. It draws an
    /// <see cref="SO_Mission"/> directly. Missions are their own games, not arcade cards, so this card
    /// shares the arcade card's LOOK and none of its data path.
    ///
    /// <para>A press asks the card's <see cref="MenuAvailabilityView"/> first. While missions are
    /// Locked, the press refuses with the shell's sting and reason and nothing else happens. Once
    /// they are Available, <see cref="Pressed"/> is raised for the owning view.</para>
    /// </summary>
    public class KitMissionCard : MonoBehaviour
    {
        [Header("Parts")]
        [SerializeField, Tooltip("The card's selectable tile. Its onClick calls OnPressed.")]
        KitTile tile;
        [SerializeField, Tooltip("The mission's CardBackground.")]
        Image art;
        [SerializeField, Tooltip("\"Mission - difficulty 1 - 9\".")]
        TMP_Text eyebrowText;
        [SerializeField, Tooltip("The mission's DisplayName.")]
        TMP_Text titleText;
        [SerializeField, Tooltip("\"8 threats\" - how many threats the mission can field.")]
        TMP_Text threatsText;
        [SerializeField, Tooltip("The mission's Description.")]
        TMP_Text taglineText;

        [Header("Availability")]
        [SerializeField, Tooltip("Locked refuses a press with its message; Available raises Pressed.")]
        MenuAvailabilityView availability;

        const string EyebrowRange = "Mission - difficulty {0} - {1}";
        const string EyebrowSingle = "Mission - difficulty {0}";

        /// <summary>The mission this card draws.</summary>
        public SO_Mission Mission { get; private set; }

        /// <summary>The tile, for focus and selection.</summary>
        public KitTile Tile => tile;

        /// <summary>Raised when an Available card is pressed.</summary>
        public event Action<KitMissionCard> Pressed;

        public void Draw(SO_Mission mission)
        {
            Mission = mission;
            if (!mission) return;
            if (art) art.sprite = mission.CardBackground;
            if (titleText) titleText.text = mission.DisplayName;
            if (taglineText) taglineText.text = mission.Description;
            if (eyebrowText)
                eyebrowText.text = mission.MinDifficulty == mission.MaxDifficulty
                    ? string.Format(CultureInfo.InvariantCulture, EyebrowSingle, mission.MinDifficulty)
                    : string.Format(CultureInfo.InvariantCulture, EyebrowRange, mission.MinDifficulty, mission.MaxDifficulty);
            if (threatsText) threatsText.text = ThreatsCaption(mission.PotentialThreats?.Length ?? 0);
        }

        /// <summary>"1 threat", "8 threats", or empty when the mission lists none.</summary>
        public static string ThreatsCaption(int count) =>
            count <= 0 ? string.Empty : count == 1 ? "1 threat" : $"{count} threats";

        /// <summary>The tile's onClick.</summary>
        public void OnPressed()
        {
            if (availability && !availability.TryPress()) return;
            Pressed?.Invoke(this);
        }
    }
}
