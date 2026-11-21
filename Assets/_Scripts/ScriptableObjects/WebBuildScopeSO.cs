using System.Collections.Generic;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// What the browser build (<c>FROGLET_WEB_TOYBOX</c>, Docs/WEBGL_TOYBOX.md) carries beyond the freestyle
    /// Toybox: the arcade and arena cards it lets a player launch, and the Maelstrom buckets it deals from.
    /// Shombith, 2026-11-21: "web has the toybox, all of squirrel's games and all the time games and arena's
    /// regatta unlocked. Maelstrom only picks between time games: I1 2 games, I2 4, I3 6, I4 8."
    ///
    /// <para>The one place that scope is decided. <c>WebToyboxBuild</c> adds every card's scene to the build,
    /// the Arcade and Arena grids show only these cards, and the Maelstrom draws only from
    /// <see cref="MaelstromTiers"/>. Written by <c>Tools/Build/web_build_scope.py --write</c> from that rule;
    /// <c>--check</c> fails when the asset drifts from it, and <c>WebBuildScopeTests</c> fails when a Maelstrom
    /// bucket names a card the build would not carry (a draw would load a scene that is not there).</para>
    ///
    /// <para>Read only when <see cref="CosmicShore.Core.WebToybox.IsBuild"/> is true; every other build ignores it.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "WebBuildScope", menuName = "ScriptableObjects/Config/" + nameof(WebBuildScopeSO))]
    public class WebBuildScopeSO : ScriptableObject
    {
        /// <summary>Resources path of the one asset (<c>Assets/Resources/WebBuildScope.asset</c>).</summary>
        public const string ResourcePath = "WebBuildScope";

        [Header("Cards")]
        [Tooltip("Every arcade and arena card the browser build carries, the Maelstrom card included. Each card's " +
                 "scene is added to the WebGL build; the Arcade and Arena windows show only these cards. A card " +
                 "not listed here is not in the browser build at all.")]
        [SerializeField] List<SO_ArcadeGame> games = new();

        [Header("Maelstrom")]
        [Tooltip("The Maelstrom's buckets in the browser, one per intensity, used INSTEAD of the MaelstromData " +
                 "asset's own tiers. Every card listed here must also be in Games above and in the Maelstrom's " +
                 "GameQueue. Tiers are exclusive, as in MaelstromDataSO: a run at intensity N draws only bucket N.")]
        [SerializeField] List<MaelstromIntensityTier> maelstromTiers = new();

        /// <summary>The cards the browser build carries.</summary>
        public IReadOnlyList<SO_ArcadeGame> Games => games;

        /// <summary>The Maelstrom's browser buckets (replace <see cref="MaelstromDataSO.IntensityTiers"/> there).</summary>
        public List<MaelstromIntensityTier> MaelstromTiers => maelstromTiers;

        /// <summary>True when the browser build carries <paramref name="game"/>.</summary>
        public bool Carries(SO_ArcadeGame game) => game && games != null && games.Contains(game);

        static WebBuildScopeSO _cached;
        static bool _missingReported;

        /// <summary>
        /// The project's scope, or null when the asset is missing. A missing asset is loud: the browser build
        /// then carries no card at all (the Toybox still plays), which is the safe failure - a card whose
        /// scene is not in the build would crash its launch.
        /// </summary>
        public static WebBuildScopeSO Load()
        {
            if (_cached) return _cached;
            _cached = Resources.Load<WebBuildScopeSO>(ResourcePath);
            if (!_cached && !_missingReported)
            {
                _missingReported = true;
                CSDebug.LogError($"[WebBuildScope] Resources/{ResourcePath} is missing - the browser build offers " +
                               "no arcade or arena game. Run Tools/Build/web_build_scope.py --write.");
            }
            return _cached;
        }
    }
}
