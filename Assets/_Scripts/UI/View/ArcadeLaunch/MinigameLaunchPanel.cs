using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// The launch panel for a mode with an arena of its own: the game already playing in the
    /// preview window, its intensity, its briefing, the hull's controls, the roster, and Start —
    /// all at once.
    ///
    /// <para>This is the panel the old two-screen flow collapsed into. The second screen existed to
    /// pick a vessel; every arcade mode locks to one now, so the hull is a FACT about the card and
    /// the panel simply states it — which is what the controls block is for.</para>
    ///
    /// <para><b>Intensity redraws the arena.</b> The preview is the mode's real cell, so changing
    /// intensity changes what the window shows; the panel re-arms the session rather than leaving
    /// a stale world under a changed number. The window itself never resizes and the panel never
    /// closes — see <c>Docs/ModePreview/ARCHITECTURE.md</c>.</para>
    /// </summary>
    public class MinigameLaunchPanel : ArcadeLaunchPanel
    {
        [Header("Preview")]
        [SerializeField, Tooltip("The live preview window. The mode's own arena plays in it under " +
                                 "AI; tapping it hands the player the stick.")]
        ModePreviewWindow previewWindow;

        [Header("Controls")]
        [SerializeField, Tooltip("The hull's abilities and the controls that fire them. Built from " +
                                 "the vessel's own ElementalAbilityMap, so nothing here is authored " +
                                 "per mode.")]
        VesselControlsPanel controlsPanel;

        [SerializeField, Tooltip("The OBJECTIVE box: the metric's icon, how you win, and a live " +
                                 "counter with no target. Optional - a panel without one shows " +
                                 "no objective box.")]
        ObjectiveBoxView objectiveBox;

        [SerializeField, Tooltip("The '+1' toast, authored at the same screen position the " +
                                 "IN-GAME toast feed uses. Optional.")]
        PreviewMicroToast microToast;

        [Header("AI difficulty row")]
        [SerializeField, Tooltip("Canvas pixels between the bottom of the AI difficulty row and the " +
                                 "top of the controls block while the row is shown. The block's " +
                                 "top is otherwise exactly where it was authored.")]
        float controlsGapUnderDifficultyRow = 10f;

        Sprite _metricIcon;
        int _objectiveCount;

        // The controls block's AUTHORED top edge (RectTransform.offsetMax.y), captured the first
        // time the difficulty row is shown, so hiding the row puts the block back where the
        // prefab has it rather than where the last Skim Race card left it.
        float _controlsAuthoredTop;
        bool _controlsTopCaptured;
        static readonly Vector3[] RowCorners = new Vector3[4];

        public override ModePreviewWindow PreviewWindow => previewWindow;

        /// <summary>
        /// Every card except the meta-mode. Maelstrom has no arena of its own, so it gets
        /// <see cref="MaelstromLaunchPanel"/> instead.
        /// </summary>
        public override bool Handles(SO_ArcadeGame game)
            => game != null && game.Mode != GameModes.Maelstrom;

        /// <summary>
        /// The difficulty row sits directly under the intensity row, which is where the controls
        /// block starts - so while the row is shown the block's top moves down to clear it, and it
        /// goes back the moment a card without the row opens. The block is a scroll view, so the
        /// only cost is a shorter viewport on the cards that offer difficulty.
        /// </summary>
        public override void SetAIDifficultyAvailable(bool available)
        {
            base.SetAIDifficultyAvailable(available);
            FitControlsUnderDifficultyRow(available);
        }

        void FitControlsUnderDifficultyRow(bool rowShown)
        {
            if (!controlsPanel || !aiDifficultyPicker) return;
            if (controlsPanel.transform is not RectTransform block) return;
            if (block.parent is not RectTransform parent) return;

            if (!_controlsTopCaptured)
            {
                _controlsAuthoredTop = block.offsetMax.y;
                _controlsTopCaptured = true;
            }

            float top = _controlsAuthoredTop;
            if (rowShown && aiDifficultyPicker.transform is RectTransform row)
            {
                // Measured, not authored as a number: the row's bottom edge in the block's own
                // parent space, so moving or resizing the row in the prefab needs no second edit
                // here. offsetMax is relative to the block's top anchor.
                row.GetWorldCorners(RowCorners);
                float rowBottom = parent.InverseTransformPoint(RowCorners[0]).y;
                float anchorTop = parent.rect.yMin + block.anchorMax.y * parent.rect.height;
                top = Mathf.Min(_controlsAuthoredTop, rowBottom - controlsGapUnderDifficultyRow - anchorTop);
            }

            block.offsetMax = new Vector2(block.offsetMax.x, top);
        }

        public override void Bind(SO_ArcadeGame game, int intensity)
        {
            base.Bind(game, intensity);

            var vessel = ResolveModeVessel(game);
            if (controlsPanel)
                controlsPanel.Show(vessel ? vessel.Class : VesselClassType.Any, vessel,
                                   game != null ? game.Mode : GameModes.Random);
        }

        /// <summary>
        /// Fill the objective box for the previewed mode. Called by the modal, which owns the
        /// preview definition - the panel never resolves one itself.
        /// </summary>
        public void BindObjective(CosmicShore.Data.ScoringMetric metric, string howYouWin)
        {
            var library = Resources.Load<ModeControlsLibrarySO>(ModeControlsLibrarySO.ResourcePath);
            _metricIcon = library ? library.IconForMetric(metric) : null;
            _objectiveCount = 0;

            if (objectiveBox) objectiveBox.Bind(metric, howYouWin);
            if (microToast) microToast.Hide();
        }

        /// <summary>
        /// Take the objective box down for a card that states its objective somewhere else. The
        /// weekly challenge does: its ask is one line, and a box that repeats it beside a counter
        /// stuck at 0 says the same thing twice and one of them wrongly.
        /// </summary>
        public void HideObjective()
        {
            if (objectiveBox) objectiveBox.Clear();
            if (microToast) microToast.Hide();
        }

        /// <summary>
        /// Whether tapping the preview may hand the player the stick. False leaves the arena
        /// playing under AI as a look-only view - see <see cref="ModePreviewWindow.SetFocusEnabled"/>.
        /// </summary>
        public void SetPreviewFocusEnabled(bool enabled)
        {
            if (previewWindow) previewWindow.SetFocusEnabled(enabled);
        }

        /// <summary>
        /// The preview's objective moved: pulse the box's counter and pop the toast, on the SAME
        /// event - the "+1" and the pulsing counter are two views of one beat, which is what makes
        /// the pair teach ("that thing I just did is the thing that scores").
        /// </summary>
        public void NotifyObjectiveProgress(int delta, int total, Color? flash = null)
        {
            _objectiveCount = total;
            if (objectiveBox) objectiveBox.SetCount(total, pulse: true, flash: flash);
            if (delta > 0 && microToast) microToast.Show(delta, _metricIcon, flash);
        }

        public override void Hide()
        {
            // The preview is the expensive half of this panel — a satellite arena and, for a
            // vessel-locked mode, a networked hull swap. It must never outlive the window somebody
            // was looking at it through. The session itself is stopped by the modal, which owns it;
            // this just takes the frame down.
            if (previewWindow) previewWindow.Hide();
            if (controlsPanel) controlsPanel.Clear();
            if (objectiveBox) objectiveBox.Clear();
            if (microToast) microToast.Hide();
            base.Hide();
        }

        /// <summary>
        /// The hull this mode's controls block describes: the card's vessel when it locks one, and
        /// otherwise the FIRST it lists.
        ///
        /// <para>Taking the first is a compromise and it replaces a worse one. Requiring exactly
        /// one meant Scurry (3), Brood Rush (6) and Freestyle (6) drew no ability rows at all, so
        /// those cards showed an empty controls block — and "nothing" is not more honest than "one
        /// of the hulls you may fly", it is just less useful. A mode that wants a different hull
        /// named says so in <c>ModeControlsLibrarySO.Vessel</c>, which the panel applies over
        /// this.</para>
        /// </summary>
        static SO_Vessel ResolveModeVessel(SO_ArcadeGame game)
        {
            if (game == null || game.Vessels == null || game.Vessels.Count == 0) return null;
            return game.Vessels[0];
        }
    }
}
