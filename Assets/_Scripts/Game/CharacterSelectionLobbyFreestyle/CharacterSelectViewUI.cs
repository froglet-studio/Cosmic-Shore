using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.App.UI.Views
{
    public class CharacterSelectViewUI : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private Button shipSelectButton;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button readyButton;
        [SerializeField] private Button leaveSquadButton;
        [SerializeField] private Button unreadyButton;
        [SerializeField] private Button inviteButton;
        [SerializeField] private Button teamSelectButton;

        [Header("Player Texts")]
        [SerializeField] private TextMeshProUGUI playerNameText;
        [SerializeField] private TextMeshProUGUI stateText;

        [Header("Friend UI Elements")]
        [SerializeField] private Image _clientClassImage;
        [SerializeField] private TextMeshProUGUI[] friendNameTexts;
        [SerializeField] private TextMeshProUGUI[] friendStatesTexts;
        [SerializeField] private GameObject[] addFriendButtonObjects;
        [SerializeField] private Image[] friendClassImages;

        [Header("Sprites")]
        [SerializeField] private Sprite teamSelectSprite;
        [SerializeField] private Sprite[] _classSprites;

        [SerializeField] private Transform _characterDataContent;
        [SerializeField] private TextMeshProUGUI _characterSelectText;
        [SerializeField] private Image _mainClassSelectionPanelImage;
        [SerializeField] private GameObject _characterSelectionPanel;
        [SerializeField] private CharacterSelectClassComponentReference _characterSelectListElement;

        [SerializeField] private ToggleGroup teamToggleGroup; // Reference to the ToggleGroup.
        [SerializeField] private List<Toggle> teamToggles;
        [SerializeField] private List<Sprite> _toggleSelectedSprites;

        public ToggleGroup TeamToggleGroup => teamToggleGroup;
        public List<Toggle> TeamToggles => teamToggles;
        public List<Sprite> ToggleSelectedSprites => _toggleSelectedSprites;

        /// <summary>
        /// Updates the ready button text based on the ready state.
        /// </summary>
        /// <param name="isReady">If set to <c>true</c>, button shows ready text.</param>
        public void ToggleReadyButtonText(bool isReady)
        {
            if (readyButton != null)
            {
                // Optionally, you can update the button text component if it's a child.
                TextMeshProUGUI buttonText = readyButton.GetComponentInChildren<TextMeshProUGUI>();
                if (buttonText != null)
                {
                    buttonText.text = isReady ? "Ready!" : "Not Ready";
                }
            }
        }

        #region UI Element Accessors

        public Button ShipSelectButton => shipSelectButton;
        public Button ConfirmButton => confirmButton;
        public Button ReadyButton => readyButton;
        public Button LeaveSquadButton => leaveSquadButton;
        public Button UnreadyButton => unreadyButton;
        public Button InviteButton => inviteButton;
        public Button TeamSelectButton => teamSelectButton;

        public TextMeshProUGUI PlayerNameText => playerNameText;
        public TextMeshProUGUI StateText => stateText;
        public TextMeshProUGUI CharacterSelectText => _characterSelectText;
        public TextMeshProUGUI[] FriendNameTexts => friendNameTexts;
        public TextMeshProUGUI[] FriendStatesTexts => friendStatesTexts;
        public GameObject[] AddFriendButtonObjects => addFriendButtonObjects;
        public Image[] FriendClassImages => friendClassImages;
        public Sprite TeamSelectSprite => teamSelectSprite;
        public Sprite[] ClassSprites => _classSprites;
        public Image ClientClassImage => _clientClassImage;
        public GameObject CharacterSelectionPanel => _characterSelectionPanel;
        public Transform CharacterDataContent => _characterDataContent;
        public CharacterSelectClassComponentReference CharacterSelectListElement => _characterSelectListElement;
        public Image MainClassSelectionPanelImage => _mainClassSelectionPanelImage;

        #endregion
    }
}
