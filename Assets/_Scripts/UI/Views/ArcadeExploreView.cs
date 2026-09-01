using CosmicShore.Core;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Gameplay;
using System;
using System.Collections.Generic;
using System.Linq;
using Obvious.Soap;
using Reflex.Attributes;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using CosmicShore.Utility;

namespace CosmicShore.UI
{
    public class ArcadeExploreView : MonoBehaviour
    {
        [Header("Game Selection View")]
        [Inject] SO_GameList GameList;
        [SerializeField] GameObject GameSelectionView;
        [SerializeField] Transform GameSelectionGrid;
        [SerializeField] ArcadeDPadNav ArcadeDPadNav;
        [SerializeField] WeeklyChallengeCard WeeklyChallengeCard;
        [Header("Game Detail View")]
        [SerializeField] ArcadeGameConfigureModal ArcadeGameConfigureModal;
        [Header("Test Settings")]
        [Tooltip("If true, will filter out unowned games from being available to play (MUST BE TRUE ON FOR PRODUCTION BUILDS")]
        [SerializeField] bool RespectInventoryForGameSelection = false;

        [SerializeField] VesselClassTypeVariable selectedVesselClassType;
        
        SO_ArcadeGame SelectedGame;
        List<GameCard> GameCards;

        // The sync manager this view subscribed to, remembered so the unsubscribe cannot miss
        // it if the scene's instance is replaced between enable and disable.
        ArcadeConfigSyncManager _pickSource;

        void OnEnable()
        {
            CatalogManager.OnLoadInventory += PopulateGameSelectionList;

            if (GameModeProgressionService.Instance != null)
                GameModeProgressionService.Instance.OnProgressionChanged += OnProgressionChanged;

            _pickSource = ArcadeConfigSyncManager.Instance;
            if (_pickSource != null)
            {
                _pickSource.OnGamePicksChanged += RefreshPartyPicks;
                RefreshPartyPicks();
            }
        }

        void OnDisable()
        {
            CatalogManager.OnLoadInventory -= PopulateGameSelectionList;

            if (GameModeProgressionService.Instance != null)
                GameModeProgressionService.Instance.OnProgressionChanged -= OnProgressionChanged;

            if (_pickSource != null)
            {
                _pickSource.OnGamePicksChanged -= RefreshPartyPicks;
                _pickSource = null;
            }
        }

        void Start()
        {
            LoadoutSystem.Init();
            PopulateGameSelectionList();
        }

        public void PopulateGameSelectionList()
        {
            GameCards = new List<GameCard>();
            // Rebuild the dpad grid from scratch - AddRow calls below would otherwise
            // append duplicate rows on every repopulate (inventory load, progression
            // change, favorite toggle), breaking gamepad navigation.
            ArcadeDPadNav.ResetGrid();
            ArcadeDPadNav.AddRow(new List<Button>());
            ArcadeDPadNav.AddButtonToRow(WeeklyChallengeCard.GetComponent<Button>(), 0);

            // Hand the card this view so a press can route through SelectWeeklyChallenge. Done on
            // every repopulate because the card's own state (this week's mode, completion, the
            // countdown label) is redrawn by Bind - and a repopulate is exactly when the grid
            // around it was rebuilt.
            if (WeeklyChallengeCard)
                WeeklyChallengeCard.Bind(this);

            // Deactivate all game cards and add them to the list of game cards
            for (var i = 0; i < GameSelectionGrid.transform.childCount; i++)
            {
                ArcadeDPadNav.AddRow(new List<Button>());

                var gameSelectionRow = GameSelectionGrid.GetChild(i);
                for (var j = 0; j < gameSelectionRow.childCount; j++)
                {
                    gameSelectionRow.GetChild(j).gameObject.SetActive(false);
                    GameCards.Add(gameSelectionRow.GetChild(j).GetComponent<GameCard>());

                    ArcadeDPadNav.AddButtonToRow(gameSelectionRow.GetChild(j).GetComponent<Button>(), i+1);
                }
            }

            // Sort favorited first, then alphabetically. Sort a COPY - sorting
            // GameList.Games directly mutates the ScriptableObject's serialized list
            // order at runtime, which any positional consumer of the list would see.
            var filteredGames = RespectInventoryForGameSelection ? GameList.Games.Where(x => CatalogManager.Inventory.ContainsGame(x.DisplayName)).ToList() : GameList.Games;

            // The Maelstrom is NOT one of the grid's cards. It is the meta-mode that draws the
            // others, so listing it beside them invites "play this one" when what it actually
            // means is "play several of these" - and it now has its own launch panel, in its own
            // window, reached from its own control. Excluded here rather than removed from
            // SO_GameList, because that list is also the roster the tournament pool and the
            // client-side mode lookup read.
            var sortedGames = new List<SO_ArcadeGame>(
                filteredGames.Where(g => g && g.Mode != CosmicShore.Data.GameModes.Tournament));
            sortedGames.Sort((x, y) =>
            {
                int flagComparison = FavoriteSystem.IsFavorited(y.Mode).CompareTo(FavoriteSystem.IsFavorited(x.Mode));
                if (flagComparison == 0)
                    return string.Compare(x.DisplayName, y.DisplayName, StringComparison.Ordinal); // Sort alphabetically by Name if they're tied

                return flagComparison;
            });

            var progressionService = GameModeProgressionService.Instance;

            for (var i = 0; i < GameCards.Count && i < GameList.Games.Count && i < sortedGames.Count; i++)
            {
                var game = sortedGames[i];

                CSDebug.Log($"ExploreMenu - Populating Game Select List: {game.DisplayName}");

                var gameCard = GameCards[i];
                gameCard.GameMode = game.Mode;
                gameCard.Favorited = FavoriteSystem.IsFavorited(game.Mode);
                gameCard.GetComponent<Button>().onClick.RemoveAllListeners();
                gameCard.ExploreView = this;

                // Check if this game mode is unlocked via the quest progression system
                bool isLocked = progressionService != null && !progressionService.IsGameModeUnlocked(game.Mode);
                gameCard.SetLocked(isLocked);

                if (!isLocked)
                {
                    gameCard.GetComponent<Button>().onClick.AddListener(() => SelectGame(game));
                }

                if (gameCard.TryGetComponent(out CallToActionTarget target))
                {
                    target.TargetID = game.CallToActionTargetType;
                }
                else
                {
                    CSDebug.LogWarningFormat("{0} - The {1} game card does not have Call To Action Target Component. Please attach it.",
                        nameof(ArcadeExploreView), game.CallToActionTargetType.ToString());
                }

                gameCard.gameObject.SetActive(true);
            }

            RefreshPartyPicks();

            ArcadeDPadNav.RefreshSelection();
        }

        void OnProgressionChanged(GameModeProgressionData data)
        {
            PopulateGameSelectionList();
        }

        /// <summary>
        /// Redraws every card's party chips from the replicated pick list. Driven by the sync
        /// manager's change event and re-run whenever the grid is rebuilt, because repopulating
        /// re-points the cards at different modes and their chips must follow.
        /// </summary>
        void RefreshPartyPicks()
        {
            if (GameCards == null) return;

            var sync = ArcadeConfigSyncManager.Instance;
            var picks = sync != null ? sync.GamePicks : null;

            for (int i = 0; i < GameCards.Count; i++)
            {
                var card = GameCards[i];
                if (!card) continue;

                int mode = (int)card.GameMode;
                List<int> avatars = null;

                if (picks != null)
                {
                    for (int j = 0; j < picks.Count; j++)
                    {
                        if (picks[j].GameMode != mode) continue;
                        avatars ??= new List<int>();
                        avatars.Add(picks[j].AvatarId);
                    }
                }

                card.ShowPartyPicks(avatars, sync != null && sync.LocalPlayerPicked(mode));
            }
        }

        public void SelectGame(SO_ArcadeGame selectedGame)
        {
            SelectedGame = selectedGame;

            // OpenFor, not ModalWindowIn + SetSelectedGame: a card's panel may live in its OWN
            // window (the Maelstrom's), and which window opens has to be decided before anything
            // is shown. Opening this one first and closing it again a frame later would flash the
            // wrong window every time a player picks that card.
            ArcadeGameConfigureModal.OpenFor(SelectedGame);
            // TODO: is is throwing a key not found exception
            //UserActionSystem.Instance.CompleteAction(SelectedGame.ViewUserAction);
        }

        /// <summary>
        /// The arcade card for a mode, or null when the roster does not carry one. Public because
        /// the weekly challenge card needs the mode's DISPLAY NAME and art, and the roster is
        /// injected here - a second lookup elsewhere would be a second thing to keep in step.
        /// </summary>
        public SO_ArcadeGame FindGameByMode(CosmicShore.Data.GameModes mode)
        {
            if (GameList?.Games == null) return null;

            for (int i = 0; i < GameList.Games.Count; i++)
            {
                var game = GameList.Games[i];
                if (game && game.Mode == mode) return game;
            }

            return null;
        }

        /// <summary>
        /// Open the launch modal for THIS WEEK'S weekly challenge, with its intensity and seat count
        /// pinned. Routes through the ordinary launch surface rather than a bespoke one - the
        /// weekly challenge is a mode you already know with one objective attached.
        ///
        /// <para>The mode's quest-progression LOCK is deliberately not consulted: the challenge is
        /// the same for every player on a given date, and skipping it per player would mean two
        /// players no longer share a date's challenge. (Flip
        /// <c>WeeklyChallengeCatalogSO.respectModeProgression</c> to change that.)</para>
        /// </summary>
        public void SelectWeeklyChallenge()
        {
            var service = WeeklyChallengeService.Instance;
            if (service == null)
            {
                CSDebug.LogWarning("[ArcadeExploreView] No WeeklyChallengeService - the weekly " +
                                   "challenge cannot be launched.");
                return;
            }

            var challenge = service.ThisWeek;
            if (!challenge.IsValid)
            {
                CSDebug.LogWarning("[ArcadeExploreView] ThisWeek's weekly challenge did not resolve " +
                                   "(missing or empty WeeklyChallengeCatalog).");
                return;
            }

            var card = FindGameByMode(challenge.GameMode);
            if (card == null)
            {
                CSDebug.LogWarning($"[ArcadeExploreView] Weekly challenge names {challenge.GameMode}, " +
                                   "which has no card in SO_GameList - remove it from the " +
                                   "WeeklyChallengeCatalog pool.");
                return;
            }

            SelectedGame = card;
            ArcadeGameConfigureModal.OpenForWeeklyChallenge(card, challenge);
        }

        public void SelectShip(SO_Vessel selectedShip)
        {
            CSDebug.Log($"SelectShip: {selectedShip.Name}");

            selectedVesselClassType.Value = selectedShip.Class;
            // TODO - Remove statics from MiniGame, use SOAP Data Container
            // notify the mini game engine that this is the vessel to play
            // MiniGame.PlayerShipType = selectedShip.Class;

            // Set resource levels from the vessel's config
            MiniGame.ResourceCollection = selectedShip.InitialResourceLevels;
        }

        public void PlaySelectedGame()
        {
            AudioSystem.Instance.PlayMenuAudio(MenuAudioCategory.LetsGo);
            LoadoutSystem.SaveGameLoadOut(SelectedGame.Mode, new Loadout(MiniGame.IntensityLevel, MiniGame.NumberOfPlayers, MiniGame.PlayerVesselType, SelectedGame.Mode, SelectedGame.IsMultiplayer));
            Arcade.Instance.LaunchArcadeGame(SelectedGame.Mode, MiniGame.PlayerVesselType, MiniGame.ResourceCollection, MiniGame.IntensityLevel, MiniGame.NumberOfPlayers, SelectedGame.IsMultiplayer, false);
        }

        public void ToggleFavorite()
        {
            FavoriteSystem.ToggleFavorite(SelectedGame.Mode);
            PopulateGameSelectionList();
        }
    }
}
