using CosmicShore.Core;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Gameplay;
using System;
using System.Collections.Generic;
using System.Linq;
using Obvious.Soap;
using Reflex.Attributes;
using Reflex.Core;
using Reflex.Injectors;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using CosmicShore.Utility;

namespace CosmicShore.UI
{
    public class ArcadeExploreView : MonoBehaviour
    {
        [Header("Game Selection View")]
        [Inject] SO_GameList GameList;

        // Reflex injects objects that exist at SCENE LOAD. EnsureGridCapacity creates a row at
        // RUNTIME, so nothing injects it unless we do - and an un-injected card's MenuAudio has a
        // null AudioSystem, which throws from the Button's PERSISTENT onClick listener and eats
        // every runtime listener behind it (SelectGame among them). See EnsureGridCapacity.
        [Inject] Container _container;

        [Tooltip("Roster this grid draws INSTEAD of the injected MASTER list (every card, which the " +
                 "client-side mode lookup needs). The Arcade names ArcadeGames, the Arena " +
                 "ArenaGames; empty draws everything.\n\nThis is how a second card grid exists without a second implementation " +
                 "of one: the Arena is the same view, the same cards, the same launch modal and the " +
                 "same config - pointed at its own SO_GameList. A parallel screen would have to " +
                 "re-derive progression locks, favourites, party picks and the daily challenge, and " +
                 "would drift from all four.")]
        [SerializeField] SO_GameList rosterOverride;
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
        [SerializeField] VesselClassTypeVariable selectedVesselClassType;
        
        SO_ArcadeGame SelectedGame;
        List<GameCard> GameCards;

        // Slots the progression chain locked THIS populate. Recorded rather than re-derived,
        // because "did this card get a SelectGame listener?" is only answerable at the moment the
        // decision is made - Button.onClick can report its PERSISTENT count and nothing else, so a
        // runtime listener is invisible to any later inspection.
        readonly List<int> _lockedSlots = new();

        /// <summary>
        /// The roster this grid draws - its own override when one is authored, else the injected
        /// arcade list. Resolved through ONE accessor so no consumer can read a different roster
        /// than the cards were built from.
        /// </summary>
        SO_GameList Roster => rosterOverride ? rosterOverride : GameList;

        // The sync manager this view subscribed to, remembered so the unsubscribe cannot miss
        // it if the scene's instance is replaced between enable and disable.
        ArcadeConfigSyncManager _pickSource;

        [Header("Card Reveal")]
        [Tooltip("Timing for the staggered card pop-in played when this grid's window opens " +
                 "(the card-entrance block). Leave empty for the fleet defaults in CardGridReveal.")]
        [SerializeField] HUDAnimationSettingsSO cardRevealSettings;

        // The window this grid lives in. Its OnModalOpened is the reveal trigger: the window
        // hides by CanvasGroup alpha, so OnEnable here fires at scene load, never on open.
        ModalWindowManager _hostModal;
        readonly List<GameObject> _revealCards = new();
        Coroutine _reveal;

        void OnEnable()
        {
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
            // A reveal cut short by the grid going away must not strand a card at alpha 0.
            CardGridReveal.Snap(this, _revealCards, _reveal);
            _reveal = null;

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

            _hostModal = GetComponentInParent<ModalWindowManager>(true);
            if (_hostModal) _hostModal.OnModalOpened += PlayCardReveal;
        }

        void OnDestroy()
        {
            if (_hostModal) _hostModal.OnModalOpened -= PlayCardReveal;
            CardGridReveal.Snap(this, _revealCards, _reveal);
            _reveal = null;
        }

        /// <summary>
        /// The staggered pop-in of every visible card, in grid order, each time this grid's
        /// window opens. Deliberately NOT played from PopulateGameSelectionList: that also runs
        /// on a favourite toggle or a progression change while the window is up, and a settled
        /// card must not flicker (Docs/HomeHub/ARCHITECTURE.md §4.1).
        /// </summary>
        void PlayCardReveal()
        {
            // The previous run's cards are snapped to rest by Play BEFORE the list is rebuilt,
            // so a card that left the grid since cannot be stranded mid-pop.
            CardGridReveal.Snap(this, _revealCards, _reveal);
            _revealCards.Clear();
            if (GameCards != null)
                foreach (var card in GameCards)
                    if (card) _revealCards.Add(card.gameObject);
            _reveal = CardGridReveal.Play(this, _revealCards, cardRevealSettings, null);
        }

        public void PopulateGameSelectionList()
        {
            GameCards = new List<GameCard>();
            _lockedSlots.Clear();
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
            var roster = Roster;

            // The whole roster. There used to be a RespectInventoryForGameSelection flag here that
            // filtered the grid to games the player OWNED, serialized false under a comment reading
            // "MUST BE TRUE ON FOR PRODUCTION BUILDS". It was deleted rather than switched on,
            // because switching it on would have shipped an EMPTY ARCADE: the gate read
            // CatalogManager.Inventory, CatalogManager is PlayFab-era, and CatalogManager.prefab is
            // referenced by zero scenes and zero prefabs - so the manager never exists, Inventory is
            // never populated, and ContainsGame is false for all sixteen live modes. The flag was
            // not off by accident; it was unusable, and a flag that cannot be turned on is worse
            // than no flag because its comment reads as a to-do. Docs/UI_ARCHITECTURE_AUDIT.md
            // §2.11. Ownership gating, if it comes back, comes back on the live economy - not on
            // this.
            var filteredGames = roster.Games;

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

                if (isLocked) _lockedSlots.Add(i);

                if (!isLocked)
                {
                    gameCard.GetComponent<Button>().onClick.AddListener(() => SelectGame(game));
                }

                gameCard.gameObject.SetActive(true);
            }

            NormalizeGridRows();
            RefreshPartyPicks();

            // Last, and unconditionally: CalculateRelativeRectTransformBounds skips INACTIVE
            // objects, so this has to run after the cards that will be shown have been switched
            // on. Unconditional because the authored content height was already short of the
            // authored rows before any row was added - this is a repair as much as a fit.
            FitScrollContent();
            ReportUnreachableCards(sortedGames.Count);

            ArcadeDPadNav.RefreshSelection();
        }

        /// <summary>
        /// Bring the grid's ROWS into line with the cards that were just filled: a row is shown
        /// iff it holds a visible card, and every row is the height of the first.
        ///
        /// <para><b>A row's active state is this view's to own, exactly as a card's is.</b> The
        /// fill loop above switches every card off and the filled ones back on, but a card inside
        /// an INACTIVE row is not <c>activeInHierarchy</c> whatever its own flag says - so a row
        /// left disabled in the scene silently deletes four modes from the arcade with no error,
        /// no gap and nothing to distinguish it from "not shipped yet". That is not hypothetical:
        /// the Arena work disabled all three arcade rows in Menu_Main and the whole grid came up
        /// empty. Owning the card's flag and not the row's is owning half a rule.</para>
        ///
        /// <para><b>And an empty row must go away</b>, or it holds open a row's worth of layout
        /// for nothing.</para>
        ///
        /// <para><b>Uniform heights are what make the pitch uniform.</b> The grid stacks rows
        /// with a NEGATIVE spacing (-142.08 in Menu_Main) and does not control child height, so
        /// the gap between two rows is that row's own height plus the spacing. Menu_Main's three
        /// rows are authored 384.74 / 365.31 / 456.00 tall around identical 202.72-tall cards,
        /// which is 242.7 / 223.2 / 313.9 of pitch - and a row this view CLONES inherits the last
        /// one, so the overflow row arrives with 71 units of unexplained air above it. Every row
        /// takes the FIRST row's height, so one pitch holds across the whole grid however many
        /// rows exist. General rule: <b>with a layout group that does not control child size, a
        /// non-uniform child is a non-uniform gap - and a negative spacing makes it look
        /// deliberate.</b></para>
        /// </summary>
        void NormalizeGridRows()
        {
            if (GameSelectionGrid == null || GameSelectionGrid.childCount == 0) return;

            float height = GameSelectionGrid.GetChild(0) is RectTransform first
                ? first.rect.height
                : 0f;

            for (int i = 0; i < GameSelectionGrid.childCount; i++)
            {
                var row = GameSelectionGrid.GetChild(i);

                bool holdsCard = false;
                for (int j = 0; j < row.childCount && !holdsCard; j++)
                    holdsCard = row.GetChild(j).gameObject.activeSelf;

                if (row.gameObject.activeSelf != holdsCard)
                    row.gameObject.SetActive(holdsCard);

                if (i > 0 && height > 0f && row is RectTransform rect &&
                    !Mathf.Approximately(rect.rect.height, height))
                    rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            }
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
            if (rows <= 0) return;

            for (int i = 0; i < rows; i++)
            {
                var row = Instantiate(template, GameSelectionGrid);
                row.name = $"{template.name} ({GameSelectionGrid.childCount})";
                // A clone inherits the template's active state, and a template left disabled in
                // the scene would hand back a row whose cards can never be seen. NormalizeGridRows
                // settles this again from content, but a row created inactive would not even be
                // measured by the content fit in between.
                row.gameObject.SetActive(true);

                // THE ROW MUST BE INJECTED, and this is the line the whole feature turned on.
                //
                // Reflex populates [Inject] for objects present at SCENE LOAD (via the scene's
                // ContainerScope) and for anything a call site explicitly injects. A row created
                // here is neither, so every [Inject] field on its four cards is NULL - and one of
                // them is load-bearing: MenuAudio.PlayAudio dereferences an [Inject] AudioSystem,
                // and MenuAudio.PlayAudio is the ONE persistent onClick listener every GameCard's
                // Button carries.
                //
                // UnityEvent.Invoke runs PERSISTENT listeners BEFORE runtime ones
                // (InvokableCallList.PrepareInvoke: m_ExecutingCalls = persistent, then runtime)
                // and does not guard them, so the NullReferenceException from PlayAudio aborted
                // the invoke list before reaching the runtime listener this view attaches -
                // `() => SelectGame(game)`. The card rendered perfectly, reported itself
                // interactable, passed a raycast, played no sound, and opened no modal.
                //
                // That is why the failure read as POSITIONAL rather than as belonging to a mode:
                // only cards in a cloned row are un-injected, and only the first of them is ever
                // active at 13 modes. At 14 modes the second would have gone dead too.
                if (_container != null)
                    GameObjectInjector.InjectRecursive(row.gameObject, _container);
                else
                    CSDebug.LogErrorFormat(
                        "{0} - no Reflex container, so the new card row cannot be injected. Every " +
                        "card in it will swallow its own press (MenuAudio.PlayAudio throws on a " +
                        "null AudioSystem before SelectGame runs). Ensure the scene has a " +
                        "ContainerScope and that this view is injected.",
                        nameof(ArcadeExploreView));
            }
        }

        /// <summary>
        /// Size the scroll view's content to what it actually contains, so every card can be
        /// scrolled to and pressed.
        ///
        /// <para><b>Adding a row is not enough on its own, and the shortfall reads as three
        /// separate bugs.</b> The grid lives in a <see cref="ScrollRect"/> whose Content has a
        /// HARDCODED height (1104 in Menu_Main) and no <c>ContentSizeFitter</c>. A row that ends
        /// up below the reachable range is clipped by the viewport's <see cref="Mask"/>, which
        /// does two things to it: it cuts the drawing off (you see the top of a card and nothing
        /// under it), and - being an <c>ICanvasRaycastFilter</c> that rejects any point outside
        /// its own rect - it eats the PRESS as well. Meanwhile the scroll stops at the authored
        /// height, so dragging further springs back (MovementType is Elastic). Half a card, a
        /// scroll that snaps back, and a card that cannot be opened are one cause - which is why
        /// favouriting a mode "fixed" it: that only moved it out of the last slot and moved
        /// something else in.</para>
        ///
        /// <para><b>MEASURED, never incremented.</b> The first attempt grew Content by what the
        /// new rows cost (row height + the grid's spacing) - which assumes Content previously
        /// contained its children exactly, and it did not: the authored 1104 was already short of
        /// the three authored rows, so the increment landed short too and the card stayed out of
        /// reach. <see cref="RectTransformUtility.CalculateRelativeRectTransformBounds"/> reads
        /// the real extent of every active descendant at runtime, so this corrects the
        /// pre-existing shortfall and any future one without modelling the layout in code - the
        /// modelling is what got it wrong.</para>
        ///
        /// <para><b>And a measurement has to be taken to a FIXED POINT, because the Content's
        /// own layout group force-expands.</b> Under force-expand, height becomes spacing: the
        /// surplus over what the children need is shared out among them, pushing the grid down
        /// and demanding more height. One measure-and-set can therefore never catch up. The fix
        /// is to switch force-expand off on a scrolling Content - its job is to be as tall as
        /// its contents, not to distribute an authored height - and then iterate until nothing
        /// grows. <b>General rule for a future mode: adding a card is adding a ROW and a row is
        /// only reachable if the scroll content was measured after it, so never assume an
        /// authored content height contains what the scene authored into it.</b></para>
        ///
        /// <para>Only ever GROWS (never shrinks below the authored height), and uses
        /// <c>SetSizeWithCurrentAnchors</c> rather than writing <c>sizeDelta</c>: the grid is
        /// authored with fractional vertical anchors, where <c>sizeDelta</c> is an offset from
        /// the anchor span rather than a height, and a layout group rewrites those anchors at
        /// runtime. Deliberately NOT a <c>ContentSizeFitter</c>, which would re-derive the
        /// already-authored rows' height from their preferred sizes instead of the anchors the
        /// scene uses, changing the existing arcade layout.</para>
        /// </summary>
        void FitScrollContent()
        {
            var scroll = GameSelectionGrid != null
                ? GameSelectionGrid.GetComponentInParent<ScrollRect>()
                : null;
            if (scroll == null || scroll.content == null) return;

            // A scroll extent is not a layout frame. Pin first, THEN measure - otherwise the
            // measurement is of a layout that the measurement itself is about to move.
            for (int i = 0; i < scroll.content.childCount; i++)
                PinVerticalAnchorsToTop(scroll.content.GetChild(i) as RectTransform);

            if (GameSelectionGrid is RectTransform gridRect)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(gridRect);
                Fit(gridRect);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
            Fit(scroll.content);

            static void Fit(RectTransform rt)
            {
                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(rt);

                // Both of these are TOP-pivoted, so what has to be covered is how far the lowest
                // child reaches BELOW the origin - not the bounds' total height, which is short
                // by whatever sits above the pivot.
                float needed = Mathf.Max(bounds.size.y, -bounds.min.y);
                if (needed <= rt.rect.height + 0.5f) return;

                rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, needed);
            }
        }

        /// <summary>
        /// Re-anchor a rect to the TOP of its parent at the height it is drawing right now.
        /// Visually a no-op; what it changes is what happens NEXT time the parent's height moves.
        ///
        /// <para>The VERTICAL axis only. The horizontal anchors are left exactly as authored -
        /// the Maelstrom banner is deliberately anchored WIDER than the content (x 0.474 to
        /// 1.715) so it runs past the scroll view's right edge, and normalising that would move
        /// it on screen.</para>
        /// </summary>
        static void PinVerticalAnchorsToTop(RectTransform rt)
        {
            if (rt == null) return;
            if (rt.parent is not RectTransform parent) return;
            // Already top-anchored: nothing to preserve and nothing that can stretch.
            if (Mathf.Approximately(rt.anchorMin.y, 1f) && Mathf.Approximately(rt.anchorMax.y, 1f))
                return;

            float height = rt.rect.height;
            // localPosition is the PIVOT's position in the parent, and rect.yMax is the rect's
            // top relative to that pivot - so this is the rect's top edge in parent space.
            float drop = parent.rect.yMax - (rt.localPosition.y + rt.rect.yMax);

            rt.anchorMin = new Vector2(rt.anchorMin.x, 1f);
            rt.anchorMax = new Vector2(rt.anchorMax.x, 1f);
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, height);
            rt.anchoredPosition = new Vector2(
                rt.anchoredPosition.x, -(drop + (1f - rt.pivot.y) * height));
        }

        /// <summary>
        /// Report any card that a player cannot reach, by NAME, once per repopulate.
        ///
        /// <para>Every failure in this area was silent: a truncated roster left no gap in the
        /// grid, and a clipped row looked like a scroll that had reached its end. Both read as
        /// "that mode is not shipped yet". A card that is switched on but sits outside the
        /// scrollable range cannot be pressed - the viewport's Mask rejects the raycast - so it
        /// is a defect however it got there, and it says so.</para>
        /// </summary>
        void ReportUnreachableCards(int rosterCount)
        {
            if (GameCards == null) return;

            if (rosterCount > GameCards.Count)
            {
                CSDebug.LogErrorFormat(
                    "{0} - {1} arcade modes have no card slot ({2} slots). Grid growth failed; the last {3} are unreachable.",
                    nameof(ArcadeExploreView), rosterCount, GameCards.Count, rosterCount - GameCards.Count);
            }

            var scroll = GameSelectionGrid != null
                ? GameSelectionGrid.GetComponentInParent<ScrollRect>()
                : null;
            if (scroll == null || scroll.content == null) return;

            var content = scroll.content;
            float reach = content.rect.height;

            for (int i = 0; i < GameCards.Count; i++)
            {
                var card = GameCards[i];
                if (card == null || !card.gameObject.activeInHierarchy) continue;
                if (card.transform is not RectTransform cardRect) continue;

                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(content, cardRect);
                if (-bounds.min.y <= reach + 0.5f) continue;

                CSDebug.LogErrorFormat(
                    "{0} - The {1} card sits {2:0} units past the scroll content's {3:0}, so it cannot be scrolled to or pressed.",
                    nameof(ArcadeExploreView), card.GameMode, -bounds.min.y - reach, reach);
            }

            ReportCardPressability();
        }

        /// <summary>
        /// State the press path of the LAST card in the roster, once per repopulate.
        ///
        /// <para>The last slot is where a card lands when the roster outgrows the authored grid,
        /// and it is the slot that has been reported dead twice. Three passes of reasoning about
        /// the layout could not settle whether the press is being swallowed by geometry, by the
        /// button, or by the lock, because on screen all three look identical: nothing happens.
        /// So the view says which - the button's own state, and what a real
        /// <see cref="EventSystem"/> raycast at the card's centre actually lands on.</para>
        ///
        /// <para>Deliberately NOT an error: this is the one card whose press path is worth
        /// stating whether or not it is broken, so that "it works" is as loud as "it does not".
        /// It costs one raycast per repopulate, on the menu.</para>
        /// </summary>
        void ReportCardPressability()
        {
            if (GameCards == null) return;

            GameCard last = null;
            int lastIndex = -1;
            for (int i = 0; i < GameCards.Count; i++)
            {
                if (GameCards[i] != null && GameCards[i].gameObject.activeInHierarchy)
                {
                    last = GameCards[i];
                    lastIndex = i;
                }
            }
            if (last == null) return;

            string button = "no Button";
            if (last.TryGetComponent(out Button btn))
            {
                // Whether SelectGame was wired is recorded at the decision, not read back off the
                // Button: onClick can only report its PERSISTENT count, and SelectGame is added at
                // runtime - so the count reads the same on a wired card and an unwired one.
                button = $"interactable={btn.interactable}, " +
                         $"SelectGame listener={(_lockedSlots.Contains(lastIndex) ? "NO - progression locked" : "yes")}, " +
                         $"{btn.onClick.GetPersistentEventCount()} persistent";
            }

            string hit = "no EventSystem";
            var events = EventSystem.current;
            if (events != null && last.transform is RectTransform rect)
            {
                // The card's own centre, in screen space. A Screen Space - Overlay canvas takes a
                // null camera; anything else needs the canvas's own, so ask the canvas rather
                // than assuming Camera.main (which in this scene follows a vessel).
                var canvas = last.GetComponentInParent<Canvas>();
                Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                    ? canvas.worldCamera
                    : null;
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, rect.position);

                var ownScroll = GameSelectionGrid != null
                    ? GameSelectionGrid.GetComponentInParent<ScrollRect>()
                    : null;
                RectTransform scrollViewport = ownScroll != null ? ownScroll.viewport : null;

                var data = new PointerEventData(events) { position = screen };
                var results = new List<RaycastResult>();
                events.RaycastAll(data, results);

                // The hit is compared with IsChildOf, NOT with ==. A GameCard's own root Image is
                // authored m_RaycastTarget: 0, so the raycast ALWAYS lands on a descendant (the
                // Border) and never on the card object itself - an equality test made the success
                // branch unreachable, so this instrument reported "something is ON TOP of the card"
                // about a perfectly healthy card. It manufactured the overlay verdict that three
                // investigations then chased.
                bool outsideViewport = scrollViewport != null &&
                    !RectTransformUtility.RectangleContainsScreenPoint(scrollViewport, screen, cam);

                hit = results.Count == 0
                    ? (outsideViewport
                        ? "NOTHING - but the card is currently scrolled OUT OF the viewport, which is normal at scroll-top and says nothing about whether it can be pressed"
                        : "NOTHING, and the card IS inside the viewport - a Mask or a raycast filter is rejecting the point")
                    : results[0].gameObject.transform.IsChildOf(last.transform)
                        ? $"the card (via '{results[0].gameObject.name}')"
                        : $"'{results[0].gameObject.name}' ({results[0].gameObject.GetComponents<Component>().Length} components) - it is ON TOP of the card";
            }

            int active = 0;
            for (int i = 0; i < GameCards.Count; i++)
                if (GameCards[i] != null && GameCards[i].gameObject.activeInHierarchy) active++;

            CSDebug.LogFormat(
                "{0} - {1} active cards, {2} locked; last is {3} at slot {4}: {5}; a press at its centre lands on {6}.",
                nameof(ArcadeExploreView), active, _lockedSlots.Count, last.GameMode, lastIndex, button, hit);
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
            // The press itself, stated. Everything ReportCardPressability can measure is about the
            // card; this is the other half - whether the click ever ARRIVES. Without it a dead card
            // and a card whose modal declines to open are the same observation (nothing happens),
            // and they have nothing in common: one is the grid's problem, the other the modal's.
            CSDebug.LogFormat("{0} - card pressed: {1}. Handing it to the configure modal ({2}).",
                nameof(ArcadeExploreView),
                selectedGame ? selectedGame.DisplayName : "<null card>",
                ArcadeGameConfigureModal ? "wired" : "NOT WIRED - nothing can open");

            // Stating a fault and then dereferencing through it is worse than not stating it: the
            // NullReferenceException on the next line is what the reader sees, and it names the
            // field rather than the wiring.
            if (!ArcadeGameConfigureModal) return;

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
            var roster = Roster;
            if (roster?.Games == null) return null;

            for (int i = 0; i < roster.Games.Count; i++)
            {
                var game = roster.Games[i];
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
