using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The freestyle counterpart of the game scenes' connecting-screen hold: when a cell
    /// spawns a prepopulated environment in a scene WITHOUT an arena gate (Menu_Main's lava
    /// lamp), this veil covers the screen with the connecting panel's status idiom, brackets
    /// <see cref="PrismTrailBuilder.SetLoadGateHolding"/> at a gentler 80ms lay slice
    /// (<see cref="VeilLayBudgetMs"/> - the scene's Netcode/Relay/audio keep breathing under
    /// the veil, unlike the quiescent game scenes that take the full 250ms), and releases
    /// (fading out - continuity of existence applies to UI too) only when
    /// <see cref="PrismTrailBuilder.PollArenaReady"/> reports every prism laid, created, and
    /// grown. Cell.SpawnEnvironment DEFERS raising it until the scene has finished booting -
    /// building during boot starved audio into underruns and wedged a clone batch (the
    /// 10,496-prism freeze); streaming ungated under live gameplay crashed outright. The
    /// overlay is created programmatically like SceneTransitionManager's fade and blocks
    /// pointer input while held. Scene-local: a scene change destroys it and OnDestroy
    /// closes the gate bracket.
    /// </summary>
    public sealed class EnvironmentLoadVeil : MonoBehaviour
    {
        static EnvironmentLoadVeil s_active;

        const float FadeInSeconds = 0.2f;
        const float FadeOutSeconds = 0.45f;

        string _label = "ENVIRONMENT";
        CanvasGroup _group;
        TextMeshProUGUI _title;
        TextMeshProUGUI _progress;
        bool _released;
        float _fade;
        float _dotTimer;

        /// <summary>
        /// Raise the veil (idempotent - a second environment in the same hold just relabels).
        /// Call IMMEDIATELY BEFORE starting the environment's budgeted lay so the gate boost
        /// applies from the first slice and the ready poll never sees a pre-lay all-clear.
        /// </summary>
        public static void Hold(string environmentName)
        {
            // A released veil mid-fade-out no longer holds the gate - retire it and raise a
            // fresh one rather than relabeling a dying overlay over an ungated build.
            if (s_active != null && s_active._released)
            {
                Destroy(s_active.gameObject);
                s_active = null;
            }
            if (s_active != null)
            {
                s_active._label = environmentName;
                return;
            }
            // A gated scene (connecting screen) already owns the hold - no second veil.
            if (PrismTrailBuilder.IsLoadGateHolding) return;
            var go = new GameObject("EnvironmentLoadVeil");
            s_active = go.AddComponent<EnvironmentLoadVeil>();
            s_active._label = environmentName;
        }

        /// <summary>Gate slice while the veil holds: enough for a fast build (~10x the ungated
        /// pace) while leaving most of each frame for the services still running under the menu
        /// (Netcode heartbeats, Relay/session traffic, audio) - the full 250ms connecting-screen
        /// slice starved them into buffer underruns.</summary>
        const float VeilLayBudgetMs = 80f;

        void Awake()
        {
            // Gate ON before the first lay slice (AddComponent runs Awake synchronously,
            // so the caller's subsequent Spawn() sees the boosted budget immediately).
            PrismTrailBuilder.SetLoadGateHolding(true);
            PrismTrailBuilder.LoadGateLayBudgetOverrideMs = VeilLayBudgetMs;

            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000; // above every menu canvas, like the transition fade
            gameObject.AddComponent<GraphicRaycaster>();
            _group = gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            // Blocks pointer/touch input while the world builds (gamepad navigation is not
            // raycast-based - a pad-triggered scene change is still leak-safe because
            // OnDestroy closes the gate bracket before the next scene opens its own).
            _group.blocksRaycasts = true;

            var back = new GameObject("Backdrop", typeof(Image));
            back.transform.SetParent(transform, false);
            var backImage = back.GetComponent<Image>();
            backImage.color = new Color(0.02f, 0.03f, 0.06f, 1f);
            Stretch(back.GetComponent<RectTransform>());

            _title = MakeLabel("Title", 44f, new Vector2(0f, 60f));
            _progress = MakeLabel("Progress", 26f, new Vector2(0f, 0f));
        }

        TextMeshProUGUI MakeLabel(string name, float size, Vector2 offset)
        {
            var go = new GameObject(name, typeof(TextMeshProUGUI));
            go.transform.SetParent(transform, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(0.85f, 0.9f, 1f, 1f);
            var rt = go.GetComponent<RectTransform>();
            Stretch(rt);
            rt.anchoredPosition = offset;
            return text;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        void Update()
        {
            if (!_released)
            {
                _fade = Mathf.Min(1f, _fade + Time.unscaledDeltaTime / FadeInSeconds);
                _group.alpha = _fade;

                // Same status idiom as ConnectingPanelController so freestyle loads read as
                // the same family as the minigame connecting screens.
                _dotTimer += Time.unscaledDeltaTime;
                int dots = 1 + (int)(_dotTimer / 0.35f) % 4;
                _title.text = $"GROWING {_label.ToUpperInvariant()}{new string('.', dots)}";
                if (PrismTrailBuilder.IsLayingInProgress)
                    _progress.text =
                        $"BUILDING ARENA  {PrismTrailBuilder.LayProgress:P0}  " +
                        $"({PrismTrailBuilder.LayDoneCount:N0} / {PrismTrailBuilder.LayQueuedCount:N0})  ·  {_dotTimer:F0}s";
                else if (PrismTrailBuilder.GrowRemainingCount > 0)
                    _progress.text =
                        $"GROWING ARENA  ({PrismTrailBuilder.GrowRemainingCount:N0} settling)  ·  {_dotTimer:F0}s";
                else
                    _progress.text = $"·  {_dotTimer:F0}s";

                // PollArenaReady force-settles behind the covered screen and self-releases
                // on a 180s no-progress stall, so the veil can never hold a wedged build.
                if (PrismTrailBuilder.PollArenaReady())
                {
                    _released = true;
                    PrismTrailBuilder.SetLoadGateHolding(false);
                    PrismTrailBuilder.LoadGateLayBudgetOverrideMs = 0f;
                }
                return;
            }

            _fade -= Time.unscaledDeltaTime / FadeOutSeconds;
            _group.alpha = Mathf.Max(0f, _fade);
            _group.blocksRaycasts = false;
            if (_fade <= 0f)
                Destroy(gameObject);
        }

        void OnDestroy()
        {
            // A scene change mid-hold must not leave the global gate bracket open.
            if (!_released)
            {
                PrismTrailBuilder.SetLoadGateHolding(false);
                PrismTrailBuilder.LoadGateLayBudgetOverrideMs = 0f;
            }
            if (s_active == this)
                s_active = null;
        }
    }
}
