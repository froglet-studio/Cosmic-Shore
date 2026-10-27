using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// One squad mate on the Ark's Squads tab: their seat (Leader, Rogue One, Rogue Two), who flies it (you, a friend,
    /// an AI wingmate or an open seat), and their four picks - vessel, element, pilot and artifact. Each pick is a
    /// button that steps to the next choice while the player may change it (their own seat, and the AI seats of a
    /// squad they made); <see cref="SquadsPage"/> decides that and writes the card.
    /// </summary>
    public class SquadMateCard : MonoBehaviour
    {
        public enum Pick { Vessel = 0, Element = 1, Pilot = 2, Artifact = 3, Seat = 4 }

        [Header("Who")]
        [SerializeField] private TMP_Text seatText;
        [SerializeField] private TMP_Text memberText;
        [Tooltip("Steps an AI seat to open and back, or opens a friend's seat again (squads the player made).")]
        [SerializeField] private KitButton seatButton;
        [SerializeField] private TMP_Text seatButtonLabel;

        [Header("Vessel")]
        [SerializeField] private KitButton vesselButton;
        [SerializeField] private Image vesselImage;
        [SerializeField] private TMP_Text vesselText;

        [Header("Element")]
        [SerializeField] private KitButton elementButton;
        [SerializeField] private TMP_Text elementText;

        [Header("Pilot")]
        [SerializeField] private KitButton pilotButton;
        [SerializeField] private Image pilotImage;
        [SerializeField] private TMP_Text pilotText;

        [Header("Artifact")]
        [SerializeField] private KitButton artifactButton;
        [SerializeField] private Image artifactImage;
        [SerializeField] private TMP_Text artifactText;

        /// <summary>Raised with the pick the player pressed.</summary>
        public event System.Action<Pick> Pressed;

        void Awake()
        {
            Hook(vesselButton, Pick.Vessel);
            Hook(elementButton, Pick.Element);
            Hook(pilotButton, Pick.Pilot);
            Hook(artifactButton, Pick.Artifact);
            Hook(seatButton, Pick.Seat);
        }

        void Hook(KitButton b, Pick pick)
        {
            if (b) b.onClick.AddListener(() => Pressed?.Invoke(pick));
        }

        public void DrawWho(string seat, string member, string seatAction)
        {
            if (seatText) seatText.text = seat;
            if (memberText) memberText.text = member;
            bool canSeat = !string.IsNullOrEmpty(seatAction);
            if (seatButton) seatButton.interactable = canSeat;
            if (seatButtonLabel) seatButtonLabel.text = canSeat ? seatAction : string.Empty;
        }

        public void DrawPicks(Sprite vesselSprite, string vessel, string element, Sprite pilotSprite, string pilot,
            Sprite artifactSprite, string artifact, bool editable)
        {
            Draw(vesselImage, vesselSprite);
            Draw(pilotImage, pilotSprite);
            Draw(artifactImage, artifactSprite);
            if (vesselText) vesselText.text = vessel;
            if (elementText) elementText.text = element;
            if (pilotText) pilotText.text = pilot;
            if (artifactText) artifactText.text = artifact;
            foreach (var b in new[] { vesselButton, elementButton, pilotButton, artifactButton })
                if (b) b.interactable = editable;
        }

        static void Draw(Image image, Sprite sprite)
        {
            if (!image) return;
            image.sprite = sprite;
            image.enabled = sprite;
        }
    }
}
