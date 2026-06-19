using System;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>
    /// SOAP event channel for the generic popup system (mirrors the ToastChannel pattern). Any
    /// system raises a <see cref="PopupRequest"/> here; <see cref="PopupManager"/> (the single
    /// subscriber/presenter) shows it. Result comes back via the request's
    /// <see cref="PopupRequest.OnResult"/> callback. Decoupled — requesters never reference the
    /// presenter, and the presenter never references the requesters.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/UI/Popup Channel", fileName = "PopupChannel")]
    public class PopupChannel : ScriptableObject
    {
        /// <summary>Raised to show a popup. <see cref="PopupManager"/> subscribes.</summary>
        public event Action<PopupRequest> OnPopupRequested;

        /// <summary>Raised to silently hide whatever popup currently holds the given replace key
        /// (no result callback). Used to dismiss the invite popup when the invite is resolved on
        /// another surface (the panel).</summary>
        public event Action<string> OnDismissRequested;

        public void Show(PopupRequest request) => OnPopupRequested?.Invoke(request);

        /// <summary>Hide the active popup carrying <paramref name="replaceKey"/> without firing its result.</summary>
        public void Dismiss(string replaceKey) => OnDismissRequested?.Invoke(replaceKey);

        // ── Convenience builders ──────────────────────────────────────────────

        /// <summary>Two-button confirm dialog (modal, centered by default).</summary>
        public void Confirm(string title, string message, string primaryLabel, string secondaryLabel,
            Action<PopupResult> onResult, bool modal = true, PopupPlacement placement = PopupPlacement.Center,
            string replaceKey = null)
            => Show(new PopupRequest(title, message, primaryLabel, secondaryLabel, modal, placement,
                0f, replaceKey, onResult));

        /// <summary>Single-button "OK" / info popup.</summary>
        public void Info(string title, string message, string okLabel = "OK", Action onOk = null,
            PopupPlacement placement = PopupPlacement.Center, float autoHideSeconds = 0f)
            => Show(new PopupRequest(title, message, okLabel, null, false, placement, autoHideSeconds, null,
                onOk != null ? _ => onOk() : (Action<PopupResult>)null));
    }
}
