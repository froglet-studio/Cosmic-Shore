using System;
using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The launch surface for an <b>Arena</b> card — a <see cref="MinigameLaunchPanel"/> whose one
    /// difference is that the pilot PICKS A HULL before they can start.
    ///
    /// <para>An arcade card locks to one vessel, so its panel has nothing to ask. An arena card
    /// can be flown in several, so this panel carries a vessel picker (the style-B hull strip, or
    /// the older carousel: one icon, prev / next) and a claim button, and Start stays dead until that button has been
    /// pressed; CHANGE takes the claim back until the pilot is ready. The
    /// choice is per session: it is re-armed every time the card opens, on the host and on every
    /// client alike, because each pilot picks their own hull.</para>
    ///
    /// <para>Same contract as every panel (<c>Docs/ArcadeLaunch/ARCHITECTURE.md</c> §2): the panel
    /// owns the widgets and RAISES; <see cref="ArcadeGameConfigureModal"/> owns the decision —
    /// which vessel is current, whether it is confirmed, and whether Start may fire. Nothing
    /// here writes <c>ArcadeGameConfigSO</c> or the player's vessel type.</para>
    ///
    /// <para>It lives in its OWN window (<see cref="ArcadeLaunchPanel.HostModal"/>) the way the
    /// Maelstrom's does, and it answers <see cref="Handles"/> for the cards in
    /// <see cref="roster"/> — the Arena's <c>SO_GameList</c> — and for any <see cref="SO_ArcadeGame.ArenaRules"/>
    /// card, listed or not (a voyage is on no grid). It must sit BEFORE the arcade's
    /// <see cref="MinigameLaunchPanel"/> in the modal's list, because that panel accepts every
    /// non-Maelstrom card and first match wins.</para>
    /// </summary>
    public class ArenaLaunchPanel : MinigameLaunchPanel
    {
        [Header("Arena")]
        [SerializeField, Tooltip("The Arena roster. This panel draws exactly the cards in this list.")]
        SO_GameList roster;

        [Header("Vessel picker")]
        [SerializeField, Tooltip("Shows the current vessel's IconActive.")]
        Image vesselIcon;

        [SerializeField, Tooltip("Steps the carousel backwards.")]
        Button prevButton;

        [SerializeField, Tooltip("Steps the carousel forwards.")]
        Button nextButton;

        [SerializeField, Tooltip("Confirms the shown vessel. Hidden once pressed; Start is dead " +
                                 "until it has been.")]
        Button selectVesselButton;

        [SerializeField, Tooltip("Takes the claim back so the pilot can pick another hull: shown in the claim " +
                                 "button's place once the pick is confirmed, until the pilot is ready. Optional.")]
        Button changeVesselButton;

        [Header("Hull strip (optional)")]
        [SerializeField, Tooltip("The tiles of the hull strip, left to right (the style-B configure tree). " +
                                 "They show a window onto the card's hulls in roster order: a card with more " +
                                 "hulls than tiles slides the window to keep the current pick in view, so the " +
                                 "prev / next arrows reach every hull. A tap shows that hull; a hull another " +
                                 "pilot holds is drawn locked and refuses. Surplus tiles are hidden. Optional: " +
                                 "the carousel arrows work without it.")]
        List<KitHullTile> hullTiles = new();

        [Header("Shared vessel panel")]
        [SerializeField, Tooltip("The vessel section's caption. It names whose hull the panel is picking: the " +
                                 "pilot's own, or the AI seat they tapped. Optional.")]
        TMPro.TMP_Text seatCaptionText;

        [SerializeField, Tooltip("The caption while the panel picks the pilot's own hull. Empty: no caption.")]
        string ownSeatCaption = "Your vessel";

        [SerializeField, Tooltip("The caption while the panel picks an AI seat's hull. {domain} is the AI's team. " +
                                 "Empty: no caption.")]
        string aiSeatCaption = "{domain} AI's vessel";

        [SerializeField, Tooltip("What the vessel section names when the selected AI has no hull yet (dealt at " +
                                 "launch from the hulls left). Empty: nothing.")]
        string autoVesselLine = "Auto";

        [SerializeField, Tooltip("The vessel section's own hull labels (the section value, the class name). While " +
                                 "an AI seat is selected only these follow its hull; the pilot's summary chips, " +
                                 "FLY row and controls keep naming the pilot's own.")]
        List<TMPro.TMP_Text> seatVesselNameTexts = new();

        [Header("Vessels row (replaces the intensity row on an arena card)")]
        [SerializeField, Tooltip("The section caption while the row shows the vessel selection (it read 'Intensity'). " +
                                 "Empty: the caption is left as authored.")]
        string vesselsCaption = "Vessels";

        [SerializeField, Tooltip("The name of each selection mode, in VesselSelectionMode order: Pick, Random, Draft, " +
                                 "Squad. An empty entry shows nothing for that mode.")]
        string[] selectionModeNames = { "Pick", "Random", "Draft", "Squad" };

        [SerializeField, Tooltip("The row's line under the caption for each mode, in the same order: what the mode " +
                                 "does. Replaced by the draft's turn or the dealt hull while one applies. Empty: nothing.")]
        string[] selectionModeHints =
        {
            "Choose your vessel",
            "Every pilot is dealt a vessel",
            "Take turns picking",
            "Dealt one vessel from your squad",
        };

        [SerializeField, Tooltip("The row's mode label. Optional: when empty the panel builds one in the intensity " +
                                 "stepper's place the first time an arena card opens, with the stepper's own LB / RB " +
                                 "buttons copied either side of it.")]
        TMP_Text selectionModeText;

        [SerializeField, Tooltip("Steps the mode back. Optional, built with the label when empty.")]
        Button selectionPrevButton;

        [SerializeField, Tooltip("Steps the mode on. Optional, built with the label when empty.")]
        Button selectionNextButton;

        [SerializeField, Tooltip("The line beside the caption: the mode's hint, the draft's turn, the dealt hull. " +
                                 "Optional: when empty the intensity section's own hint is used.")]
        TMP_Text selectionStatusText;

        // The card hull the leftmost tile shows (StripOffset).
        int _stripOffset;

        // The intensity section the Vessels row takes over, found once (the configure prefab is generated, so the row
        // is built at runtime rather than authored into it).
        bool _selectionRowResolved;
        GameObject _selectionRow;
        GameObject _intensityBody;
        TMP_Text _sectionCaption;
        string _sectionCaptionAuthored;
        bool _selectionRowShown;
        bool _selectionInteractable;

        /// <summary>+1 / -1: the pilot asked for the next / previous vessel.</summary>
        public event Action<int> OnVesselCycleRequested;

        /// <summary>The pilot tapped hull tile <c>index</c> (an index into the card's vessel list).</summary>
        public event Action<int> OnVesselPickRequested;

        /// <summary>The pilot confirmed the vessel currently shown.</summary>
        public event Action OnVesselConfirmRequested;

        /// <summary>The pilot wants to pick again after confirming (CHANGE).</summary>
        public event Action OnVesselChangeRequested;

        /// <summary>+1 / -1: the host stepped the Vessels row (the selection mode).</summary>
        public event Action<int> OnVesselSelectionStepRequested;

        // An ArenaRules card is an arena card wherever it is listed: a voyage (Tandava) is on no grid, so the roster
        // alone sent it to the arcade's one-hull panel and its pilots could not pick a vessel (Shombith, 2026-10-26).
        public override bool Handles(SO_ArcadeGame game)
            => game != null && (game.ArenaRules || (roster != null && roster.Games != null && roster.Games.Contains(game)));

        public override void Bind(SO_ArcadeGame game, int intensity)
        {
            _controlsVessel = null;
            _stripOffset = 0;
            base.Bind(game, intensity);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (prevButton)         prevButton.onClick.AddListener(RequestPrev);
            if (nextButton)         nextButton.onClick.AddListener(RequestNext);
            if (selectVesselButton) selectVesselButton.onClick.AddListener(RequestConfirm);
            if (changeVesselButton) changeVesselButton.onClick.AddListener(RequestChange);
            for (int i = 0; i < hullTiles.Count; i++)
                if (hullTiles[i]) hullTiles[i].Bind(i, RequestTile);
            if (selectionPrevButton) selectionPrevButton.onClick.AddListener(RequestSelectionPrev);
            if (selectionNextButton) selectionNextButton.onClick.AddListener(RequestSelectionNext);
        }

        protected override void OnDisable()
        {
            if (prevButton)         prevButton.onClick.RemoveListener(RequestPrev);
            if (nextButton)         nextButton.onClick.RemoveListener(RequestNext);
            if (selectVesselButton) selectVesselButton.onClick.RemoveListener(RequestConfirm);
            if (changeVesselButton) changeVesselButton.onClick.RemoveListener(RequestChange);
            foreach (var tile in hullTiles)
                if (tile) tile.Bind(-1, null);
            if (selectionPrevButton) selectionPrevButton.onClick.RemoveListener(RequestSelectionPrev);
            if (selectionNextButton) selectionNextButton.onClick.RemoveListener(RequestSelectionNext);
            base.OnDisable();
        }

        void Update()
        {
            // LB / RB step the selection mode, as they stepped the intensity this row replaced (the stepper's own
            // shoulder read is off while the row is shown). The preview holding the stick owns the pad.
            if (!_selectionRowShown || !_selectionInteractable || ModePreviewWindow.AnyHasFocus) return;
            var pad = Gamepad.current;
            if (pad == null) return;
            if (pad.rightShoulder.wasPressedThisFrame) RequestSelectionNext();
            else if (pad.leftShoulder.wasPressedThisFrame) RequestSelectionPrev();
        }

        /// <summary>
        /// Show the Vessels row in the intensity row's place (<paramref name="shown"/>), naming
        /// <paramref name="mode"/>, with <paramref name="status"/> beside the caption (the mode's own hint when null).
        /// <paramref name="interactable"/> is false on a guest, who sees the host's pick. Hidden, the intensity row
        /// comes back exactly as authored.
        /// </summary>
        public void ShowVesselSelection(bool shown, VesselSelectionMode mode, bool interactable, string status = null)
        {
            ResolveSelectionRow();
            _selectionRowShown = shown && _selectionRow;
            _selectionInteractable = interactable;

            if (_intensityBody) _intensityBody.SetActive(!_selectionRowShown);
            if (_selectionRow) _selectionRow.SetActive(_selectionRowShown);
            if (_intensityBody && _intensityBody.TryGetComponent(out IntensityStepper stepper))
                stepper.enabled = !_selectionRowShown;   // its shoulder read would step a hidden row

            if (_sectionCaption)
                _sectionCaption.text = _selectionRowShown && !string.IsNullOrEmpty(vesselsCaption)
                    ? vesselsCaption
                    : _sectionCaptionAuthored;

            if (!_selectionRowShown) return;

            int index = (int)mode - 1;
            if (selectionModeText)
                selectionModeText.text = index >= 0 && index < selectionModeNames.Length
                    ? TemplateText.Fill(selectionModeNames[index])
                    : string.Empty;
            if (selectionPrevButton) selectionPrevButton.interactable = interactable;
            if (selectionNextButton) selectionNextButton.interactable = interactable;
            if (selectionStatusText)
                selectionStatusText.text = status ?? (index >= 0 && index < selectionModeHints.Length
                    ? TemplateText.Fill(selectionModeHints[index])
                    : string.Empty);
        }

        /// <summary>
        /// Find the intensity section and build the Vessels row in its stepper's place, once. The configure window is a
        /// generated prefab (Tools/Build/author_menu_configure.py), so the row is assembled from parts already in it:
        /// the stepper's LB / RB buttons (copied) and a label in the stepper hint's font. Every field wired in the
        /// inspector wins over what this finds.
        /// </summary>
        void ResolveSelectionRow()
        {
            if (_selectionRowResolved) return;
            _selectionRowResolved = true;

            var stepper = GetComponentInChildren<IntensityStepper>(true);
            if (!stepper)
            {
                CSDebug.LogWarning("[ArenaLaunchPanel] No intensity stepper to put the Vessels row in - the arena card " +
                                   "keeps its intensity row and opens on Pick.");
                return;
            }

            _intensityBody = stepper.gameObject;
            var section = stepper.transform.parent;
            _sectionCaption = FindText(section, "Caption");
            _sectionCaptionAuthored = _sectionCaption ? _sectionCaption.text : null;
            if (!selectionStatusText) selectionStatusText = FindText(section, "Hint");

            if (selectionModeText && selectionPrevButton && selectionNextButton)
            {
                _selectionRow = selectionModeText.transform.parent ? selectionModeText.transform.parent.gameObject : null;
                return;
            }

            var bodyRect = (RectTransform)stepper.transform;
            var row = new GameObject("VesselsRow", typeof(RectTransform));
            var rowRect = (RectTransform)row.transform;
            rowRect.SetParent(section, false);
            CopyRect(bodyRect, rowRect);
            rowRect.SetSiblingIndex(bodyRect.GetSiblingIndex() + 1);

            Button prevSource = null, nextSource = null;
            foreach (var b in stepper.GetComponentsInChildren<Button>(true))
            {
                if (b.name == "Previous") prevSource = b;
                else if (b.name == "Next") nextSource = b;
            }

            float left = 0f, right = 0f;
            if (!selectionPrevButton && prevSource)
            {
                selectionPrevButton = CopyButton(prevSource, rowRect);
                left = ((RectTransform)prevSource.transform).rect.width + 12f;
            }
            if (!selectionNextButton && nextSource)
            {
                selectionNextButton = CopyButton(nextSource, rowRect);
                var next = (RectTransform)selectionNextButton.transform;
                // Right after the label rather than where the stepper's pips ended.
                next.anchorMin = next.anchorMax = new Vector2(1f, 0.5f);
                next.pivot = new Vector2(1f, 0.5f);
                next.anchoredPosition = Vector2.zero;
                right = next.rect.width + 12f;
            }

            if (!selectionModeText)
            {
                var labelGo = new GameObject("Mode", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                var labelRect = (RectTransform)labelGo.transform;
                labelRect.SetParent(rowRect, false);
                labelRect.anchorMin = new Vector2(0f, 0f);
                labelRect.anchorMax = new Vector2(1f, 1f);
                labelRect.offsetMin = new Vector2(left, 0f);
                labelRect.offsetMax = new Vector2(-right, 0f);
                var tmp = labelGo.GetComponent<TextMeshProUGUI>();
                var style = _sectionCaption ? _sectionCaption : selectionStatusText;
                if (style)
                {
                    tmp.font = style.font;
                    tmp.fontSharedMaterial = style.fontSharedMaterial;
                    tmp.color = style.color;
                    tmp.fontSize = style.fontSize * 1.4f;
                }
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.textWrappingMode = TextWrappingModes.NoWrap;
                tmp.raycastTarget = false;
                selectionModeText = tmp;
            }

            if (isActiveAndEnabled)
            {
                if (selectionPrevButton) selectionPrevButton.onClick.AddListener(RequestSelectionPrev);
                if (selectionNextButton) selectionNextButton.onClick.AddListener(RequestSelectionNext);
            }

            _selectionRow = row;
            row.SetActive(false);
        }

        static TMP_Text FindText(Transform root, string name)
        {
            if (!root) return null;
            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child.name == name && child.TryGetComponent(out TMP_Text text)) return text;
            }
            return null;
        }

        static void CopyRect(RectTransform from, RectTransform to)
        {
            to.anchorMin = from.anchorMin;
            to.anchorMax = from.anchorMax;
            to.pivot = from.pivot;
            to.anchoredPosition = from.anchoredPosition;
            to.sizeDelta = from.sizeDelta;
        }

        static Button CopyButton(Button source, RectTransform parent)
        {
            var copy = Instantiate(source, parent, false);
            copy.name = source.name;
            copy.onClick.RemoveAllListeners();   // the copy steps the mode, never the intensity
            CopyRect((RectTransform)source.transform, (RectTransform)copy.transform);
            copy.gameObject.SetActive(true);
            return copy;
        }

        void RequestSelectionPrev() { if (_selectionInteractable) OnVesselSelectionStepRequested?.Invoke(-1); }
        void RequestSelectionNext() { if (_selectionInteractable) OnVesselSelectionStepRequested?.Invoke(+1); }

        /// <summary>Name whose hull the vessel section shows, overriding the own / AI seat captions: the chosen
        /// squad in Squad mode, the draft's turn. Null puts the seat caption back.</summary>
        public void ShowVesselCaption(string caption)
        {
            if (seatCaptionText && caption != null) seatCaptionText.text = caption;
        }

        /// <summary>
        /// Draw the picker. <paramref name="canCycle"/> is false when there is nothing to step
        /// through (one hull, or none); <paramref name="confirmed"/> hides the Select button and the
        /// arrows, and <paramref name="canChange"/> then offers CHANGE in its place (until the pilot is ready).
        /// </summary>
        public void ShowVessel(SO_Vessel vessel, bool canCycle, bool confirmed, bool canChange = false)
        {
            if (vesselIcon)
            {
                vesselIcon.sprite  = vessel ? vessel.IconActive : null;
                vesselIcon.enabled = vessel;
            }

            bool arrows = vessel && canCycle && !confirmed;
            if (prevButton) prevButton.interactable = arrows;
            if (nextButton) nextButton.interactable = arrows;
            ShowArrows(!confirmed);

            if (selectVesselButton)
            {
                SetPartShown(selectVesselButton.gameObject, vessel && !confirmed);
                selectVesselButton.interactable = vessel && !confirmed;
            }
            ShowChange(vessel && confirmed && canChange);

            DrawVessel(vessel);
            if (seatCaptionText) seatCaptionText.text = TemplateText.Fill(ownSeatCaption);

            // The controls block describes the hull this pilot will fly, so it follows the strip.
            if (vessel && vessel != _controlsVessel)
            {
                _controlsVessel = vessel;
                ShowControlsFor(vessel);
            }
        }

        SO_Vessel _controlsVessel;

        /// <summary>
        /// Draw an AI seat's hull in the shared vessel panel (the pilot tapped that seat): the caption names
        /// the seat, the section's own labels and description show the hull (<see cref="autoVesselLine"/> when
        /// none is picked yet), the arrows step it when <paramref name="canCycle"/>, and the claim button stands
        /// aside - an AI's pick lands as it is made. The pilot's own summary, FLY row and controls are untouched.
        /// </summary>
        public void ShowSeatVessel(SO_Vessel vessel, string seatDomain, bool canCycle)
        {
            if (seatCaptionText) seatCaptionText.text = TemplateText.Fill(aiSeatCaption, "domain", seatDomain);

            if (vesselIcon)
            {
                vesselIcon.sprite  = vessel ? vessel.IconActive : null;
                vesselIcon.enabled = vessel;
            }

            if (prevButton) prevButton.interactable = canCycle;
            if (nextButton) nextButton.interactable = canCycle;
            ShowArrows(true);

            if (selectVesselButton)
            {
                SetPartShown(selectVesselButton.gameObject, false);
                selectVesselButton.interactable = false;
            }
            ShowChange(false);

            string name = vessel ? vessel.Name : TemplateText.Fill(autoVesselLine);
            foreach (var label in seatVesselNameTexts)
                if (label) label.text = name;
            if (vesselDescriptionText) vesselDescriptionText.text = vessel ? vessel.Description : string.Empty;
        }

        /// <summary>
        /// Draw the hull strip: the tiles show a window onto <paramref name="ships"/> that keeps
        /// <paramref name="shown"/> in view (selected), a hull <paramref name="heldByOthers"/> names drawn
        /// locked. Once the pick is <paramref name="confirmed"/> the strip stops taking taps. A panel
        /// without tiles ignores it.
        /// </summary>
        public void ShowHullStrip(IReadOnlyList<SO_Vessel> ships, int shown, Func<SO_Vessel, bool> heldByOthers,
                                  bool confirmed)
        {
            int count = ships?.Count ?? 0;
            _stripOffset = StripOffset(_stripOffset, shown, count, hullTiles.Count);
            for (int i = 0; i < hullTiles.Count; i++)
            {
                var tile = hullTiles[i];
                if (!tile) continue;
                int index = _stripOffset + i;
                var ship = index < count ? ships[index] : null;
                tile.Show(ship, index == shown, ship && heldByOthers != null && heldByOthers(ship), !confirmed);
            }
        }

        /// <summary>
        /// The card hull the leftmost of <paramref name="tiles"/> tiles shows: unchanged while
        /// <paramref name="shown"/> is in view, slid just far enough to bring it in otherwise, and never past
        /// either end. Zero when every hull fits. The Regatta card lists nine hulls on an eight-tile strip,
        /// and with a fixed strip the ninth (the Stoat) had no tile and could never be picked (2026-10-25).
        /// </summary>
        public static int StripOffset(int offset, int shown, int count, int tiles)
        {
            if (tiles <= 0 || count <= tiles) return 0;
            if (shown >= 0 && shown < count)
            {
                if (shown < offset) offset = shown;
                else if (shown >= offset + tiles) offset = shown - tiles + 1;
            }
            return Mathf.Clamp(offset, 0, count - tiles);
        }

        // A confirmed pick has nothing to step: the arrows leave rather than sit there greyed.
        void ShowArrows(bool shown)
        {
            if (prevButton) SetPartShown(prevButton.gameObject, shown);
            if (nextButton) SetPartShown(nextButton.gameObject, shown);
        }

        void ShowChange(bool shown)
        {
            if (!changeVesselButton) return;
            SetPartShown(changeVesselButton.gameObject, shown);
            changeVesselButton.interactable = shown;
        }

        void RequestPrev()    => OnVesselCycleRequested?.Invoke(-1);
        void RequestNext()    => OnVesselCycleRequested?.Invoke(+1);
        void RequestConfirm() => OnVesselConfirmRequested?.Invoke();
        void RequestChange()  => OnVesselChangeRequested?.Invoke();
        void RequestTile(int tile) => OnVesselPickRequested?.Invoke(_stripOffset + tile);
    }
}
