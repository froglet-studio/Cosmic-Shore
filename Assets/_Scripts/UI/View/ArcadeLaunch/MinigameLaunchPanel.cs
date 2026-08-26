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

        public override ModePreviewWindow PreviewWindow => previewWindow;

        /// <summary>
        /// Every card except the meta-mode. Maelstrom has no arena of its own, so it gets
        /// <see cref="MaelstromLaunchPanel"/> instead.
        /// </summary>
        public override bool Handles(SO_ArcadeGame game)
            => game != null && game.Mode != GameModes.Tournament;

        public override void Bind(SO_ArcadeGame game, int intensity)
        {
            base.Bind(game, intensity);

            var vessel = ResolveModeVessel(game);
            if (controlsPanel)
                controlsPanel.Show(vessel ? vessel.Class : VesselClassType.Any, vessel,
                                   game != null ? game.Mode : GameModes.Random);
        }

        public override void Hide()
        {
            // The preview is the expensive half of this panel — a satellite arena and, for a
            // vessel-locked mode, a networked hull swap. It must never outlive the window somebody
            // was looking at it through. The session itself is stopped by the modal, which owns it;
            // this just takes the frame down.
            if (previewWindow) previewWindow.Hide();
            if (controlsPanel) controlsPanel.Clear();
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
