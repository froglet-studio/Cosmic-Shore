using CosmicShore.UI;
using CosmicShore.Utility;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Thresher's launch sight (<c>R_VesselActions/THRESHER.md</c> §5a): while a planted spin is
    /// spectated, the launch camera sits ON the launch trajectory, behind the launch point with no
    /// height offset, so the trajectory is a single point dead centre of the screen — and this draws
    /// it as a <b>crosshair</b>. Beside it, a <b>zoom window shaped like the TIME petal</b> (the
    /// Plant is the Thresher's Time ability) looks down the same trajectory from the launch point,
    /// magnified by how deep the left trigger is pulled — the Serpent scope's zoom
    /// (<c>SERPENT_SNIPER_SCOPE.md</c>), in this ability's own petal.
    ///
    /// <para><b>Built the way the Serpent's eyepiece rounds taught</b> (vessel skill §4.aa):
    /// <list type="bullet">
    /// <item>The window is a <see cref="TimePetalImage"/>, a <c>RawImage</c> subclass that overrides
    /// only its outline — the surface that is known to render, re-shaped.</item>
    /// <item>Every mark (both crosshairs, the frame) is a plain <see cref="Image"/> quad — the
    /// component that provably draws on a generated canvas.</item>
    /// <item>Shown and hidden by the PANEL GameObjects, never by <c>Canvas.enabled</c> (which freezes
    /// UGUI's rebuilds for good).</item>
    /// <item>The zoom is a pure function of the trigger: nothing else narrows the window, and the
    /// flight view is never magnified (a magnified main camera made the Serpent's pilots sick).</item>
    /// </list></para>
    ///
    /// <para>Deliberately no <c>CanvasScaler</c> (sizes are fractions of the live screen height, as
    /// the Serpent's) and no raycaster (a hit target over the flight view would eat presses).
    /// Local pilot only — the caller owns that gate.</para>
    /// </summary>
    public sealed class ThresherLaunchSight : MonoBehaviour
    {
        // The zoom window's HEIGHT as a fraction of screen height; its width follows the petal.
        const float WindowHeightFraction = 0.45f;
        // The Serpent eyepiece's slot (clear of the goal stack: (16, -52) + three 48-unit rows).
        const float WindowTopMargin = 220f;
        const float WindowLeftMargin = 16f;
        // The dark frame round the picture, in pixels.
        const float FramePixels = 3f;

        // Crosshair proportions at 1080p, scaled with the screen.
        const float CrossGap = 9f;
        const float CrossArm = 22f;
        const float CrossThickness = 3f;
        const float CentreDot = 4f;
        const float ReferenceHeight = 1080f;

        RectTransform _flightCross;
        Image[] _flightMarks;
        RectTransform _window;
        RectTransform _windowCross;
        Image[] _windowMarks;
        TimePetalImage _picture;
        ScopePipView _pip;

        /// <summary>Build the sight on a new GameObject, hidden.</summary>
        public static ThresherLaunchSight Create(string owner)
        {
            var go = new GameObject($"[ThresherLaunchSight {owner}]");
            var sight = go.AddComponent<ThresherLaunchSight>();
            sight.Build();
            sight.Hide();
            return sight;
        }

        void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // Above the vessel HUD: a sight that something covered would be the bug.
            canvas.sortingOrder = 500;

            // The flight view's crosshair: the screen centre IS the trajectory (the camera is on it).
            _flightCross = MakeRoot("FlightCrosshair", transform, new Vector2(0.5f, 0.5f));
            _flightMarks = MakeCross(_flightCross);

            // The zoom window: a dark Time petal frame, the picture inside it, a crosshair on top.
            _window = new GameObject("ZoomWindow", typeof(RectTransform)).GetComponent<RectTransform>();
            _window.SetParent(transform, false);
            _window.anchorMin = _window.anchorMax = new Vector2(0f, 1f);
            _window.pivot = new Vector2(0f, 1f);

            var frame = MakePetal("Frame", 0f);
            frame.color = new Color(0.02f, 0.025f, 0.06f, 0.92f);
            _picture = MakePetal("Picture", FramePixels);
            _picture.color = Color.white;

            _windowCross = MakeRoot("WindowCrosshair", _window, new Vector2(0.5f, 0.5f));
            _windowMarks = MakeCross(_windowCross);

            _pip = new ScopePipView(_picture)
            {
                RenderAspect = TimePetalGeometry.Aspect,
                Label = "ThresherSight",
            };
        }

        static RectTransform MakeRoot(string name, Transform parent, Vector2 anchor)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
            return rect;
        }

        /// <summary>Four arms with a gap, and a centre dot: five plain Image quads.</summary>
        static Image[] MakeCross(RectTransform root)
        {
            var marks = new Image[5];
            for (int i = 0; i < marks.Length; i++)
            {
                var rect = MakeRoot(i == 0 ? "Dot" : $"Arm{i}", root, new Vector2(0.5f, 0.5f));
                var img = rect.gameObject.AddComponent<Image>();
                img.raycastTarget = false;
                marks[i] = img;
            }
            return marks;
        }

        TimePetalImage MakePetal(string name, float inset)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(_window, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
            var petal = rect.gameObject.AddComponent<TimePetalImage>();
            petal.raycastTarget = false;
            return petal;
        }

        /// <summary>
        /// Show and advance the sight. <paramref name="launchPoint"/> / <paramref name="aim"/> /
        /// <paramref name="aimUp"/> are the launch trajectory and its "up"; <paramref name="fieldOfView"/>
        /// the window's vertical FOV (a pure function of the trigger); <paramref name="tint"/> the
        /// crosshairs' colour.
        /// </summary>
        public void Tick(Vector3 launchPoint, Vector3 aim, Vector3 aimUp, float fieldOfView, Color tint)
        {
            SetVisible(true);   // FIRST: a graphic must be active when told its geometry changed

            float scale = Screen.height / ReferenceHeight;
            LayOutCross(_flightMarks, scale, tint);

            float height = Mathf.Max(120f, Screen.height * WindowHeightFraction);
            _window.sizeDelta = new Vector2(height * TimePetalGeometry.Aspect, height);
            _window.anchoredPosition = new Vector2(WindowLeftMargin, -WindowTopMargin);
            LayOutCross(_windowMarks, scale, tint);

            if (aim.sqrMagnitude < 1e-6f) return;
            Vector3 up = aimUp.sqrMagnitude > 1e-6f ? aimUp : Vector3.up;
            if (!SafeLookRotation.TryGet(aim, up, out var rotation, this, logError: false)) return;
            _pip.Tick(launchPoint, rotation, fieldOfView);
        }

        static void LayOutCross(Image[] marks, float scale, Color tint)
        {
            float gap = CrossGap * scale, arm = CrossArm * scale, t = Mathf.Max(1f, CrossThickness * scale);
            float reach = gap + arm * 0.5f;
            var upright = new Vector2(t, arm);
            var across = new Vector2(arm, t);
            Place(marks[0], Vector2.zero, Vector2.one * Mathf.Max(2f, CentreDot * scale), tint);
            Place(marks[1], new Vector2(0f, reach), upright, tint);
            Place(marks[2], new Vector2(0f, -reach), upright, tint);
            Place(marks[3], new Vector2(-reach, 0f), across, tint);
            Place(marks[4], new Vector2(reach, 0f), across, tint);
        }

        static void Place(Image mark, Vector2 at, Vector2 size, Color tint)
        {
            var rect = mark.rectTransform;
            rect.anchoredPosition = at;
            rect.sizeDelta = size;
            mark.color = tint;
        }

        public void Hide()
        {
            SetVisible(false);
            _pip?.Hide();
        }

        /// <summary>By the panels' GameObjects, never <c>Canvas.enabled</c> (see the class doc).</summary>
        void SetVisible(bool visible)
        {
            if (_flightCross) _flightCross.gameObject.SetActive(visible);
            if (_window) _window.gameObject.SetActive(visible);
        }

        void OnDestroy()
        {
            // The window's camera and render target outlive nothing: a leaked RT reports nothing.
            _pip?.Dispose();
            _pip = null;
        }
    }
}
