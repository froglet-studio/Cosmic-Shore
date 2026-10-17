using System.Collections.Generic;
using CosmicShore.Data;
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
    ///
    /// <para><b>The showcase</b> (Docs/MENU_REDESIGN_B.md section 51; the owner, 2026-10-18: "each vessel should show
    /// each powerup animation"). Pressing an ability in the Hangar stands the same arena with the autopilot flying
    /// (<see cref="ModePreviewSession.DemoPilot"/>) and presses each of the vessel's abilities in turn through its own
    /// action handler, so every ability plays its REAL effect - the shot, the trail, the field - starting with the
    /// one pressed; pressing another jumps to it. TRY from there hands the player the stick.</para>
    public class HangarAbilityPreview : MonoBehaviour
    {
        /// <summary>One ability the showcase plays: its element (the row it lights) and the control that fires it.</summary>
        public readonly struct ShowcaseAbility
        {
            public readonly Element Element;
            public readonly InputEvents Input;
            /// <summary>False for a passive ability: its row lights, nothing is pressed.</summary>
            public readonly bool Presses;

            public ShowcaseAbility(Element element, InputEvents input, bool presses)
            {
                Element = element;
                Input = input;
                Presses = presses;
            }
        }

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
        [SerializeField, Tooltip("The 3D stage under the window: it stops rendering while TRY covers it.")]
        private HangarVesselStage stage;

        [Header("Words")]
        [Tooltip("The name the drill's coach uses for this place (its {mode} token).")]
        [SerializeField] private string placeName = "Hangar";

        [Header("Showcase")]
        [Tooltip("Seconds each ability's control is held (long enough for a held ability - a boost, a beam - to show).")]
        [SerializeField, Min(0.05f)] private float showcaseHoldSeconds = 1.4f;
        [Tooltip("Seconds after letting go before the next ability (the effect plays out).")]
        [SerializeField, Min(0f)] private float showcaseGapSeconds = 2.2f;
        [Tooltip("Seconds of flight before the first press, so the vessel is under way.")]
        [SerializeField, Min(0f)] private float showcaseLeadInSeconds = 0.8f;

        SO_Vessel _vessel;
        bool _running, _requested, _flown;
        bool _showcase;
        readonly List<ShowcaseAbility> _abilities = new();
        int _index;
        float _clock;
        bool _pressed, _leadDone, _stepStarted;

        public bool IsRunning => _running;
        /// <summary>The autopilot flies and the showcase presses; false while the player flies (TRY).</summary>
        public bool IsShowcase => _running && _showcase;

        /// <summary>The showcase pressed this element's ability (its row lights).</summary>
        public event System.Action<Element> ShowcasePlayed;

        /// <summary>The abilities the showcase plays for the selected vessel, in the rows' order.</summary>
        public void SetShowcaseAbilities(IReadOnlyList<ShowcaseAbility> abilities)
        {
            ReleasePress();
            _abilities.Clear();
            if (abilities != null)
                for (int i = 0; i < abilities.Count; i++) _abilities.Add(abilities[i]);
            _index = 0;
        }

        /// <summary>
        /// Play the vessel's real abilities, starting with <paramref name="element"/>'s. Stands the arena with the
        /// autopilot flying when nothing runs; jumps to that ability when the showcase already runs; does nothing while
        /// the player flies (their own press fires it).
        /// </summary>
        public void Showcase(Element element)
        {
            int at = _abilities.FindIndex(a => a.Element == element);
            if (at < 0) return;
            if (_running)
            {
                if (!_showcase) return;
                ReleasePress();
                _index = at;
                _clock = 0f;
                _stepStarted = false;
                return;
            }
            if (!Begin(demo: true)) return;
            _index = at;
            _clock = 0f;
            _leadDone = _stepStarted = false;
        }

        void Awake() => Show(false);

        void OnDisable() => End();

        /// <summary>The selected vessel changed: a run on the old hull ends.</summary>
        public void SetVessel(SO_Vessel vessel)
        {
            if (vessel != _vessel) End();
            _vessel = vessel;
        }

        /// <summary>TRY: stand the bare arena with the selected hull and fly it. During the showcase: take the stick.</summary>
        public void Try()
        {
            if (_running)
            {
                if (!_showcase) return;
                ReleasePress();
                _showcase = false;
                if (session) session.DemoPilot = false;
                return;
            }
            Begin(demo: false);
        }

        bool Begin(bool demo)
        {
            if (!_vessel || !definition || !window) return false;
            if (!session) session = FindFirstObjectByType<ModePreviewSession>(FindObjectsInactive.Exclude);
            if (!session) { CSDebug.LogWarning("[Hangar] TRY needs a ModePreviewSession in the scene."); return false; }

            _running = true;
            _showcase = demo;
            _requested = _flown = false;
            Show(true);
            session.Attach(window);
            session.SetDrillNames(placeName, _vessel.Name);
            session.SetDefinition(definition, _vessel.Class);
            session.DemoPilot = demo;     // after: a SetDefinition that stops an old run clears it
            return true;
        }

        /// <summary>STOP, or anything else that ends the run. Idempotent.</summary>
        public void End()
        {
            if (!_running) return;
            ReleasePress();
            _running = false;
            _showcase = false;
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
            else if (_flown) { End(); return; }   // handed back: START, Esc, STOP or a tap outside

            if (_showcase && session && session.IsFlying) TickShowcase(Time.unscaledDeltaTime);
        }

        /// <summary>Lead-in, then each ability: hold its control, let go, let the effect play out, next (and round).</summary>
        void TickShowcase(float dt)
        {
            if (_abilities.Count == 0) return;
            _clock += dt;
            if (!_leadDone)
            {
                if (_clock < showcaseLeadInSeconds) return;
                _leadDone = true;
                _clock = 0f;
            }

            var ability = _abilities[_index % _abilities.Count];
            if (!_stepStarted)
            {
                _stepStarted = true;
                _clock = 0f;
                ShowcasePlayed?.Invoke(ability.Element);
                if (ability.Presses) _pressed = session.PressAbility(ability.Input, true);
            }
            if (_pressed && _clock >= showcaseHoldSeconds) ReleasePress();
            if (_clock >= showcaseHoldSeconds + showcaseGapSeconds)
            {
                _index = (_index + 1) % _abilities.Count;
                _stepStarted = false;
            }
        }

        void ReleasePress()
        {
            if (!_pressed) return;
            _pressed = false;
            var ability = _abilities.Count > 0 ? _abilities[_index % _abilities.Count] : default;
            if (session) session.PressAbility(ability.Input, false);
        }

        void Show(bool shown)
        {
            if (stage) stage.Covered = shown;
            KitVisibility.SetShown(windowGroup, shown);
            KitVisibility.SetShown(flyingHint, shown);
        }
    }
}
