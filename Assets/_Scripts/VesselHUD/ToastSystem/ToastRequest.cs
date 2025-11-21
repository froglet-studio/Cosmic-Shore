// using CosmicShore.Game.UI.Toast;
// using UnityEngine;
//
// public readonly struct ToastRequest
// {
//     public readonly string Message;
//     public readonly float Duration;
//     public readonly ToastAnimation Animation;
//     public readonly Sprite Icon;
//     public readonly Color? Accent;
//     public readonly int CountdownFrom;
//     public readonly bool AutoConfirmAtEnd;
//     public readonly string CountdownPrefix;
//
//     public ToastRequest(
//         string message,
//         float duration = 2.5f,
//         ToastAnimation animation = ToastAnimation.SlideFromRight,
//         Sprite icon = null,
//         Color? accent = null,
//         int countdownFrom = 0,
//         bool autoConfirmAtEnd = false,
//         string countdownPrefix = "")
//     {
//         Message = message;
//         Duration = duration;
//         Animation = animation;
//         Icon = icon;
//         Accent = accent;
//         CountdownFrom = countdownFrom;
//         AutoConfirmAtEnd = autoConfirmAtEnd;
//         CountdownPrefix = countdownPrefix;
//     }
//
//     public ToastRequest WithMessage(string msg) => new ToastRequest(
//         msg, Duration, Animation, Icon, Accent, CountdownFrom, AutoConfirmAtEnd, CountdownPrefix);
// }