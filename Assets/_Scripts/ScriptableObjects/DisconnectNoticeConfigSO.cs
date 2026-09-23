using UnityEngine;

namespace CosmicShore.ScriptableObjects
{
    /// <summary>
    /// Every string, colour and duration the disconnect notice uses.
    ///
    /// <para>It is a config asset rather than fields on the component for the reason
    /// <c>Docs/UI_ARCHITECTURE_AUDIT.md</c> §5.4 records against the rest of the UI layer: this
    /// project has no string table and no localization layer, so hard-coding user-facing copy in
    /// C# means a copy change is a code change. The notice is built at runtime (it has to exist in
    /// every scene, including game scenes, which no authored canvas does), and runtime-built UI is
    /// listed in §5.7 as a structural risk precisely because it is "not editable by a designer
    /// without touching C#". Putting the whole surface's text and palette in one asset is what
    /// keeps that true of the LAYOUT only.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "DisconnectNoticeConfig",
                     menuName = "ScriptableObjects/System/Disconnect Notice Config", order = 40)]
    public class DisconnectNoticeConfigSO : ScriptableObject
    {
        [Header("Copy")]
        [Tooltip("Heading shown when the device loses its connection.")]
        public string TitleConnectionLost = "Connection Lost";

        [TextArea(2, 4)]
        [Tooltip("Body shown when the device loses its connection. Kept factual: the player's " +
                 "session keeps running locally, which is what the offline host provides.")]
        public string BodyConnectionLost =
            "You're offline. You can keep playing on your own — online features will return when " +
            "the connection does.";

        [Tooltip("Label of the control that re-runs the boot chain (ReconnectService).")]
        public string ReconnectLabel = "Reconnect";

        [Tooltip("Label while a reconnect attempt is in flight.")]
        public string ReconnectingLabel = "Reconnecting…";

        [Tooltip("Label of the control that leaves the notice and carries on offline.")]
        public string DismissLabel = "Continue Offline";

        [Tooltip("Shown briefly when the connection comes back on its own, then auto-hides.")]
        public string TitleConnectionRestored = "Back Online";

        [Header("Behaviour")]
        [Min(0f)]
        [Tooltip("Seconds the restored notice stays up before hiding itself. 0 hides immediately.")]
        public float RestoredDwellSeconds = 2.5f;

        [Min(0f)]
        [Tooltip("Fade duration for the whole surface.")]
        public float FadeSeconds = 0.2f;

        [Tooltip("Suppress the notice while a game scene is loading. The launch splash is already " +
                 "opaque there and owns its own failure copy (BootStatusBroadcaster deliberately " +
                 "suppresses connection-lost inside that window rather than showing a misleading " +
                 "'tap retry'); a second surface over it would say the same thing twice.")]
        public bool SuppressDuringLaunch = true;

        [Header("Palette")]
        [Tooltip("Full-screen scrim behind the panel.")]
        public Color ScrimColor = new(0f, 0f, 0f, 0.72f);

        [Tooltip("Panel body.")]
        public Color PanelColor = new(0.10f, 0.10f, 0.15f, 0.98f);

        [Tooltip("Title text.")]
        public Color TitleColor = Color.white;

        [Tooltip("Body text.")]
        public Color BodyColor = new(0.78f, 0.80f, 0.86f, 1f);

        [Tooltip("The primary control (Reconnect).")]
        public Color PrimaryButtonColor = new(0.05f, 0.75f, 0.71f, 1f);

        [Tooltip("The secondary control (Continue Offline).")]
        public Color SecondaryButtonColor = new(0.22f, 0.22f, 0.28f, 1f);

        [Header("Layout")]
        [Tooltip("Panel size in reference pixels (the canvas matches the project's 1920x1080).")]
        public Vector2 PanelSize = new(760f, 340f);

        [Tooltip("Sorting order of the notice's own canvas. Above the gameplay HUD and the menu, " +
                 "below nothing - a disconnect notice the player cannot see is the whole defect " +
                 "this closes.")]
        public int SortingOrder = 32000;
    }
}
