using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// One squad mate as a card in the Ark's Squads deck (Docs/MENU_REDESIGN_B.md section 93, Rev 31 "the deck"): the seat
    /// and its number, the hull, who flies it, and the four picks (vessel, element, pilot, artifact) as rows. The pilot stands
    /// beside the card, stepping out of its frame; an open seat shows an outline instead. <see cref="SquadsPage"/> writes it.
    /// </summary>
    public class SquadMateCard : MonoBehaviour
    {
        [Header("Card")]
        [Tooltip("The whole card: in Make squad a press picks this seat to edit.")]
        [SerializeField] private KitButton button;
        [Tooltip("The thin line in the domain colour round the card being edited (off otherwise).")]
        [SerializeField] private Graphic editMark;

        [Header("Words")]
        [SerializeField] private TMP_Text seatText;
        [SerializeField] private TMP_Text numberText;
        [SerializeField] private TMP_Text whoText;
        [SerializeField] private TMP_Text vesselText;
        [SerializeField] private TMP_Text elementText;
        [SerializeField] private TMP_Text pilotText;
        [SerializeField] private TMP_Text artifactText;

        [Header("Art")]
        [SerializeField] private Image hullImage;
        [Tooltip("The pilot, full figure, stepping out of the card.")]
        [SerializeField] private Image pilotImage;
        [Tooltip("Shown instead of the pilot while the seat is open for a friend.")]
        [SerializeField] private CanvasGroup openSeatGroup;
        [SerializeField] private SquadNanobotSwap nanobots;

        [Header("Colours")]
        [SerializeField] private Color artifactColor = new(0.902f, 0.914f, 1f, 1f);
        [Tooltip("The artifact row's word while no artifact is carried.")]
        [SerializeField] private Color noArtifactColor = new(0.361f, 0.373f, 0.439f, 1f);

        /// <summary>Raised when the card is pressed.</summary>
        public event System.Action Pressed;

        public SquadNanobotSwap Nanobots => nanobots;

        void Awake()
        {
            if (button) button.onClick.AddListener(() => Pressed?.Invoke());
        }

        public void Draw(string seat, string number, string who, Sprite hull, string vessel, string element,
            Sprite pilot, string pilotName, string artifact, bool carriesArtifact, bool openSeat)
        {
            Set(seatText, seat);
            Set(numberText, number);
            Set(whoText, who);
            Set(vesselText, vessel);
            Set(elementText, element);
            Set(pilotText, pilotName);
            Set(artifactText, artifact);
            if (artifactText) artifactText.color = carriesArtifact ? artifactColor : noArtifactColor;
            if (hullImage)
            {
                hullImage.sprite = hull;
                hullImage.enabled = hull;
            }
            if (pilotImage)
            {
                pilotImage.sprite = pilot;
                pilotImage.enabled = pilot && !openSeat;
            }
            if (openSeatGroup) KitVisibility.SetShown(openSeatGroup, openSeat);
        }

        /// <summary>Marks the card being edited in Make squad, in the domain colour; in a made squad no card is marked.</summary>
        public void SetEditing(bool editing, Color domain)
        {
            if (!editMark) return;
            editMark.enabled = editing;
            editMark.color = domain;
        }

        /// <summary>Make squad presses cards; a made squad's cards are shown, not pressed.</summary>
        public void SetPressable(bool pressable)
        {
            if (button) button.interactable = pressable;
        }

        static void Set(TMP_Text t, string s)
        {
            if (t) t.text = s;
        }
    }
}
