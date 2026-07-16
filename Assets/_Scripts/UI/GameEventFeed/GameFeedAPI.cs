using UnityEngine;
using CosmicShore.Utility;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;

namespace CosmicShore.UI
{
    public static class GameFeedAPI
    {
        private const string ChannelPath = "Channels/GameFeedChannel";

        private static ScriptableEventGameFeedPayload _channel;

        private static ScriptableEventGameFeedPayload Channel =>
            _channel != null
                ? _channel
                : (_channel = Resources.Load<ScriptableEventGameFeedPayload>(ChannelPath));

        /// <summary>
        /// Domain color source - the same <see cref="SO_ColorSet"/> the vessels and prisms
        /// use. Assigned once by ThemeManager at game start (single source of truth - R5).
        /// Null before a themed scene loads, in which case GetDomainColor falls back to white.
        /// </summary>
        public static SO_ColorSet ColorSet { private get; set; }

        public static void Post(string message, Domains domain, GameFeedType type = GameFeedType.Generic)
        {
            var ch = Channel;
            if (ch == null)
            {
                CSDebug.LogWarning($"[GameFeedAPI] Missing channel at Resources/{ChannelPath}");
                return;
            }
            ch.Raise(new GameFeedPayload(message, domain, type));
        }

        public static void PostJoust(string attackerName, Domains attackerDomain,
                                      string targetName, Domains targetDomain)
        {
            var atkHex = ColorUtility.ToHtmlStringRGB(GetDomainColor(attackerDomain));
            var defHex = ColorUtility.ToHtmlStringRGB(GetDomainColor(targetDomain));

            var message = $"<color=#{atkHex}><b>{attackerName}</b></color> jousted <color=#{defHex}><b>{targetName}</b></color>";

            Post(message, Domains.Blue, GameFeedType.JoustHit);
        }

        public static Color GetDomainColor(Domains domain) =>
            ColorSet != null ? ColorSet.GetDomainUIColor(domain) : Color.white;
    }
}
