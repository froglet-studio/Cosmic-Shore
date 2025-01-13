using CosmicShore.Utilities.Network;
using TMPro;
using UnityEngine;
using VContainer;


namespace CosmicShore.Game.UI
{
    public class RoomNameUI : MonoBehaviour
    {
        [SerializeField]
        TextMeshProUGUI m_RoomNameText;

        LocalLobby m_LocalLobby;
        string m_LobbyCode;

        [Inject]
        private void InjectDependencies(LocalLobby localLobby)
        {
            m_LocalLobby = localLobby;
            m_LocalLobby.OnChanged += UpdateUI;
        }

        void Start()            // Changed from Awake() to Start() so that the UI is updated after the LocalLobby is initialized
        {
            UpdateUI(m_LocalLobby);
        }

        private void OnDestroy()
        {
            m_LocalLobby.OnChanged -= UpdateUI;
        }

        private void UpdateUI(LocalLobby localLobby)
        {
            if (!string.IsNullOrEmpty(localLobby.LobbyCode))
            {
                m_LobbyCode = localLobby.LobbyCode;
                m_RoomNameText.text = $"Lobby Code: {m_LobbyCode}";
                gameObject.SetActive(true);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }
}
