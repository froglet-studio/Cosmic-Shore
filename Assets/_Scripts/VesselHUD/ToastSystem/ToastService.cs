using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Game.UI.Toast
{
    /// Attach to a chat container under your HUD Canvas (use a VerticalLayoutGroup).
    /// New items are added at the end → visually push older items upward (chat feel).
    public sealed class ToastService : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private ToastChannel channel;
        [SerializeField] private ToastItemView toastPrefab;
        [SerializeField] private RectTransform container;
        [SerializeField] private int maxConcurrent = 5;

        readonly Queue<(ChatToastRequest req, System.Action externalDone)> _queue = new();
        readonly List<ToastItemView> _active = new();
        readonly Stack<ToastItemView> _pool = new();

        void OnEnable()
        {
            if (channel) channel.OnChatToast += Enqueue;
        }
        void OnDisable()
        {
            if (channel) channel.OnChatToast -= Enqueue;
        }

        void Enqueue(ChatToastRequest req, System.Action externalDone)
        {
            _queue.Enqueue((req, externalDone));
            TryServe();
        }

        void TryServe()
        {
            while (_active.Count < maxConcurrent && _queue.Count > 0)
            {
                var (req, externalDone) = _queue.Dequeue();
                var view = Acquire();
                _active.Add(view);

                void Reclaimed()
                {
                    Return(view);
                    _active.Remove(view);
                    TryServe();
                }

                view.Play(req, Reclaimed, externalDone);
            }
        }

        ToastItemView Acquire()
        {
            var v = _pool.Count > 0 ? _pool.Pop() : Instantiate(toastPrefab, container);
            v.transform.SetAsLastSibling(); // new chat at bottom
            v.gameObject.SetActive(true);
            return v;
        }

        void Return(ToastItemView v)
        {
            v.ForceHide();
            v.transform.SetParent(container, false);
            _pool.Push(v);
        }

        // ===== Public high-level helpers (mirror channel API) =====

        /// Prefix-only chat line (subtle, longer stay)
        public void ShowPrefix(string prefix, float duration = 4.5f, ToastAnimation anim = ToastAnimation.ChatSubtleSlide,
                               Sprite icon = null, Color? accent = null)
            => Enqueue(new ChatToastRequest(prefix, "", duration, anim, icon, accent), null);

        /// Prefix + static postfix (no timers)
        public void ShowPrefixPostfix(string prefix, string postfix, float duration = 3.5f,
                                      ToastAnimation anim = ToastAnimation.ChatSubtleSlide,
                                      Sprite icon = null, Color? accent = null)
            => Enqueue(new ChatToastRequest(prefix, postfix, duration, anim, icon, accent), null);

        /// Postfix countdown only (prefix stays constant; postfix animates each second).
        /// E.g., prefix = "Overcharging", from = 3, format = "in {0}" → "in 3", "in 2", "in 1"
        public void ShowCountdown(string prefix, int from, string postfixFormat = "in {0}",
                                  ToastAnimation anim = ToastAnimation.Pop, System.Action onDone = null,
                                  Sprite icon = null, Color? accent = null)
            => Enqueue(new ChatToastRequest(prefix, "", 0f, anim, icon, accent, from, postfixFormat), onDone);
    }
}
