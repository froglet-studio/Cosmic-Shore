using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// One squad mate as a card in the Ark's Squads deck (Docs/MENU_REDESIGN_B.md section 95, Rev 31 "the deck"): the seat
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

        [Header("Actions")]
        [Tooltip("CHANGE VESSEL: on your own card in a squad you lead; opens the squad's editor on your seat (Rev 67, section 168).")]
        [SerializeField] private KitButton changeVesselButton;
        [Tooltip("OPEN THE SEAT: on an offline friend's card in a squad you lead; the seat opens and its pad stands empty until " +
                 "someone takes it (Shombith, 2026-11-06: \"anyone can take their place in the squad\").")]
        [SerializeField] private KitButton openSeatButton;

        [Header("Presence")]
        [Tooltip("The dot beside who flies the seat while they are here: you, an AI wingmate, an online friend (Rev 67).")]
        [SerializeField] private Graphic onlineDot;
        [Tooltip("The dot while the friend in the seat is offline.")]
        [SerializeField] private Graphic offlineDot;

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
        /// <summary>Raised by CHANGE VESSEL.</summary>
        public event System.Action ChangeVesselPressed;
        /// <summary>Raised by OPEN THE SEAT.</summary>
        public event System.Action OpenSeatPressed;

        public SquadNanobotSwap Nanobots => nanobots;

        void Awake()
        {
            if (button) button.onClick.AddListener(() => Pressed?.Invoke());
            if (changeVesselButton) changeVesselButton.onClick.AddListener(() => ChangeVesselPressed?.Invoke());
            if (openSeatButton) openSeatButton.onClick.AddListener(() => OpenSeatPressed?.Invoke());
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

        /// <summary>Which of the card's actions show: CHANGE VESSEL, OPEN THE SEAT.</summary>
        public void SetActions(bool changeVessel, bool openSeat)
        {
            if (changeVesselButton) changeVesselButton.gameObject.SetActive(changeVessel);
            if (openSeatButton) openSeatButton.gameObject.SetActive(openSeat);
        }

        /// <summary>The presence dot: lit while someone is here to fly the seat, dim for an offline friend, none for an open seat.</summary>
        public void SetPresence(bool shown, bool online)
        {
            if (onlineDot) onlineDot.enabled = shown && online;
            if (offlineDot) offlineDot.enabled = shown && !online;
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
