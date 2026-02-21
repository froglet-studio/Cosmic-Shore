using System.Collections.Generic;
using CosmicShore.App.Profile;
using CosmicShore.App.UI.Panels;
using CosmicShore.Game.Party;
using Reflex.Attributes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.App.UI.Views
{
    public class PartyArcadeView : MonoBehaviour
    {
        [Inject] PartyManager partyManager;
        [Inject] PlayerDataService playerDataService;

        [Header("Local Player")]
        [SerializeField] private Image    localAvatarImage;
        [SerializeField] private TMP_Text localDisplayNameText;

        [Header("Party Members")]
        [SerializeField] private Transform  partyMemberContainer;
        [SerializeField] private GameObject partyMemberPrefab;

        [Header("Actions")]
        [SerializeField] private Button            addPlayerButton;
        [SerializeField] private OnlinePlayersPanel onlinePlayersPanel;

        [Header("Data")]
        [SerializeField] private SO_ProfileIconList profileIcons;

        private readonly List<GameObject> _memberRows = new();

        // -----------------------------------------------------------------------------------------
        // Unity Lifecycle

        void Awake()
        {
            addPlayerButton?.onClick.AddListener(OpenOnlinePlayers);
        }

        void OnEnable()
        {
            if (partyManager != null)
            {
                partyManager.OnPartyMemberJoined += OnPartyMemberJoined;
                partyManager.OnJoinedParty        += OnJoinedParty;
            }
        }

        void OnDisable()
        {
            if (partyManager != null)
            {
                partyManager.OnPartyMemberJoined -= OnPartyMemberJoined;
                partyManager.OnJoinedParty        -= OnJoinedParty;
            }
        }

        void Start()
        {
            RefreshLocalPlayerDisplay();
        }

        // -----------------------------------------------------------------------------------------
        // Public

        /// <summary>Call this after profile changes to update the local player display.</summary>
        public void RefreshLocalPlayerDisplay()
        {
            if (playerDataService == null) return;
            var profile = playerDataService.CurrentProfile;
            if (profile == null) return;

            if (localDisplayNameText != null)
                localDisplayNameText.text = profile.displayName;

            var sprite = ResolveAvatarSprite(profile.avatarId);
            if (sprite != null && localAvatarImage != null)
                localAvatarImage.sprite = sprite;
        }

        // -----------------------------------------------------------------------------------------
        // Panel

        private void OpenOnlinePlayers()
        {
            onlinePlayersPanel?.Show();
        }

        // -----------------------------------------------------------------------------------------
        // Party member list

        private void OnPartyMemberJoined(string displayName)
        {
            // For now just log; in a full impl you'd query their avatar from the party session
            Debug.Log($"[PartyArcadeView] Party member joined: {displayName}");
            // TODO: spawn a partyMemberPrefab row with their avatar + name
            //       You'd get the full OnlinePlayerInfo from PartyManager.PartySession.Players
        }

        private void OnJoinedParty(string hostDisplayName)
        {
            Debug.Log($"[PartyArcadeView] Joined party hosted by {hostDisplayName}");
            // TODO: update panel to show host + self in the party list
        }

        // -----------------------------------------------------------------------------------------
        // Helpers

        private Sprite ResolveAvatarSprite(int avatarId)
        {
            if (profileIcons == null) return null;
            foreach (var icon in profileIcons.profileIcons)
            {
                if (icon.Id == avatarId)
                    return icon.IconSprite;
            }
            return null;
        }
    }
}
