using System.Text;
using CosmicShore.Data;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Single source of truth for the per-mode end-game counts (how many crystals / jousts
    /// end a turn) for HexRace, Joust, and Crystal Capture.
    ///
    /// Authored ONLY through <c>Tools &gt; Cosmic Shore &gt; End Game Conditions</c>
    /// (the <c>EndConditionOverridesWindow</c> editor tool) — there are intentionally no
    /// per-scene inspector override fields anymore. The turn monitors load this asset from
    /// <c>Resources/EndConditionOverrides</c> at runtime.
    ///
    /// Semantic: <b>0 = auto/default</b>, <b>&gt; 0 = explicit count</b>:
    ///   • HexRace / Crystal Capture — 0 falls back to the track-waypoint auto-calc (then 39).
    ///   • Joust — 0 falls back to <see cref="DefaultJoustCount"/>.
    ///
    /// Two value sets are stored: the <b>LIVE</b> counts (what the game actually uses at runtime)
    /// and the <b>BUILD</b> counts (the values a shipping build must use). Lower a LIVE count to end
    /// a mode quickly while testing, then restore the BUILD values before building. The editor
    /// window and a build-time check warn whenever LIVE drifts from BUILD
    /// (see <see cref="LiveMatchesBuild"/> / <see cref="DescribeDrift"/>).
    ///
    /// See the <c>/EndGameConditions</c> skill (<c>.claude/skills/EndGameConditions/</c>).
    /// </summary>
    [CreateAssetMenu(
        fileName = "EndConditionOverrides",
        menuName = "ScriptableObjects/" + nameof(EndConditionOverridesSO))]
    public class EndConditionOverridesSO : ScriptableObject
    {
        /// <summary>Resources path the turn monitors load this from (must live at Assets/Resources/EndConditionOverrides.asset).</summary>
        public const string ResourcePath = "EndConditionOverrides";

        /// <summary>Joust target used when <see cref="joustCount"/> is 0 (auto/default).</summary>
        public const int DefaultJoustCount = 3;

        [Header("Live counts — used at runtime. 0 = auto/default (edit via Tools > Cosmic Shore > End Game Conditions)")]
        [Tooltip("HexRace crystals to end the race. 0 = auto-calc from the track waypoints.")]
        [Min(0)] public int hexRaceCrystalCount = 0;

        [Tooltip("Crystal Capture crystals to end the turn. 0 = auto-calc from track waypoints.")]
        [Min(0)] public int crystalCaptureCrystalCount = 20;

        [Tooltip("Joust collisions to end the turn. 0 = default (3).")]
        [Min(0)] public int joustCount = 3;

        [Header("Build values — what the LIVE counts above must be restored to before shipping a build")]
        [Tooltip("HexRace crystals a shipping build must use. Restore the live count to this before building.")]
        [Min(0)] public int hexRaceCrystalCountBuild = 0;

        [Tooltip("Crystal Capture crystals a shipping build must use. Restore the live count to this before building.")]
        [Min(0)] public int crystalCaptureCrystalCountBuild = 20;

        [Tooltip("Joust collisions a shipping build must use. Restore the live count to this before building.")]
        [Min(0)] public int joustCountBuild = 3;

        static EndConditionOverridesSO _instance;

        /// <summary>
        /// Cached runtime accessor — loads the asset from <see cref="ResourcePath"/> once.
        /// Returns null only if the asset is missing (callers fall back to their own defaults).
        /// </summary>
        public static EndConditionOverridesSO Instance
        {
            get
            {
                if (_instance == null)
                    _instance = Resources.Load<EndConditionOverridesSO>(ResourcePath);
                return _instance;
            }
        }

        /// <summary>
        /// Crystal target for a crystal-collection mode. Returns the configured count when &gt; 0,
        /// otherwise <paramref name="autoCalcFallback"/> (the mode's waypoint/default calc).
        /// </summary>
        public int GetCrystalCount(GameModes mode, int autoCalcFallback)
        {
            int configured = mode switch
            {
                GameModes.HexRace => hexRaceCrystalCount,
                GameModes.MultiplayerCrystalCapture => crystalCaptureCrystalCount,
                _ => 0,
            };
            return configured > 0 ? configured : autoCalcFallback;
        }

        /// <summary>Joust target: the configured count when &gt; 0, otherwise <see cref="DefaultJoustCount"/>.</summary>
        public int GetJoustCount() => joustCount > 0 ? joustCount : DefaultJoustCount;

        /// <summary>
        /// True when every LIVE count (used at runtime) equals its recorded BUILD value. False means
        /// the project is in a test/override configuration that should be restored before shipping —
        /// it drives the warning in the editor window and the build-time check.
        /// </summary>
        public bool LiveMatchesBuild =>
            hexRaceCrystalCount == hexRaceCrystalCountBuild &&
            crystalCaptureCrystalCount == crystalCaptureCrystalCountBuild &&
            joustCount == joustCountBuild;

        /// <summary>
        /// Multi-line summary (one bullet per mode) of every count where the LIVE value differs from
        /// the BUILD value. Empty string when <see cref="LiveMatchesBuild"/> is true. Shared by the
        /// editor window's drift warning and the build-time check so both phrase drift identically.
        /// </summary>
        public string DescribeDrift()
        {
            var sb = new StringBuilder();
            AppendDrift(sb, "HexRace", hexRaceCrystalCount, hexRaceCrystalCountBuild);
            AppendDrift(sb, "Crystal Capture", crystalCaptureCrystalCount, crystalCaptureCrystalCountBuild);
            AppendDrift(sb, "Joust", joustCount, joustCountBuild);
            return sb.ToString();

            static void AppendDrift(StringBuilder sb, string label, int live, int build)
            {
                if (live != build)
                    sb.Append($"  • {label}: live {live} → build {build}\n");
            }
        }
    }
}
