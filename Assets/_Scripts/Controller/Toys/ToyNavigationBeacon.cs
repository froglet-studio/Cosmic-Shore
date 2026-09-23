using CosmicShore.UI;
using CosmicShore.Utility;
using UnityEngine;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The arrow that points at the toy you asked to be taken to — the platform's own
    /// <see cref="ObjectiveIndicator"/>, borrowed for as long as the trip lasts.
    ///
    /// <para>The Toy Box's Navigate drops the player just outside the toy's ring facing it, so the
    /// arrow is usually invisible at the moment it is raised: the indicator hides itself whenever
    /// its target is on screen. It earns its place on the frames AFTER that — the player turns,
    /// drifts, gets distracted by the cell — and it retires itself the moment they arrive, so it is
    /// a hand-off aid rather than a permanent HUD element.</para>
    ///
    /// <para>It reuses <see cref="PaintingRunner"/>'s pattern rather than inventing a second one:
    /// ONE indicator, created at the CANVAS ROOT (the widget stretches to its parent and clamps to
    /// that rect's edges, so a mid-hierarchy container pins it in a corner), driven by a relay so
    /// the target can change without rebuilding the widget.</para>
    /// </summary>
    public static class ToyNavigationBeacon
    {
        /// <summary>Arrived once the vessel is inside this many ring radii of the toy.</summary>
        const float ArrivedRingFactor = 3.5f;

        /// <summary>
        /// A trip that never resolves still ends. The arrow is guidance, not a quest marker, and a
        /// pointer left up after the player has moved on to something else is noise.
        /// </summary>
        const float MaxTripSeconds = 90f;

        class Relay : IObjectiveProvider
        {
            public Transform Target;

            public bool TryGetObjective(out Transform target)
            {
                target = Target;
                return target;
            }
        }

        static readonly Relay s_relay = new();
        static ObjectiveIndicator s_indicator;
        static Driver s_driver;

        /// <summary>
        /// Point the arrow at <paramref name="toy"/> until the player reaches it, leaves freestyle,
        /// or the trip times out. Safe to call with nulls — it simply does not raise the arrow.
        /// </summary>
        public static void PointAt(Toy toy, IPlayer player, MenuCrystalClickHandler freestyle)
        {
            if (!toy) { Clear(); return; }

            s_relay.Target = toy.transform;
            EnsureIndicator();
            EnsureDriver().Begin(toy, player, freestyle);

            CSDebug.LogVerbose(CSLogChannel.ToyBox, $"[ToyBox] beacon -> {toy.DisplayName}");
        }

        /// <summary>Take the arrow down. Idempotent.</summary>
        public static void Clear() => s_relay.Target = null;

        static void EnsureIndicator()
        {
            if (s_indicator) return;

            var hud = Object.FindAnyObjectByType<MenuMiniGameHUD>(FindObjectsInactive.Include);
            Canvas canvas = hud ? hud.GetComponentInParent<Canvas>(true) : null;
            if (!canvas) canvas = Object.FindAnyObjectByType<Canvas>();
            if (!canvas) return; // headless/test scene - Navigate still works, just unguided

            s_indicator = ObjectiveIndicator.CreateRuntime(canvas.transform, s_relay);
        }

        static Driver EnsureDriver()
        {
            if (s_driver) return s_driver;

            var go = new GameObject("ToyNavigationBeacon") { hideFlags = HideFlags.HideAndDontSave };
            Object.DontDestroyOnLoad(go);
            s_driver = go.AddComponent<Driver>();
            return s_driver;
        }

        /// <summary>
        /// Watches the trip. Deliberately a component rather than a UniTask loop: it has to survive
        /// the modal that started it being closed, and its whole job is a per-frame distance test.
        /// </summary>
        class Driver : MonoBehaviour
        {
            Toy _toy;
            IPlayer _player;
            MenuCrystalClickHandler _freestyle;
            float _deadline;

            public void Begin(Toy toy, IPlayer player, MenuCrystalClickHandler freestyle)
            {
                _toy = toy;
                _player = player;
                _freestyle = freestyle;
                _deadline = Time.unscaledTime + MaxTripSeconds;
                enabled = true;
            }

            void Update()
            {
                if (s_relay.Target == null) { enabled = false; return; }

                if (!_toy || Time.unscaledTime > _deadline) { Finish("gave up"); return; }

                // Leaving freestyle ends the trip: the arrow points at a place in the lava lamp,
                // and out of freestyle the player is not going anywhere.
                if (_freestyle && !_freestyle.IsInFreestyle) { Finish("left freestyle"); return; }

                var vessel = _player?.Vessel?.Transform;
                if (!vessel) return;

                float arrived = Mathf.Max(1f, _toy.SwitchRingRadius) * ArrivedRingFactor;
                if ((vessel.position - _toy.transform.position).sqrMagnitude <= arrived * arrived)
                    Finish("arrived");
            }

            void Finish(string why)
            {
                CSDebug.LogVerbose(CSLogChannel.ToyBox, $"[ToyBox] beacon down ({why})");
                Clear();
                enabled = false;
            }
        }
    }
}
