using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// How the in-game HUD groups scores. <see cref="Inherit"/> keeps whatever the canvas prefab's
    /// own wiring decides, which is exactly today's behaviour.
    /// </summary>
    public enum HudScoreLayout
    {
        /// <summary>Decide from the prefab wiring, as before. Safe default.</summary>
        Inherit = 0,
        /// <summary>One card per player, in PlayerScoreContainer.</summary>
        PerPlayer = 1,
        /// <summary>One panel per domain (Jade / Ruby / Gold) - the domain-mode layout.</summary>
        PerDomain = 2,
    }

    /// <summary>
    /// The per-mode slice of GameCanvas configuration, held as data instead of as scene overrides.
    ///
    /// <b>Why this exists.</b> GameCanvas.prefab is meant to be one asset shared by every game
    /// mode. The handful of things that genuinely differ per mode used to be authored as inspector
    /// overrides ON the canvas instance in each scene - and a scene override always beats the
    /// prefab, so once they piled up, editing the prefab stopped reaching those scenes at all.
    /// Anything genuinely per-mode belongs here instead: one asset per mode, dropped into that
    /// mode's scene via <see cref="CosmicShore.Gameplay.GameModeSceneConfig"/>. The canvas prefab
    /// then needs zero per-scene edits, and a new mode is "make an asset, drop it in".
    ///
    /// <b>Design rule.</b> Every field is opt-in: the neutral value means "leave whatever the
    /// prefab / scene already does alone". Adding a config asset to a scene can therefore never
    /// change behaviour until you actually set something, which is what makes it safe to roll out
    /// one mode at a time.
    ///
    /// See <c>Docs/GAMECANVAS.md</c>.
    /// </summary>
    [CreateAssetMenu(
        fileName = "GameModeUIConfig_",
        menuName = "ScriptableObjects/Game Modes/Game Mode UI Config",
        order = 0)]
    public class GameModeUIConfigSO : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("The mode this config describes. Used for lookup and for the editor tooling's labels.")]
        public GameModes Mode = GameModes.Random;

        [Tooltip("Optional human label shown in tooling. Defaults to the enum name.")]
        public string DisplayName;

        [Header("End-game scoreboard stats")]
        [Tooltip("Stat events this mode shows on the end-game scoreboard, in display order.\n\n" +
                 "EMPTY = leave alone: the scene's own EventDrivenStatsProvider list wins if it has " +
                 "one, otherwise the provider falls back to discovering stats from the local vessel's " +
                 "telemetry. This was the one value in the canvas that was genuinely different per " +
                 "mode, which is why it is the first thing to move here.")]
        public List<VesselStatEventSO> EndGameStats = new();

        [Header("In-game HUD")]
        [Tooltip("How this mode groups live scores.\n\n" +
                 "This is THE field that lets one GameCanvas prefab serve every mode. The unified " +
                 "prefab carries the superset - MultiplayerHUD + the domain containers - so the " +
                 "wiring is always present; this says whether the mode actually wants the " +
                 "per-domain layout or the per-player cards.\n\n" +
                 "Inherit = decide from the prefab wiring exactly as before, so a scene with no " +
                 "config behaves identically to today.")]
        public HudScoreLayout ScoreLayout = HudScoreLayout.Inherit;

        public string ResolvedName =>
            !string.IsNullOrWhiteSpace(DisplayName) ? DisplayName : Mode.ToString();

        public bool HasEndGameStats => EndGameStats != null && EndGameStats.Count > 0;
    }
}
