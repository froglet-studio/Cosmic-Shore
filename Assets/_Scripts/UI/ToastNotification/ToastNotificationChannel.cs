using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.App.UI.ToastNotification
{
    [CreateAssetMenu(
        fileName = "ToastNotificationChannel",
        menuName = "ScriptableObjects/UI/Toast Notification Channel")]
    public class ToastNotificationChannel : ScriptableEvent<string> { }
}
