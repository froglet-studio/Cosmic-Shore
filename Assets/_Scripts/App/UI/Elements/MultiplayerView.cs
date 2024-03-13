using CosmicShore.Utility.ClassExtensions;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using CosmicShore.Game.Arcade;
using UnityEngine.SceneManagement;

namespace CosmicShore
{
    public class MultiplayerView : MonoBehaviour
    {
        [Header("UI Buttons")]
        [SerializeField] private Button joinGameButton;
        [SerializeField] private Button hostGameButton;
        [SerializeField] private Button spectateGameButton;
        
        [Header("Vessel Settings")]
        [SerializeField] private SO_Vessel hostVessel;
        [SerializeField] private SO_Vessel clientVessel;
        
        private void Awake()
        {
            hostGameButton.onClick.AddListener(HostGame);
            joinGameButton.onClick.AddListener(JoinGame);
            spectateGameButton.onClick.AddListener(SpectateGame);
        }

        private void HostGame()
        {
            this.LogWithClassMethod("", "Hosting a game.");
            LoadMiniGame(ShipTypes.Manta, hostVessel, 1,1);
            NetworkManager.Singleton.StartHost();
        }

        private void JoinGame()
        {
            this.LogWithClassMethod("", "Joining a game as client.");
            LoadMiniGame(ShipTypes.Rhino, clientVessel, 1,1);
            NetworkManager.Singleton.StartClient();
        }

        private void SpectateGame()
        {
            this.LogWithClassMethod("", "Join a game as spectator.");
            NetworkManager.Singleton.StartClient();
        }

        private void LoadMiniGame(ShipTypes type, SO_Vessel vessel, int intensity, int playerCount)
        {
            MiniGame.PlayerShipType = type;
            MiniGame.PlayerVessel = vessel;
            MiniGame.IntensityLevel = intensity;
            MiniGame.NumberOfPlayers = playerCount;

            SceneManager.LoadScene("MinigameCellularDuel");
        }
    }
}
