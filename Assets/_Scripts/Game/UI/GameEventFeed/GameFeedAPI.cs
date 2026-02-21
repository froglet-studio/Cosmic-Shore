using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Game.UI
{
    public static class GameFeedAPI
    {
        private const string ChannelPath = "Channels/GameFeedChannel";

        private static ScriptableEventGameFeedPayload _channel;

        private static ScriptableEventGameFeedPayload Channel =>
            _channel != null
                ? _channel
                : (_channel = Resources.Load<ScriptableEventGameFeedPayload>(ChannelPath));

        private static readonly Dictionary<Domains, Color> DomainColors = new()
        {
            { Domains.Jade, new Color(0.0f, 0.8f, 0.4f) },
            { Domains.Ruby, new Color(0.9f, 0.2f, 0.2f) },
            { Domains.Gold, new Color(1.0f, 0.8f, 0.0f) },
            { Domains.Blue, new Color(0.2f, 0.4f, 0.9f) },
        };

        public static void Post(string message, Domains domain, GameFeedType type = GameFeedType.Generic)
        {
            var ch = Channel;
            if (ch == null)
            {
                Debug.LogWarning($"[GameFeedAPI] Missing channel at Resources/{ChannelPath}");
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

            Post(message, Domains.Unassigned, GameFeedType.JoustHit);
        }

        public static Color GetDomainColor(Domains domain)
        {
            return DomainColors.TryGetValue(domain, out var color) ? color : Color.white;
        }
    }
}
