using CosmicShore.Core;
using CosmicShore.UI;
using CosmicShore.Utility;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Hangar: one view in three zones (style-B menu redesign, item 2) - the vessel list, the
    /// selected vessel and its abilities. Picking a list tile, by click or by gamepad / keyboard focus,
    /// selects that vessel; there is no grid-to-detail hop and no Back button.
    /// </summary>
    public class HangarScreen : MonoBehaviour, IScreen
    {
        [Header("Data")]
        [SerializeField] private SO_VesselList ShipList;

        [Header("Vessel List")]
        [Tooltip("Content of the list's ScrollRect. Tiles are built here, owned vessels first.")]
        [SerializeField] private RectTransform vesselList;
        [Tooltip("Scrolls a focused tile into view when the roster outgrows the list.")]
        [SerializeField] private ScrollRect vesselScroll;
        [Tooltip("One row per vessel (HangarVesselTile.prefab, a variant of KitTile).")]
        [SerializeField] private HangarVesselTile vesselTilePrefab;
        [Tooltip("\"1 / 11 owned\" beside the list's label.")]
        [SerializeField] private KitText ownedCountText;
        [Tooltip("The owned count's line. {owned} is the hulls unlocked, {total} the hulls listed. Empty shows nothing.")]
        [SerializeField] private string ownedCountLine = "{owned} / {total} owned";

        [Header("List Animation")]
        [Tooltip("Delay in seconds between each tile's fade-in.")]
        [SerializeField] private float cardStaggerDelay = 0.08f;
        [Tooltip("Duration of each tile's fade-in.")]
        [SerializeField] private float cardFadeDuration = 0.25f;

        [Header("Selected Vessel")]
        [Tooltip("The centre and right zones plus the spend-crystals popup.")]
        [SerializeField] private HangarVesselDetailView detailView;

        [Header("Legacy Views (kept for backward compatibility)")]
        [SerializeField] private HangarOverviewView OverviewView;
        [SerializeField] private HangarAbilitiesView AbilitiesView;
        [SerializeField] private HangarTrainingModal HangarTrainingModal;
        [SerializeField] private NavGroup TopNav;

        [Header("Legacy Ship Selection (deprecated)")]
        [SerializeField] private Transform ShipSelectionContainer;
        [SerializeField] private InfiniteScroll ShipSelectionScrollView;
        [SerializeField] private HangarShipSelectNavLink ShipSelectCardPrefab;

        [Header("Legacy Training UI")]
        [SerializeField] private Transform GameSelectionContainer;
        [SerializeField] private Image ShipModelImage;
        [SerializeField] private TMPro.TMP_Text SelectedGameName;
        [SerializeField] private TMPro.TMP_Text SelectedGameDescription;
        [SerializeField] private GameObject SelectedGamePreviewWindow;

        static readonly Vector3[] Corners = new Vector3[4];

        List<SO_Vessel> Ships;
        SO_Vessel SelectedShip;
        readonly List<HangarVesselTile> _tiles = new();
        Coroutine _listAnimCoroutine;

        // ScreenSwitcher.NavigateTo loads the hangar twice in the same frame (once via the
        // explicit HangarMenu.LoadView() call and once via IScreen.OnScreenEnter()). A second
        // PopulateList in the same frame destroys the tiles the first run just instantiated
        // while their fade-in coroutines are still running. Coalesce calls per frame so the
        // list is built exactly once regardless of how many callers fire.
        int _lastLoadFrame = -1;

        bool UsesList => vesselList && vesselTilePrefab && detailView;

        void OnEnable()
        {
            VesselUnlockSystem.OnUnlockStateChanged += RebuildList;
            // Flipping the master developer unlock changes every tile's lock state and the sort.
            DeveloperUnlockGate.OnChanged += RebuildList;
        }

        void OnDisable()
        {
            VesselUnlockSystem.OnUnlockStateChanged -= RebuildList;
            DeveloperUnlockGate.OnChanged -= RebuildList;
        }

        public void OnScreenEnter()
        {
            LoadView();
        }

        public void OnScreenExit() { }

        public void LoadView()
        {
            if (_lastLoadFrame == Time.frameCount)
                return;
            _lastLoadFrame = Time.frameCount;

            Ships = ShipList.VesselList;

            if (UsesList)
            {
                detailView.ReturnFocus = FocusSelectedTile;
                PopulateList();
                SelectVesselForDetail(Ships.Contains(SelectedShip) ? SelectedShip : FirstListedVessel());
                AnimateListIn();
            }
            else
            {
                // Legacy flow fallback
                LoadViewLegacy();
            }
        }

        #region Vessel List

        SO_Vessel FirstListedVessel() => _tiles.Count > 0 ? _tiles[0].Vessel : null;

        void PopulateList()
        {
            _tiles.Clear();
            for (int i = vesselList.childCount - 1; i >= 0; i--)
            {
                // Destroy lands at the end of the frame. Deactivate first so the dying tiles leave
                // the layout now and ScreenSwitcher.Refocus cannot put the pad on one of them.
                var old = vesselList.GetChild(i).gameObject;
                old.SetActive(false);
                Destroy(old);
            }

            // Owned vessels first, then locked; otherwise the roster's own order.
            var sorted = Ships.Where(s => s).OrderBy(s => s.IsLocked ? 1 : 0).ToList();

            foreach (var ship in sorted)
            {
                var tile = Instantiate(vesselTilePrefab, vesselList);
                tile.name = $"VesselTile_{ship.Name}";
                tile.Configure(ship, this);
                tile.IsSelected = ship == SelectedShip;
                _tiles.Add(tile);
            }

            if (ownedCountText)
                ownedCountText.SetText(TemplateText.Fill(ownedCountLine,
                    "owned", sorted.Count(s => !s.IsLocked).ToString(), "total", sorted.Count.ToString()));
        }

        void RebuildList()
        {
            if (!UsesList || Ships == null) return;

            // A rebuild destroys the tile that may hold gamepad focus; put focus back on the
            // selected vessel's new tile, but only when focus was on a tile of this screen.
            var current = EventSystem.current ? EventSystem.current.currentSelectedGameObject : null;
            bool focusOnTile = current && current.transform.IsChildOf(vesselList);

            PopulateList();
            if (SelectedShip) SelectVesselForDetail(SelectedShip);
            if (focusOnTile) FocusSelectedTile();
        }

        /// <summary>Called by a tile when it is clicked or receives focus.</summary>
        public void OnTileFocused(HangarVesselTile tile)
        {
            if (!tile || !tile.Vessel) return;
            if (tile.Vessel != SelectedShip) SelectVesselForDetail(tile.Vessel);
            ScrollIntoView(tile);
        }

        /// <summary>Puts gamepad / keyboard focus on the selected vessel's tile.</summary>
        public void FocusSelectedTile()
        {
            var es = EventSystem.current;
            if (!es) return;
            foreach (var tile in _tiles)
            {
                if (tile && tile.Vessel == SelectedShip)
                {
                    es.SetSelectedGameObject(tile.gameObject);
                    return;
                }
            }
        }

        void ScrollIntoView(HangarVesselTile tile)
        {
            if (!vesselScroll) return;
            var viewport = vesselScroll.viewport ? vesselScroll.viewport : (RectTransform)vesselScroll.transform;
            float contentHeight = vesselList.rect.height;
            float viewHeight = viewport.rect.height;
            if (contentHeight <= viewHeight) return;

            // Distances from the content's top edge, in its own space.
            ((RectTransform)tile.transform).GetWorldCorners(Corners);
            float top = vesselList.rect.yMax - vesselList.InverseTransformPoint(Corners[1]).y;
            float bottom = vesselList.rect.yMax - vesselList.InverseTransformPoint(Corners[0]).y;

            float scrollable = contentHeight - viewHeight;
            float offset = (1f - vesselScroll.verticalNormalizedPosition) * scrollable;
            if (top < offset) offset = top;
            else if (bottom > offset + viewHeight) offset = bottom - viewHeight;
            vesselScroll.verticalNormalizedPosition = 1f - Mathf.Clamp01(offset / scrollable);
        }

        void AnimateListIn()
        {
            if (_listAnimCoroutine != null)
                StopCoroutine(_listAnimCoroutine);
            _listAnimCoroutine = StartCoroutine(AnimateTilesIn());
        }

        IEnumerator AnimateTilesIn()
        {
            foreach (var tile in _tiles)
                if (tile) tile.SetAlpha(0f);

            for (int i = 0; i < _tiles.Count; i++)
            {
                var tile = _tiles[i];
                if (!tile) continue;

                StartCoroutine(FadeTile(tile, 0f, 1f, cardFadeDuration));
                yield return new WaitForSecondsRealtime(cardStaggerDelay);
            }

            _listAnimCoroutine = null;
        }

        IEnumerator FadeTile(HangarVesselTile tile, float from, float to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (!tile) yield break;
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                tile.SetAlpha(Mathf.Lerp(from, to, t));
                yield return null;
            }
            if (tile) tile.SetAlpha(to);
        }

        #endregion

        #region Selected Vessel

        /// <summary>Selects a vessel: its tile is marked and the centre and right zones show it.</summary>
        public void SelectVesselForDetail(SO_Vessel ship)
        {
            if (ship == null) return;

            SelectedShip = ship;

            foreach (var tile in _tiles)
                if (tile) tile.IsSelected = tile.Vessel == ship;

            if (detailView)
                detailView.SetVessel(ship);

            UpdateLegacyViews(ship);
        }

        #endregion

        #region Legacy Support

        void LoadViewLegacy()
        {
            if (OverviewView)
                OverviewView.AssignModels(Ships.ConvertAll(x => (ScriptableObject)x));
            PopulateShipSelectionList();
        }

        void PopulateShipSelectionList()
        {
            if (ShipSelectionContainer == null)
            {
                CSDebug.LogError($"SerializedField 'ShipSelectionContainer' has not been assigned in HangarMenu");
                return;
            }

            for (var i = 0; i < ShipSelectionContainer.childCount; i++)
            {
                var child = ShipSelectionContainer.GetChild(i);
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }

            for (var i = 0; i < Ships.Count; i++)
            {
                var ship = Ships[i];
                var shipSelectCard = Instantiate(ShipSelectCardPrefab, ShipSelectionContainer.transform);
                shipSelectCard.name = shipSelectCard.name.Replace("(Clone)", "");
                shipSelectCard.AssignShipClass(ship);
                shipSelectCard.AssignIndex(i);
                shipSelectCard.HangarMenu = this;
            }

            if (ShipSelectionScrollView)
                ShipSelectionScrollView.Initialize(true);

            StartCoroutine(SelectFirstShipCoroutine());
        }

        public void SelectShip(int index)
        {
            var selectedShip = Ships[index];

            if (ShipSelectionContainer)
            {
                for (var i = 0; i < ShipSelectionContainer.childCount; i++)
                {
                    var selectCard = ShipSelectionContainer.GetChild(i).gameObject.GetComponent<HangarShipSelectNavLink>();
                    selectCard.SetActive(selectCard.Ship == selectedShip);
                }
            }

            SelectedShip = selectedShip;
            UpdateLegacyViews(selectedShip);
        }

        void UpdateLegacyViews(SO_Vessel ship)
        {
            if (!ship) return;

            int shipIndex = Ships.IndexOf(ship);

            if (OverviewView && shipIndex >= 0)
                OverviewView.Select(shipIndex);

            if (AbilitiesView && ship.Abilities != null)
            {
                foreach (var ability in ship.Abilities)
                    ability.Vessel = ship;
                AbilitiesView.AssignModels(ship.Abilities.ConvertAll(x => (ScriptableObject)x));
            }
        }

        public void DisplayTrainingModal()
        {
            if (SelectedShip == null || SelectedShip.IsLocked) return;

            if (HangarTrainingModal && SelectedShip.TrainingGames != null)
            {
                HangarTrainingModal.SetTrainingGames(SelectedShip.TrainingGames);
                HangarTrainingModal.ModalWindowIn();
            }
        }

        IEnumerator SelectFirstShipCoroutine()
        {
            yield return null;
            if (ShipSelectionContainer && ShipSelectionContainer.childCount > 0)
            {
                var shipSelectCard = ShipSelectionContainer.GetChild(0).gameObject.GetComponent<HangarShipSelectNavLink>();
                shipSelectCard.Select();
            }
        }

        #endregion
    }
}
