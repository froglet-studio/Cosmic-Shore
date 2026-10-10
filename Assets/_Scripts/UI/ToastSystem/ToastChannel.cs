using System;
using UnityEngine;

namespace CosmicShore.UI
{
    [CreateAssetMenu(fileName="ToastChannel", menuName= "ScriptableObjects/UI/Toast Channel")]
    public class ToastChannel : ScriptableObject
    {
        // Unified low-level
        public event Action<ChatToastRequest, Action> OnChatToast;

        // A notice raised while no ToastService is listening (between scenes: the service is a
        // scene-bound MonoBehaviour that subscribes in OnEnable) used to vanish. The one caller
        // that cannot pick its moment - a failed join bouncing the client back to its own menu -
        // asks to HOLD instead, and the next service to subscribe drains it. Runtime state only:
        // an asset never serializes a toast.
        [NonSerialized] ChatToastRequest? _held;
        [NonSerialized] float _heldUntilRealtime;

        /// <summary>True while a ToastService (or any listener) is subscribed.</summary>
        public bool HasListener => OnChatToast != null;

        /// <summary>
        /// Prefix-only line, delivered now if a service is listening, otherwise held for up to
        /// <paramref name="holdSeconds"/> of real time and delivered to the next service that
        /// subscribes (<see cref="TryTakeHeld"/>). A later held notice replaces an earlier one:
        /// the hold is a mailbox for the one message a scene change must not lose, not a queue.
        /// <paramref name="holdSeconds"/> of 0 or less means "now or never".
        /// </summary>
        public void ShowPrefixOrHold(string prefix, float holdSeconds = 45f, float duration = 4.5f,
            ToastAnimation anim = ToastAnimation.ChatSubtleSlide, Sprite icon = null, Color? accent = null)
        {
            var req = new ChatToastRequest(prefix, "", duration, anim, icon, accent);
            if (OnChatToast != null)
            {
                OnChatToast.Invoke(req, null);
                return;
            }
            if (holdSeconds <= 0f) return;
            _held = req;
            _heldUntilRealtime = Time.realtimeSinceStartup + holdSeconds;
        }

        /// <summary>
        /// Hands over the held notice, once, if one is waiting and its hold has not expired.
        /// A ToastService calls this right after it subscribes.
        /// </summary>
        public bool TryTakeHeld(out ChatToastRequest request)
        {
            if (_held.HasValue && Time.realtimeSinceStartup <= _heldUntilRealtime)
            {
                request = _held.Value;
                _held = null;
                return true;
            }
            _held = null;
            request = default;
            return false;
        }

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