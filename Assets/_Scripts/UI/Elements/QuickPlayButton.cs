using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Reflex.Attributes;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// Launches a HexRace game immediately with the current party size.
    /// Solo (no friends in lobby): 1 player, 1 team, random domain.
    /// With party: player count = party size, 1 team, random domain.
    /// Attach to a Button GameObject — wires onClick automatically.
    /// </summary>
    public class QuickPlayButton : MonoBehaviour
    {
        [Inject] GameDataSO gameData;
        [Inject] SO_GameList gameList;
        [Inject] HostConnectionDataSO hostConnectionData;

        Button _button;

        void Start()
        {
            _button = GetComponent<Button>();
            if (_button)
                _button.onClick.AddListener(OnQuickPlay);
        }

        void OnDestroy()
        {
            if (_button)
                _button.onClick.RemoveListener(OnQuickPlay);
        }

        void OnQuickPlay()
        {
            var hexRace = FindHexRaceGame();
            if (hexRace == null)
            {
                Debug.LogError("[QuickPlayButton] Could not find HexRace game in SO_GameList.");
                return;
            }

            int humanCount = GetPartyHumanCount();
            int totalPlayers = humanCount;

            // Configure game data
            gameData.SyncFromArcadeGame(hexRace);
            gameData.ConfigurePlayerCounts(totalPlayers, humanCount);
            gameData.RequestedTeamCount = 1;

            if (gameData.SelectedIntensity)
                gameData.SelectedIntensity.Value = 1;

            // Assign random domain to local player
            if (gameData.LocalPlayer is Player localPlayer && localPlayer.IsOwner)
            {
                var randomDomain = GetRandomDomain();
                localPlayer.NetDomain.Value = randomDomain;
            }

            // Hand off the party session so MultiplayerSetup reuses the existing Relay connection
            if (HostConnectionService.Instance?.PartySession != null)
                gameData.ActiveSession = HostConnectionService.Instance.PartySession;

            Debug.Log($"[QuickPlayButton] Launching HexRace — humans={humanCount}, total={totalPlayers}");

            AudioSystem.Instance.PlayMenuAudio(MenuAudioCategory.LetsGo);
            gameData.InvokeGameLaunch();
        }

        SO_ArcadeGame FindHexRaceGame()
        {
            if (gameList?.Games == null) return null;

            foreach (var game in gameList.Games)
            {
                if (game != null && game.Mode == GameModes.HexRace)
                    return game;
            }
            return null;
        }

        int GetPartyHumanCount()
        {
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsServer)
                return Mathf.Max(1, nm.ConnectedClientsIds.Count);

            return hostConnectionData != null && hostConnectionData.PartyMembers != null
                ? Mathf.Max(1, hostConnectionData.PartyMembers.Count)
                : 1;
        }

        static Domains GetRandomDomain()
        {
            var domains = new[] { Domains.Jade, Domains.Ruby, Domains.Gold };
            return domains[Random.Range(0, domains.Length)];
        }
    }
}
