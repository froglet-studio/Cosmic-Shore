using UnityEngine;

namespace CosmicShore.Game.UI.Toast
{
    /// Immutable data packet for a chat toast.
    public readonly struct ChatToastRequest
    {
        public readonly string Prefix;             // main line
        public readonly string Postfix;            // aux line (e.g., “in 3” / “x2 combo”)
        public readonly float Duration;            // total lifetime (ignored for countdown until it finishes)
        public readonly ToastAnimation Animation;
        public readonly Sprite Icon;               // optional
        public readonly Color? Accent;             // optional
        public readonly int PostfixCountdownFrom;  // if > 0 → postfix countdown only
        public readonly string PostfixCountdownFormat; // e.g., "Overcharging in {0}"

        public ChatToastRequest(
            string prefix,
            string postfix = "",
            float duration = 4.5f, // longer by default for chat feel
            ToastAnimation animation = ToastAnimation.ChatSubtleSlide,
            Sprite icon = null,
            Color? accent = null,
            int postfixCountdownFrom = 0,
            string postfixCountdownFormat = "{0}")
        {
            Prefix = prefix;
            Postfix = postfix;
            Duration = duration;
            Animation = animation;
            Icon = icon;
            Accent = accent;
            PostfixCountdownFrom = postfixCountdownFrom;
            PostfixCountdownFormat = postfixCountdownFormat;
        }
    }
}