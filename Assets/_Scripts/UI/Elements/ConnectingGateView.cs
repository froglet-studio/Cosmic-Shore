using System;
using System.Collections;
using CosmicShore.Gameplay;
using TMPro;
using UnityEngine;
using CosmicShore.ScriptableObjects;

namespace CosmicShore.UI
{
    /// <summary>
    /// The loading screen into a game: Rev 91 F, "the tour" (Docs/MENU_REDESIGN_B.md section 173), on the Rev 33
    /// cover (section 99). It COVERS the arena - the sea over a solid base, both cut by a
    /// <see cref="HexApertureGraphic"/> - so nothing renders the world while it builds. ENTERING and the mode's name
    /// sit top left, the tour (<see cref="ConnectingTourView"/>) shows a part of the game per share of the load, and
    /// the one bar (<see cref="KitStepBar"/>, fed by <see cref="ConnectingPanelController"/>) runs along the bottom.
    /// At the end a hex opens from the centre into the arena and grows past the screen while the intro's bars come
    /// in.
    ///
    /// <para>A view only: <see cref="ConnectingPanelController"/> owns the load and feeds <see cref="Tick"/> the
    /// bar's value each frame. Every word is authored - ENTERING and LOADING come from the cell-change screen's own
    /// content asset, so the two screens can never disagree.</para>
    /// </summary>
    public sealed class ConnectingGateView : MonoBehaviour
    {
        [Header("Words")]
        [Tooltip("The cell-change screen's words: ENTERING and LOADING.")]
        [SerializeField] EnvironmentLoadVeilContentSO veilWords;

        [Header("Backdrop")]
        [Tooltip("The cover: the solid base and the sea over it. The hole is opened in all of them together.")]
        [SerializeField] HexApertureGraphic[] cover = Array.Empty<HexApertureGraphic>();
        [Tooltip("The hole's edge while the gate opens (an outline-only hex, under the same parent as the cover).")]
        [SerializeField] LoadCrystalGraphic gateRing;

        [Header("Brief")]
        [Tooltip("The small word over the mode's name.")]
        [SerializeField] TMP_Text enteringText;
        [Tooltip("The small word over the bar's step name.")]
        [SerializeField] TMP_Text loadingLabel;

        [Header("Tour")]
        [Tooltip("The parts of the game, one chapter per share of the load.")]
        [SerializeField] ConnectingTourView tour;

        [Header("Motion")]
        [Tooltip("Everything but the backdrop: fades out as the gate starts.")]
        [SerializeField] CanvasGroup content;
        [Tooltip("The sea's drifting fields, drawn between the cover's black base and its sea lines. They have no hole " +
                 "of their own, so they fade out with the content as the gate starts, before the hole has grown.")]
        [SerializeField] CanvasGroup backdrop;
        [Tooltip("Seconds from the arena being ready to the hole having grown past the screen.")]
        [SerializeField, Min(0.3f)] float gateSeconds = 1.7f;
        [Tooltip("The hole's radius once it has settled at the centre, before it bursts.")]
        [SerializeField, Min(10f)] float gateSettleRadius = 452f;

        Coroutine _gate;

        /// <summary>True while the gate is opening.</summary>
        public bool IsOpening => _gate != null;

        void Awake()
        {
            if (gateRing) gateRing.enabled = false;
        }

        void OnDisable()
        {
            if (_gate == null) return;
            StopCoroutine(_gate);
            _gate = null;
        }

        /// <summary>The screen's fixed words for this load. An empty word shows nothing.</summary>
        public void Bind()
        {
            SetText(enteringText, veilWords ? veilWords.enteringWord : string.Empty);
            SetText(loadingLabel, veilWords ? veilWords.loadingWord : string.Empty);
        }

        /// <summary>Start a load: everything at rest, the cover whole, the tour on its first chapter, the entrances replayed.</summary>
        public void BeginHold()
        {
            if (_gate != null)
            {
                StopCoroutine(_gate);
                _gate = null;
            }
            foreach (var c in cover)
                if (c) c.SetAperture(Vector2.zero, 0f);
            if (content) content.alpha = 1f;
            if (backdrop) backdrop.alpha = 1f;
            if (gateRing) gateRing.enabled = false;
            if (tour) tour.Begin();
            if (content) KitEntrance.ReplayUnder(content.transform);
        }

        /// <summary>One frame of the load. <paramref name="bar"/> is the eased 0-1 progress the bar draws.</summary>
        public void Tick(float bar)
        {
            if (tour) tour.Show(bar);
        }

        /// <summary>
        /// The arena is ready: the bar's done state holds for a beat, the tour and the bar leave, and a hex opens
        /// from the centre into the arena and bursts past the screen. <paramref name="done"/> runs when the screen is
        /// fully open (or at once when this view is inactive).
        /// </summary>
        public void Open(Action done)
        {
            if (!isActiveAndEnabled)
            {
                done?.Invoke();
                return;
            }
            if (_gate != null) StopCoroutine(_gate);
            _gate = StartCoroutine(Gate(done));
        }

        IEnumerator Gate(Action done)
        {
            float d = gateSeconds;
            float tHold = 0.18f * d, tExit = 0.15f * d, tOpen = 0.22f * d, tSet = 0.65f * d;
            float t = 0f;
            if (gateRing)
            {
                gateRing.enabled = false;
                gateRing.Spokes = false;
                gateRing.Glass = Color.clear;
            }

            while (t < d)
            {
                t += Mathf.Min(Time.unscaledDeltaTime, 1f / 20f);

                // The done state is read first, then the tour and the bar leave.
                float shown = 1f - KitMotion.Ease(KitEase.OutCubic, Mathf.Clamp01((t - tHold) / tExit));
                if (content) content.alpha = shown;
                if (backdrop) backdrop.alpha = shown;

                // The hole: opens at the centre and settles, then bursts.
                float radius;
                if (t < tOpen)
                    radius = 0f;
                else if (t < tSet)
                    radius = gateSettleRadius * KitMotion.Ease(KitEase.InOutSine, (t - tOpen) / (tSet - tOpen));
                else
                {
                    float u = Mathf.Clamp01((t - tSet) / (d - tSet));
                    radius = Mathf.Lerp(gateSettleRadius, CoverRadius(), u * u * u);
                }
                foreach (var g in cover)
                    if (g) g.SetAperture(Vector2.zero, radius);
                DrawRing(radius, t, tSet, d);
                yield return null;
            }

            foreach (var g in cover)
                if (g) g.SetAperture(Vector2.zero, CoverRadius());
            if (gateRing) gateRing.enabled = false;
            _gate = null;
            done?.Invoke();
        }

        void DrawRing(float radius, float t, float tSet, float d)
        {
            if (!gateRing) return;
            bool show = radius > 1f;
            if (gateRing.enabled != show) gateRing.enabled = show;
            if (!show) return;
            var rt = gateRing.rectTransform;
            rt.anchoredPosition = Vector2.zero;
            float size = LoadCrystalGraphic.SizeFor(radius);
            rt.sizeDelta = new Vector2(size, size);
            float fade = t < tSet ? 1f : 1f - Mathf.Clamp01((t - tSet) / (d - tSet));
            gateRing.Outline = new Color(1f, 1f, 1f, fade);
        }

        // A radius whose hex covers the whole cover rect from its centre.
        float CoverRadius()
        {
            if (cover.Length == 0 || !cover[0]) return 4000f;
            var size = cover[0].rectTransform.rect.size;
            // The hex's inscribed radius is r*sqrt(3)/2; it must reach the rect's corner.
            return size.magnitude * 0.5f / 0.8660254f + 8f;
        }

        static void SetText(TMP_Text label, string text)
        {
            if (!label) return;
            label.text = text ?? string.Empty;
        }
    }
}
