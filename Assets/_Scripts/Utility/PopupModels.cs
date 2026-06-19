using System;
using UnityEngine;

namespace CosmicShore.Utility
{
    /// <summary>Which button (or auto-dismiss) closed a popup.</summary>
    public enum PopupResult
    {
        Primary,    // left/confirm button (e.g. Accept, OK)
        Secondary,  // right/cancel button (e.g. Decline)
        Dismissed   // auto-hide timeout or programmatic dismiss — NOT a user choice
    }

    /// <summary>Where on screen the popup card anchors.</summary>
    public enum PopupPlacement
    {
        Center,
        BottomLeft
    }

    /// <summary>
    /// Immutable description of a popup to show, raised through <see cref="PopupChannel"/>.
    /// One generic shape serves OK dialogs (1 button), confirm dialogs (2 buttons, modal), and
    /// the non-modal bottom-left party-invite popup (2 buttons, auto-hide, latest-wins).
    /// </summary>
    public readonly struct PopupRequest
    {
        public readonly string Title;
        public readonly string Message;
        /// <summary>Left/confirm button label. Falls back to "OK" when empty.</summary>
        public readonly string PrimaryLabel;
        /// <summary>Right/cancel button label. Null/empty → single-button popup.</summary>
        public readonly string SecondaryLabel;
        /// <summary>True → block raycasts behind the popup (must answer it). Invites are non-modal.</summary>
        public readonly bool Modal;
        public readonly PopupPlacement Placement;
        /// <summary>Seconds before the popup auto-hides (raising <see cref="PopupResult.Dismissed"/>). 0 → stays until answered.</summary>
        public readonly float AutoHideSeconds;
        /// <summary>
        /// Popups sharing a non-empty key replace each other (latest-wins, single slot) instead of
        /// stacking. The party-invite popup uses one key so a newer invite instantly takes over.
        /// </summary>
        public readonly string ReplaceKey;
        /// <summary>Invoked once with the result when the popup closes (button or auto-hide).</summary>
        public readonly Action<PopupResult> OnResult;
        public readonly Sprite Icon;

        public PopupRequest(
            string title,
            string message,
            string primaryLabel,
            string secondaryLabel = null,
            bool modal = false,
            PopupPlacement placement = PopupPlacement.Center,
            float autoHideSeconds = 0f,
            string replaceKey = null,
            Action<PopupResult> onResult = null,
            Sprite icon = null)
        {
            Title = title;
            Message = message;
            PrimaryLabel = primaryLabel;
            SecondaryLabel = secondaryLabel;
            Modal = modal;
            Placement = placement;
            AutoHideSeconds = autoHideSeconds;
            ReplaceKey = replaceKey;
            OnResult = onResult;
            Icon = icon;
        }

        public bool HasSecondary => !string.IsNullOrEmpty(SecondaryLabel);
    }
}
