using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Static posting surface for the in-game toast feed. Gameplay posts SITUATIONS with raw
    /// arguments (names, domains) - never display text. The scene's
    /// <see cref="GameToastController"/> resolves each situation against the current mode's
    /// <see cref="GameToastConfigSO"/> and formats the copy, so a situation a mode has not
    /// authored simply shows nothing (Scurry's empty set opts out of everything mode-specific).
    /// </summary>
    public static class GameToastAPI
    {
        private const string ChannelPath = "Channels/GameToastChannel";

        private static ScriptableEventGameToastData _channel;

        private static ScriptableEventGameToastData Channel =>
            _channel != null
                ? _channel
                : (_channel = Resources.Load<ScriptableEventGameToastData>(ChannelPath));

        /// <summary>
        /// Domain color source - the same <see cref="SO_ColorSet"/> the vessels and prisms
        /// use. Assigned once by ThemeManager at game start (single source of truth).
        /// Null before a themed scene loads, in which case GetDomainColor falls back to white.
        /// </summary>
        public static SO_ColorSet ColorSet { private get; set; }

        public static void Post(GameToastSituation situation, Domains domain = Domains.Blue,
            params string[] args)
            => Post(situation, domain, Domains.Blue, args);

        public static void Post(GameToastSituation situation, Domains primaryDomain,
            Domains secondaryDomain, params string[] args)
        {
            var channel = Channel;
            if (channel == null)
            {
                CSDebug.LogWarning($"[GameToastAPI] Missing channel at Resources/{ChannelPath}");
                return;
            }

            channel.Raise(new GameToastData(situation, primaryDomain, secondaryDomain, args));
        }

        /// <summary>
        /// Joust announcement: <paramref name="scorerName"/> jousted <paramref name="targetName"/>.
        /// Points are looked up from RoundStats at display time by the controller.
        /// </summary>
        public static void PostJoust(string scorerName, Domains scorerDomain,
            string targetName, Domains targetDomain)
            => Post(GameToastSituation.Joust, scorerDomain, targetDomain, scorerName, targetName);

        public static Color GetDomainColor(Domains domain) =>
            ColorSet != null ? ColorSet.GetDomainUIColor(domain) : Color.white;
    }
}
