using System.Collections.Generic;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using Reflex.Attributes;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Brain of the in-game toast feed. Listens to the game toast channel, resolves each
    /// situation against the current mode's toast config (via <see cref="GameToastLibrarySO"/>),
    /// formats the final copy (domain-colored names, live joust points) and hands the display
    /// text to <see cref="GameToastView"/>. Also drives the config-authored IDLE HINTS: during
    /// an active turn, a hint shows after its situation has been quiet for its configured idle
    /// time (e.g. Joust's "fly close to an opponent" after a minute without a joust).
    ///
    /// Lives on the toast panel prefab next to the view. A situation the current mode has not
    /// authored resolves to nothing and is skipped - that is the opt-out mechanism.
    /// </summary>
    public class GameToastController : MonoBehaviour
    {
        [Header("References (wire on the prefab)")]
        [SerializeField] private ScriptableEventGameToastData toastChannel;
        [SerializeField] private GameToastLibrarySO library;
        [SerializeField] private GameToastView view;

        [Inject] private GameDataSO gameData;

        // Idle-hint state (rebuilt from config each turn)
        private readonly List<GameToastDefinition> _activeHints = new();
        private readonly Dictionary<GameToastSituation, float> _lastActivityTime = new();
        private readonly Dictionary<GameToastDefinition, float> _lastHintShownTime = new();
        private readonly HashSet<GameToastDefinition> _hintsFiredOnce = new();
        private bool _turnActive;
        private float _turnStartTime;

        private void Start()
        {
            toastChannel.OnRaised += HandleToast;

            gameData.OnPlayerAdded += HandlePlayerAdded;
            gameData.OnMiniGameTurnStarted.OnRaised += HandleTurnStarted;
            gameData.OnMiniGameTurnEnd.OnRaised += HandleTurnEnded;
            gameData.OnResetForReplay.OnRaised += HandleResetForReplay;
        }

        private void OnDestroy()
        {
            toastChannel.OnRaised -= HandleToast;

            if (gameData == null) return;
            gameData.OnPlayerAdded -= HandlePlayerAdded;
            gameData.OnMiniGameTurnStarted.OnRaised -= HandleTurnStarted;
            gameData.OnMiniGameTurnEnd.OnRaised -= HandleTurnEnded;
            gameData.OnResetForReplay.OnRaised -= HandleResetForReplay;
        }

        //–––––––––––––––––––––––––––––––––––––––––
        // EVENT HANDLERS
        //–––––––––––––––––––––––––––––––––––––––––

        private void HandlePlayerAdded(string playerName, Domains domain)
            => Show(new GameToastData(GameToastSituation.PlayerJoined, domain, Domains.Blue, playerName));

        private void HandleToast(GameToastData data)
        {
            _lastActivityTime[data.Situation] = Time.time;
            Show(data);
        }

        private void HandleTurnStarted()
        {
            library.CollectIdleHints(gameData.GameMode, _activeHints);
            _lastActivityTime.Clear();
            _lastHintShownTime.Clear();
            _hintsFiredOnce.Clear();
            _turnStartTime = Time.time;
            _turnActive = true;
        }

        private void HandleTurnEnded()
        {
            _turnActive = false;
            _activeHints.Clear();
        }

        private void HandleResetForReplay()
        {
            _turnActive = false;
            _activeHints.Clear();
            view.Clear();
        }

        //–––––––––––––––––––––––––––––––––––––––––
        // IDLE HINTS
        //–––––––––––––––––––––––––––––––––––––––––

        private void Update()
        {
            if (!_turnActive || _activeHints.Count == 0) return;

            float now = Time.time;
            for (int i = 0, count = _activeHints.Count; i < count; i++)
            {
                var hint = _activeHints[i];
                if (!hint.repeatWhileIdle && _hintsFiredOnce.Contains(hint))
                    continue;

                float activity = _lastActivityTime.TryGetValue(hint.resetOnSituation, out var t)
                    ? t
                    : _turnStartTime;
                if (_lastHintShownTime.TryGetValue(hint, out var shown))
                    activity = Mathf.Max(activity, shown);

                if (now - activity < hint.idleSeconds)
                    continue;

                _lastHintShownTime[hint] = now;
                _hintsFiredOnce.Add(hint);
                Show(new GameToastData(hint.situation, Domains.Blue, Domains.Blue));
            }
        }

        //–––––––––––––––––––––––––––––––––––––––––
        // RESOLUTION + FORMATTING
        //–––––––––––––––––––––––––––––––––––––––––

        private void Show(GameToastData data)
        {
            if (!library.TryResolve(gameData.GameMode, data.Situation, out var definition))
                return; // this mode does not announce this situation

            string message;
            try
            {
                message = string.Format(definition.messageTemplate, BuildFormatArgs(definition, data));
            }
            catch (System.FormatException e)
            {
                Debug.LogError($"[GameToastController] Bad template for {data.Situation} " +
                               $"(\"{definition.messageTemplate}\"): {e.Message}", this);
                return;
            }

            var textColor = definition.useDomainColoredNames || !definition.tintWithDomainColor
                ? Color.white
                : GetDomainColor(data.PrimaryDomain);

            view.Spawn(message, textColor, GetDomainColor(data.PrimaryDomain), definition.alpha);
        }

        /// <summary>
        /// Expands the raw situation args (names) into the template's placeholder set - see
        /// the placeholder contract documented on <see cref="GameToastDefinition.messageTemplate"/>.
        /// </summary>
        private object[] BuildFormatArgs(GameToastDefinition definition, GameToastData data)
        {
            string arg0 = data.Args is { Length: > 0 } ? data.Args[0] : string.Empty;
            string arg1 = data.Args is { Length: > 1 } ? data.Args[1] : string.Empty;

            string name0 = definition.useDomainColoredNames ? Colorize(arg0, data.PrimaryDomain) : arg0;
            string name1 = definition.useDomainColoredNames ? Colorize(arg1, data.SecondaryDomain) : arg1;

            if (data.Situation == GameToastSituation.Joust)
            {
                // {0}=scorer, {1}=scorer points, {2}=target, {3}=target points
                return new object[]
                {
                    name0, GetJoustPoints(arg0),
                    name1, GetJoustPoints(arg1)
                };
            }

            var args = new object[data.Args?.Length ?? 0];
            for (int i = 0; i < args.Length; i++)
                args[i] = i == 0 ? name0 : i == 1 ? name1 : data.Args[i];
            return args;
        }

        private int GetJoustPoints(string playerName)
        {
            var list = gameData.RoundStatsList;
            for (int i = 0, count = list.Count; i < count; i++)
            {
                var stats = list[i];
                if (stats != null && stats.Name == playerName)
                    return stats.JoustCollisions;
            }

            return 0;
        }

        private string Colorize(string playerName, Domains domain)
        {
            if (string.IsNullOrEmpty(playerName)) return playerName;
            var hex = ColorUtility.ToHtmlStringRGB(GetDomainColor(domain));
            return $"<color=#{hex}><b>{playerName}</b></color>";
        }

        // Single source of truth - the same ColorSet the vessels and prisms use.
        private Color GetDomainColor(Domains domain) =>
            gameData != null && gameData.ThemeManagerData != null
                ? gameData.ThemeManagerData.GetDomainUIColor(domain)
                : Color.white;
    }
}
