using TMPro;
using CosmicShore.UI;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The freestyle counterpart of the game scenes' connecting-screen hold: when a cell
    /// spawns a prepopulated environment in a scene WITHOUT an arena gate (Menu_Main's lava
    /// lamp), this veil covers the screen, brackets
    /// <see cref="PrismTrailBuilder.SetLoadGateHolding"/> at a gentler 80ms lay slice
    /// (<see cref="VeilLayBudgetMs"/> - the scene's Netcode/Relay/audio keep breathing under
    /// the veil, unlike the quiescent game scenes that take the full 250ms), and releases
    /// (fading out - continuity of existence applies to UI too) only when
    /// <see cref="PrismTrailBuilder.PollArenaReady"/> reports every prism laid, created, and
    /// grown. Cell.SpawnEnvironment DEFERS the build until the scene has finished booting -
    /// building during boot starved audio into underruns and wedged a clone batch (the
    /// 10,496-prism freeze); streaming ungated under live gameplay crashed outright. The
    /// overlay is created programmatically like SceneTransitionManager's fade and blocks
    /// pointer input while held. Scene-local: a scene change destroys it and OnDestroy
    /// closes the gate bracket.
    ///
    /// <para><b>The boot world is built BEFORE the screen is revealed</b> (the owner, 2026-10-15:
    /// "I don't want the user to join and see another screen growing Garland - load it first").
    /// The menu used to appear, then this veil rose over it a second or more later ("GROWING
    /// GARLAND... BUILDING ARENA 45%"), then the menu came back: two reveals and a separate
    /// loading screen in between. <see cref="Cover"/> raises it from the cell's first frame,
    /// opaque and black like the scene transition it follows, so the player sees one fade into
    /// a finished world. The build itself still waits for the scene to settle, and
    /// <see cref="Hold"/> engages the load gate when it starts. The readout is a slim bar and a
    /// "Loading" caption at the bottom of the screen, not a headline.</para>
    /// </summary>
    public sealed class EnvironmentLoadVeil : MonoBehaviour
    {
        static EnvironmentLoadVeil s_active;

        /// <summary>Raised as a veil starts revealing the scene - the menu plays its entrances on it.</summary>
        public static event System.Action Released;

        /// <summary>True while a veil covers the screen.</summary>
        public static bool IsCovering => s_active != null && !s_active._released;

        const float FadeInSeconds = 0.2f;
        const float FadeOutSeconds = 0.45f;

        /// <summary>A cover the build never claimed (a config with nothing to build after all)
        /// lets go after this long rather than holding a finished scene behind black.</summary>
        const float UnclaimedCoverSeconds = 20f;

        const string CaptionFontPath = "Fonts & Materials/ChakraPetch-Regular SDF";
        const float BarWidth = 180f;
        const float BarHeight = 2f;

        CanvasGroup _group;
        RectTransform _content;
        RectTransform _fill;
        bool _gated;
        bool _released;
        float _fade;
        float _clock;

        /// <summary>
        /// Cover the screen NOW, without the load gate: the boot world's build is still waiting for
        /// the scene to settle. Opaque from the first frame, so it continues the scene transition's
        /// black instead of fading in over a menu the player has already seen. No-op if a veil is up
        /// or a gated scene (a connecting screen) owns the hold.
        /// </summary>
        public static void Cover(string environmentName)
        {
            if (s_active != null || PrismTrailBuilder.IsLoadGateHolding) return;
            var go = new GameObject("EnvironmentLoadVeil");
            s_active = go.AddComponent<EnvironmentLoadVeil>();
            s_active._fade = 1f;
            s_active._group.alpha = 1f;
        }

        /// <summary>
        /// Raise the veil and engage the load gate (idempotent - a second environment in the same
        /// hold, or the build claiming an earlier <see cref="Cover"/>, just engages). Call
        /// IMMEDIATELY BEFORE starting the environment's budgeted lay so the gate boost applies
        /// from the first slice and the ready poll never sees a pre-lay all-clear.
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
                s_active.Engage();
                return;
            }
            // A gated scene (connecting screen) already owns the hold - no second veil.
            if (PrismTrailBuilder.IsLoadGateHolding) return;
            var go = new GameObject("EnvironmentLoadVeil");
            s_active = go.AddComponent<EnvironmentLoadVeil>();
            s_active.Engage();
        }

        /// <summary>Gate slice while the veil holds: enough for a fast build (~10x the ungated
        /// pace) while leaving most of each frame for the services still running under the menu
        /// (Netcode heartbeats, Relay/session traffic, audio) - the full 250ms connecting-screen
        /// slice starved them into buffer underruns.</summary>
        const float VeilLayBudgetMs = 80f;

        void Engage()
        {
            if (_gated) return;
            _gated = true;
            // Gate ON before the first lay slice (the caller's Spawn() runs right after and sees
            // the boosted budget immediately).
            PrismTrailBuilder.SetLoadGateHolding(true);
            PrismTrailBuilder.LoadGateLayBudgetOverrideMs = VeilLayBudgetMs;
        }

        void Awake()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000; // above every menu canvas, under the transition fade
            // Scaled like every style-B canvas, so the readout is the same size on a phone, a 4K
            // monitor and a tablet (it was raw pixels: tiny at 4K, huge on a small phone).
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            gameObject.AddComponent<GraphicRaycaster>();
            _group = gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 0f;
            // Blocks pointer/touch input while the world builds (gamepad navigation is not
            // raycast-based - a pad-triggered scene change is still leak-safe because
            // OnDestroy closes the gate bracket before the next scene opens its own).
            _group.blocksRaycasts = true;

            // Black, the scene transition's colour, so a cover raised behind that fade is seamless.
            var back = new GameObject("Backdrop", typeof(Image));
            back.transform.SetParent(transform, false);
            back.GetComponent<Image>().color = Color.black;
            Stretch(back.GetComponent<RectTransform>());

            // The backdrop bleeds and the readout does not - the safe-area split of
            // Docs/UI_ARCHITECTURE_AUDIT.md §1.3. An occluding veil that stopped at the safe area
            // would show strips of the half-built world under the cutout.
            _content = SafeAreaLayer.Create(transform);
            BuildReadout();
        }

        /// <summary>The subtle readout: a "Loading" caption over a slim bar, bottom centre.</summary>
        void BuildReadout()
        {
            var caption = new GameObject("Caption", typeof(TextMeshProUGUI));
            caption.transform.SetParent(_content, false);
            var text = caption.GetComponent<TextMeshProUGUI>();
            var font = Resources.Load<TMP_FontAsset>(CaptionFontPath);
            if (font) text.font = font;
            text.text = "LOADING";
            text.fontSize = 14f;
            text.characterSpacing = 12f;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            var neutral = UITheme.Resolve(null, UIColorToken.NeutralLightest);
            text.color = new Color(neutral.r, neutral.g, neutral.b, 0.6f);
            Place(caption.GetComponent<RectTransform>(), new Vector2(0f, 84f), new Vector2(BarWidth * 2f, 20f));

            var track = new GameObject("Track", typeof(Image));
            track.transform.SetParent(_content, false);
            var trackImage = track.GetComponent<Image>();
            var light = UITheme.Resolve(null, UIColorToken.TextLight);
            trackImage.color = new Color(light.r, light.g, light.b, 0.12f);
            trackImage.raycastTarget = false;
            var trackRect = track.GetComponent<RectTransform>();
            Place(trackRect, new Vector2(0f, 64f), new Vector2(BarWidth, BarHeight));

            var fill = new GameObject("Fill", typeof(Image));
            fill.transform.SetParent(trackRect, false);
            var fillImage = fill.GetComponent<Image>();
            fillImage.color = UITheme.Resolve(null, UIColorToken.ControlActive);
            fillImage.raycastTarget = false;
            _fill = fill.GetComponent<RectTransform>();
            _fill.anchorMin = new Vector2(0f, 0f);
            _fill.anchorMax = new Vector2(0f, 1f);
            _fill.pivot = new Vector2(0f, 0.5f);
            _fill.sizeDelta = new Vector2(0f, 0f);
        }

        static void Place(RectTransform rt, Vector2 fromBottom, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = fromBottom;
            rt.sizeDelta = size;
        }

        static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Where the bar's lit segment sits, 0-1 of the track: [start, end]. Before the build starts
        /// (the scene is still settling) a short segment drifts back and forth - honest about not
        /// knowing yet; laying fills to the lay progress; settling holds it full.
        /// </summary>
        public static Vector2 BarSpan(bool laying, float layProgress, bool settling, float clock)
        {
            if (laying) return new Vector2(0f, Mathf.Clamp01(layProgress));
            if (settling) return new Vector2(0f, 1f);
            const float segment = 0.22f;
            float p = Mathf.PingPong(clock * 0.45f, 1f);
            float eased = p * p * (3f - 2f * p);
            float start = eased * (1f - segment);
            return new Vector2(start, start + segment);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (!_released)
            {
                _clock += dt;
                if (_fade < 1f)
                {
                    _fade = Mathf.Min(1f, _fade + dt / FadeInSeconds);
                    _group.alpha = _fade;
                }

                var span = BarSpan(PrismTrailBuilder.IsLayingInProgress, PrismTrailBuilder.LayProgress,
                                   PrismTrailBuilder.GrowRemainingCount > 0, _clock);
                _fill.anchoredPosition = new Vector2(span.x * BarWidth, 0f);
                _fill.sizeDelta = new Vector2((span.y - span.x) * BarWidth, 0f);

                if (_gated)
                {
                    // PollArenaReady force-settles behind the covered screen and self-releases
                    // on a 180s no-progress stall, so the veil can never hold a wedged build.
                    if (PrismTrailBuilder.PollArenaReady()) Release();
                }
                else if (_clock > UnclaimedCoverSeconds)
                {
                    Release();
                }
                return;
            }

            _fade -= dt / FadeOutSeconds;
            _group.alpha = Mathf.Max(0f, _fade);
            _group.blocksRaycasts = false;
            if (_fade <= 0f)
                Destroy(gameObject);
        }

        void Release()
        {
            _released = true;
            if (_gated)
            {
                PrismTrailBuilder.SetLoadGateHolding(false);
                PrismTrailBuilder.LoadGateLayBudgetOverrideMs = 0f;
            }
            Released?.Invoke();
        }

        void OnDestroy()
        {
            // A scene change mid-hold must not leave the global gate bracket open.
            if (!_released && _gated)
            {
                PrismTrailBuilder.SetLoadGateHolding(false);
                PrismTrailBuilder.LoadGateLayBudgetOverrideMs = 0f;
            }
            if (s_active == this)
                s_active = null;
        }
    }
}
