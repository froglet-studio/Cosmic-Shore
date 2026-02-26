using System.Collections.Generic;
using CosmicShore.Gameplay;
using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.UI;
using CosmicShore.ScriptableObjects;
namespace CosmicShore.UI
{
    /// <summary>
    /// Modal panel that lists all online players from the presence lobby.
    /// Reads from <see cref="HostConnectionDataSO.OnlinePlayers"/> (SOAP ScriptableList)
    /// and instantiates <see cref="OnlinePlayerEntry"/> prefabs.
    /// Pressing the "+" on an entry triggers an invite via <see cref="HostConnectionService"/>.
    /// </summary>
    public class OnlinePlayersPanel : MonoBehaviour
    {
        [Header("SOAP Data")]
        [SerializeField] private HostConnectionDataSO connectionData;

        [Header("References")]
        [SerializeField] private GameObject playerEntryPrefab;
        [SerializeField] private Transform entryContainer;
        [SerializeField] private Button closeButton;
        [SerializeField] private SO_ProfileIconList profileIcons;

        [Header("State")]
        [SerializeField] private GameObject emptyStateLabel;

        private readonly List<OnlinePlayerEntry> _activeEntries = new();

        // ─────────────────────────────────────────────────────────────────────
        // Unity Lifecycle
        // ─────────────────────────────────────────────────────────────────────

        void Awake()
        {
            closeButton?.onClick.AddListener(Hide);
        }

        void OnEnable()
        {
            if (connectionData?.OnlinePlayers != null)
            {
                connectionData.OnlinePlayers.OnItemAdded += OnPlayerAdded;
                connectionData.OnlinePlayers.OnItemRemoved += OnPlayerRemoved;
                connectionData.OnlinePlayers.OnCleared += OnPlayersCleared;
            }

            RebuildFromList();
        }

        void OnDisable()
        {
            if (connectionData?.OnlinePlayers != null)
            {
                connectionData.OnlinePlayers.OnItemAdded -= OnPlayerAdded;
                connectionData.OnlinePlayers.OnItemRemoved -= OnPlayerRemoved;
                connectionData.OnlinePlayers.OnCleared -= OnPlayersCleared;
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────────────────────────────

        public void Show()
        {
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        // ─────────────────────────────────────────────────────────────────────
        // List Sync
        // ─────────────────────────────────────────────────────────────────────

        private void RebuildFromList()
        {
            ClearEntries();

            if (connectionData?.OnlinePlayers == null || connectionData.OnlinePlayers.Count == 0)
            {
                emptyStateLabel?.SetActive(true);
                return;
            }

            emptyStateLabel?.SetActive(false);

            foreach (var info in connectionData.OnlinePlayers)
                SpawnEntry(info);
        }

        private void OnPlayerAdded(PartyPlayerData player)
        {
            emptyStateLabel?.SetActive(false);
            SpawnEntry(player);
        }

        private void OnPlayerRemoved(PartyPlayerData player)
        {
            for (int i = _activeEntries.Count - 1; i >= 0; i--)
            {
                if (_activeEntries[i].PlayerId == player.PlayerId)
                {
                    Destroy(_activeEntries[i].gameObject);
                    _activeEntries.RemoveAt(i);
                    break;
                }
            }

            if (_activeEntries.Count == 0)
                emptyStateLabel?.SetActive(true);
        }

        private void OnPlayersCleared()
        {
            ClearEntries();
            emptyStateLabel?.SetActive(true);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Entry Management
        // ─────────────────────────────────────────────────────────────────────

        private void SpawnEntry(PartyPlayerData info)
        {
            var go = Instantiate(playerEntryPrefab, entryContainer);
            var entry = go.GetComponent<OnlinePlayerEntry>();
            if (entry == null) return;

            var avatarSprite = ResolveAvatarSprite(info.AvatarId);
            entry.Populate(info, avatarSprite, OnInviteClicked);
            _activeEntries.Add(entry);
        }

        private void ClearEntries()
        {
            foreach (var entry in _activeEntries)
                Destroy(entry.gameObject);
            _activeEntries.Clear();
        }

        // ─────────────────────────────────────────────────────────────────────
        // Invite
        // ─────────────────────────────────────────────────────────────────────

        private async void OnInviteClicked(PartyPlayerData target)
        {
            if (HostConnectionService.Instance == null) return;
            await HostConnectionService.Instance.SendInviteAsync(target.PlayerId);
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

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
