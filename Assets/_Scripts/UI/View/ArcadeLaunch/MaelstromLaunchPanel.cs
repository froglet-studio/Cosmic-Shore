using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// The launch panel for the Maelstrom — the meta-mode that draws OTHER modes.
    ///
    /// <para>It differs from <see cref="MinigameLaunchPanel"/> in exactly three ways, and each one
    /// follows from that sentence:</para>
    /// <list type="bullet">
    /// <item>A <b>clip</b> instead of the live preview window — a mode with no arena of its own has
    /// nothing to stand up, which is why the preview library excludes it in code.</item>
    /// <item><b>No controls block</b> — the hull changes every round (four of the pool's modes are
    /// vessel-locked), so there is no one set of controls to teach here.</item>
    /// <item>A <b>pool list</b> in that space instead, because the question this card actually
    /// raises is "what am I going to end up playing?" — and raising the intensity answers it
    /// differently, adding modes to the draw as well as raising each game's own ceiling.</item>
    /// </list>
    /// </summary>
    public class MaelstromLaunchPanel : ArcadeLaunchPanel
    {
        [Header("Video")]
        [SerializeField, Tooltip("The clip shown where a playable mode would show its live arena. " +
                                 "Reads SO_ArcadeGame.PreviewVideo off the Maelstrom card.")]
        ModeVideoView videoView;

        [Header("Pool")]
        [SerializeField, Tooltip("Which modes the chosen intensity can draw. Redrawn on every " +
                                 "intensity change.")]
        MaelstromPoolListView poolList;

        /// <summary>Only the meta-mode.</summary>
        public override bool Handles(SO_ArcadeGame game)
            => game != null && game.Mode == GameModes.Maelstrom;

        public override void Bind(SO_ArcadeGame game, int intensity)
        {
            base.Bind(game, intensity);

            if (videoView)
                videoView.Show(game ? game.PreviewVideo : null);

            if (poolList)
                poolList.Show(intensity);
        }

        public override void HandleIntensityChanged(int intensity)
        {
            // The whole point of the ladder: the list visibly fills in as the intensity rises.
            if (poolList) poolList.Show(intensity);

            CSDebug.LogVerbose(CSLogChannel.ArcadeLaunch,
                $"[ArcadeLaunch] Maelstrom intensity {intensity} → " +
                $"{(poolList ? poolList.UnlockedCount : 0)} modes in the pool.");
        }

        public override void Show()
        {
            base.Show();
            if (videoView) videoView.Resume();
        }

        public override void Hide()
        {
            if (videoView) videoView.Stop();
            base.Hide();
        }
    }
}
