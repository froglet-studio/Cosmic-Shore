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
        // FormerlySerializedAs because the daily -> weekly rename renamed the FIELD, and Unity
        // keys serialized data by field NAME: every scene and prefab already wired to the card
        // still said DailyChallengeCard, so the reference deserialized NULL and the arcade grid
        // died on the first line that touched it. Renaming a serialized field is a data
        // migration, not a refactor.
        [FormerlySerializedAs("DailyChallengeCard")]
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

            // The weekly challenge card is OPTIONAL, and this method must survive it being
            // absent: it is the one thing that populates, unlocks and wires EVERY game card, so
            // a null reference here takes the whole arcade grid down with it - cards left
            // inactive, no click listeners, and every card warning that it has no SO_ArcadeGame
            // for its unassigned mode.
            //
            // It gets the grid's first row when it is present, and NO row when it is not: an
            // empty row is not the same thing, because ArcadeDPadNav clamps a column into
            // "row.Count - 1", which is -1 on an empty row and throws the moment the dpad walks
            // into it.
            var challengeButton = WeeklyChallengeCard ? WeeklyChallengeCard.GetComponent<Button>() : null;
            int rowIndex = -1;
            if (challengeButton)
            {
                ArcadeDPadNav.AddRow(new List<Button>());
                ArcadeDPadNav.AddButtonToRow(challengeButton, ++rowIndex);
            }

            // Hand the card this view so a press can route through SelectWeeklyChallenge. Done on
            // every repopulate because the card's own state (this week's mode, completion, the
            // countdown label) is redrawn by Bind - and a repopulate is exactly when the grid
            // around it was rebuilt.
            if (WeeklyChallengeCard)
                WeeklyChallengeCard.Bind(this);

            // The roster is resolved BEFORE the grid is walked, because the grid has to be big
            // enough to hold it - see EnsureGridCapacity. Sort a COPY: sorting GameList.Games
            // directly mutates the ScriptableObject's serialized list order at runtime, which
            // any positional consumer of the list would see.
            var filteredGames = RespectInventoryForGameSelection
                ? GameList.Games.Where(x => CatalogManager.Inventory.ContainsGame(x.DisplayName)).ToList()
                : GameList.Games;

            // The Maelstrom is NOT one of the grid's cards. It is the meta-mode that draws the
            // others, so listing it beside them invites "play this one" when what it actually
            // means is "play several of these" - and it now has its own launch panel, in its own
            // window, reached from its own control. Excluded here rather than removed from
            // SO_GameList, because that list is also the roster the tournament pool and the
            // client-side mode lookup read.
            var sortedGames = new List<SO_ArcadeGame>(
                filteredGames.Where(g => g && g.Mode != CosmicShore.Data.GameModes.Maelstrom));
            sortedGames.Sort((x, y) =>
            {
                int flagComparison = FavoriteSystem.IsFavorited(y.Mode).CompareTo(FavoriteSystem.IsFavorited(x.Mode));
                if (flagComparison == 0)
                    return string.Compare(x.DisplayName, y.DisplayName, StringComparison.Ordinal); // Sort alphabetically by Name if they're tied

                return flagComparison;
            });

            EnsureGridCapacity(sortedGames.Count);

            // Deactivate all game cards and add them to the list of game cards
            for (var i = 0; i < GameSelectionGrid.transform.childCount; i++)
            {
                // Counted rather than derived from i, because the challenge row above it is
                // conditional - "i + 1" is off by one on any scene that carries no card.
                ArcadeDPadNav.AddRow(new List<Button>());
                rowIndex++;

                var gameSelectionRow = GameSelectionGrid.GetChild(i);
                for (var j = 0; j < gameSelectionRow.childCount; j++)
                {
                    gameSelectionRow.GetChild(j).gameObject.SetActive(false);
                    GameCards.Add(gameSelectionRow.GetChild(j).GetComponent<GameCard>());

                    ArcadeDPadNav.AddButtonToRow(gameSelectionRow.GetChild(j).GetComponent<Button>(), rowIndex);
                }
            }

            var progressionService = GameModeProgressionService.Instance;

            // Bounded by the SLOTS and the ROSTER, and by nothing else. The old third term
            // (GameList.Games.Count) was a ceiling on a different list - it counts the Maelstrom
            // and any inventory-filtered card - so it could only ever mask the real bound.
            for (var i = 0; i < GameCards.Count && i < sortedGames.Count; i++)
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

        /// <summary>
        /// Grow the grid until it can show every game on the roster.
        ///
        /// <para>The grid is AUTHORED at a fixed size - 3 rows x 4 in Menu_Main, 12 slots - and
        /// the populate loop is bounded by it, so the roster silently truncated the moment it
        /// grew past that. Alphabetically-last modes simply stopped existing in the arcade: no
        /// error, no gap in the grid, nothing on screen to distinguish "not shipped yet" from
        /// "no slot left". Shipping Switchback is what crossed the line (13 renderable cards
        /// into 12 slots, dropping Wildlife Liberation), but the ceiling had been one card away
        /// for several modes and would have been hit by whichever one landed next.</para>
        ///
        /// <para>Rows are cloned from the LAST authored row, so a new row inherits its layout
        /// group, sizing and card prefab wiring rather than needing any of that re-authored -
        /// and a scene that resizes its grid keeps working with nothing here to update. Cloning
        /// happens at most once per repopulate and only when the roster actually overflows.</para>
        /// </summary>
        void EnsureGridCapacity(int required)
        {
            if (required <= 0 || GameSelectionGrid == null || GameSelectionGrid.childCount == 0)
                return;

            var template = GameSelectionGrid.GetChild(GameSelectionGrid.childCount - 1);
            int perRow = template.childCount;
            if (perRow <= 0) return;   // an empty template can never close the gap

            int capacity = 0;
            for (int i = 0; i < GameSelectionGrid.childCount; i++)
                capacity += GameSelectionGrid.GetChild(i).childCount;

            int rows = RowsNeeded(capacity, perRow, required);
            for (int i = 0; i < rows; i++)
            {
                var row = Instantiate(template, GameSelectionGrid);
                row.name = $"{template.name} ({GameSelectionGrid.childCount})";
            }
        }

        /// <summary>
        /// How many rows of <paramref name="perRow"/> slots must be added to a grid holding
        /// <paramref name="capacity"/> for it to show <paramref name="required"/> cards. Pure,
        /// so the arithmetic is asserted directly (<c>ArcadeGridCapacityTests</c>) rather than
        /// through a scene: an off-by-one here does not throw, it hides a game mode.
        /// </summary>
        public static int RowsNeeded(int capacity, int perRow, int required)
        {
            if (perRow <= 0 || required <= capacity) return 0;
            int deficit = required - capacity;
            return (deficit + perRow - 1) / perRow;
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
