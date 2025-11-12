using System;
using UnityEngine;

namespace CosmicShore.Game.UI.Toast
{
    [CreateAssetMenu(fileName="ToastChannel", menuName="CosmicShore/UI/Toast Channel")]
    public class ToastChannel : ScriptableObject
    {
        // Unified low-level
        public event Action<ChatToastRequest, Action> OnChatToast;

        // High-level helpers (service subscribes to the single event)
        public void ShowPrefix(string prefix, float duration = 4.5f, ToastAnimation anim = ToastAnimation.ChatSubtleSlide,
            Sprite icon = null, Color? accent = null)
            => OnChatToast?.Invoke(new ChatToastRequest(prefix, "", duration, anim, icon, accent), null);

        public void ShowPrefixPostfix(string prefix, string postfix, float duration = 3.5f,
            ToastAnimation anim = ToastAnimation.ChatSubtleSlide,
            Sprite icon = null, Color? accent = null)
            => OnChatToast?.Invoke(new ChatToastRequest(prefix, postfix, duration, anim, icon, accent), null);

        /// Postfix-only countdown (postfix updates each tick; prefix stays static)
        public void ShowCountdown(string prefix, int from, string postfixFormat = "in {0}",
            ToastAnimation anim = ToastAnimation.Pop, Action onDone = null,
            Sprite icon = null, Color? accent = null)
            => OnChatToast?.Invoke(new ChatToastRequest(prefix, "", 0f, anim, icon, accent, from, postfixFormat), onDone);
    }
}