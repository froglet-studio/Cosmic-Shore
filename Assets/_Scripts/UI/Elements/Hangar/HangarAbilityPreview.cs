using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// The Hangar's TRY: the selected vessel's REAL abilities, flown by the player (T-20261016-1ec1;
    /// Docs/MENU_REDESIGN_B.md section 35). The stage's stand-in lunge / roll / light stays for a quick
    /// look; TRY borrows the menu's one <see cref="ModePreviewSession"/> - the same one the arcade
    /// configure window flies - with a bare Barren-cell definition, so the vessel stands in open water,
    /// the session hands the player the stick the moment its model is up, and RT on a Sparrow fires
    /// Pulsefire for real. The session's drill (the Lesson + Mentor) coaches the vessel's own abilities
    /// over the picture.
    ///
    /// <para><b>Borrowed, then given back.</b> The session is the configure window's too, so TRY
    /// attaches it only for its own run and always strikes and detaches it on the way out - when the
    /// player hands the stick back (START, Esc, STOP, a tap outside), changes vessel, leaves the tab or
    /// the screen. The session restores the player's own hull, pose, AI target and pause itself.</para>
    /// </summary>
    public class HangarAbilityPreview : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField, Tooltip("The preview window laid over the Hangar's 3D stage.")]
        private ModePreviewWindow window;
        [SerializeField, Tooltip("The window's group: shown only while TRY runs.")]
        private CanvasGroup windowGroup;
        [SerializeField, Tooltip("'START or Esc to stop' + STOP, shown while TRY runs (STOP cancels a slow build too).")]
        private CanvasGroup flyingHint;
        [SerializeField, Tooltip("The bare arena TRY stands (Barren cell, no objective, no clock).")]
        private ModePreviewDefinitionSO definition;
        [SerializeField, Tooltip("Optional: resolved from the scene when empty (the menu holds one).")]
        private ModePreviewSession session;

        [Header("Words")]
        [Tooltip("The name the drill's coach uses for this place (its {mode} token).")]
        [SerializeField] private string placeName = "Hangar";

        SO_Vessel _vessel;
        bool _running, _requested, _flown;

        public bool IsRunning => _running;

        void Awake() => Show(false);

        void OnDisable() => End();

        /// <summary>The selected vessel changed: a run on the old hull ends.</summary>
        public void SetVessel(SO_Vessel vessel)
        {
            if (vessel != _vessel) End();
            _vessel = vessel;
        }

        /// <summary>TRY: stand the bare arena with the selected hull and fly it.</summary>
        public void Try()
        {
            if (_running || !_vessel || !definition || !window) return;
            if (!session) session = FindFirstObjectByType<ModePreviewSession>(FindObjectsInactive.Exclude);
            if (!session) { CSDebug.LogWarning("[Hangar] TRY needs a ModePreviewSession in the scene."); return; }

            _running = true;
            _requested = _flown = false;
            Show(true);
            session.Attach(window);
            session.SetDrillNames(placeName, _vessel.Name);
            session.SetDefinition(definition, _vessel.Class);
        }

        /// <summary>STOP, or anything else that ends the run. Idempotent.</summary>
        public void End()
        {
            if (!_running) return;
            _running = false;
            if (window && window.HasFocus) window.ReleaseFocus();
            if (session)
            {
                session.Stop();
                session.Detach();
            }
            if (window) window.Hide();
            Show(false);
        }

        void Update()
        {
            if (!_running || !window) return;

            // The model is up and its camera draws: ask for the stick at once (the real cell builds
            // behind the model). TRY is the player's tap, so no second tap is asked of them.
            if (!_requested && window.IsLive && !window.HasFocus)
            {
                _requested = true;
                window.RequestFocus();
            }

            if (window.HasFocus) _flown = true;
            else if (_flown) End();            // handed back: START, Esc, STOP or a tap outside
        }

        void Show(bool shown)
        {
            KitVisibility.SetShown(windowGroup, shown);
            KitVisibility.SetShown(flyingHint, shown);
        }
    }
}
