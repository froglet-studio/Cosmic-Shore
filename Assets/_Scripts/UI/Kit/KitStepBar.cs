using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The loading bar of Rev 91 (Docs/MENU_REDESIGN_B.md sections 173, 176): one bar cut into steps, each as long
    /// as its share of the work, with the running step's name over it and a percentage beside it. Used by the
    /// loading screen into a game (ConnectingPanel) and the boot splash.
    ///
    /// <para>It draws what it is told and measures nothing: the owner passes a 0..1 value that is already
    /// monotonic (<see cref="ArenaLoadProgress"/>, the boot's own model) through <see cref="Show"/>. The step
    /// shown is the one whose share the value has reached (<see cref="StepAt"/>), so the name over the bar and
    /// the fill can never disagree.</para>
    ///
    /// <para>Every word is an authored field: the step names, the done word and the percentage template. An
    /// empty field shows nothing.</para>
    /// </summary>
    [AddComponentMenu("Cosmic Shore/UI Kit/Kit Step Bar")]
    public class KitStepBar : MonoBehaviour
    {
        [Header("Fill")]
        [Tooltip("The lit part: a child of the track, anchored along its width by the bar.")]
        [SerializeField] RectTransform fill;
        [Tooltip("The fill's graphic, so the bar can show the done state on it.")]
        [SerializeField] Graphic fillGraphic;
        [Tooltip("Optional: a soft band that keeps travelling along the fill, so a slow step never looks frozen.")]
        [SerializeField] RectTransform glint;
        [Tooltip("Seconds the glint takes to cross the bar.")]
        [SerializeField, Min(0.1f)] float glintSeconds = 2.4f;

        [Header("Steps")]
        [Tooltip("Where each step ends, 0..1, rising; the last is 1. A step's length on the bar is its share of the work.")]
        [SerializeField] float[] stepEnds = { 0.22f, 0.64f, 0.86f, 1f };
        [Tooltip("The steps' names, in order. Shown over the bar while that step runs and under its segment.")]
        [SerializeField] string[] stepNames = new string[0];
        [Tooltip("One label per step under the bar (optional).")]
        [SerializeField] TMP_Text[] stepLabels = new TMP_Text[0];
        [Tooltip("One marker per step under the bar (optional): lit once the step has started, filled once it is done.")]
        [SerializeField] Graphic[] stepMarkers = new Graphic[0];

        [Header("Readout")]
        [Tooltip("The running step's name, over the bar.")]
        [SerializeField] TMP_Text stepText;
        [Tooltip("The percentage beside the bar.")]
        [SerializeField] TMP_Text percentText;
        [Tooltip("The percentage's words. {pct} is the whole number.")]
        [SerializeField] string percentTemplate = "{pct}";
        [Tooltip("Shown in place of a step's name once the bar is done.")]
        [SerializeField] string doneWord = "";

        float _shown = -1f;
        bool _done;
        int _step = -2;
        float _glintClock;

        /// <summary>The value last drawn, 0..1.</summary>
        public float Shown => Mathf.Max(0f, _shown);

        /// <summary>How many steps the bar is cut into.</summary>
        public int StepCount => stepEnds != null ? stepEnds.Length : 0;

        /// <summary>
        /// The step a value is in: the first whose end the value has not passed. Pure, so it is testable without
        /// a scene. An empty or null list answers 0.
        /// </summary>
        public static int StepAt(float[] ends, float value01)
        {
            if (ends == null || ends.Length == 0) return 0;
            for (int i = 0; i < ends.Length; i++)
                if (value01 < ends[i]) return i;
            return ends.Length - 1;
        }

        /// <summary>Draw a value. <paramref name="done"/> is the owner's word that the load is complete.</summary>
        public void Show(float value01, bool done)
        {
            value01 = Mathf.Clamp01(value01);
            if (Mathf.Approximately(value01, _shown) && done == _done) return;
            _shown = value01;
            _done = done;

            if (fill)
            {
                fill.anchorMin = new Vector2(0f, 0f);
                fill.anchorMax = new Vector2(value01, 1f);
                fill.offsetMin = Vector2.zero;
                fill.offsetMax = Vector2.zero;
            }

            if (percentText)
                percentText.text = TemplateText.Fill(percentTemplate, "pct", Mathf.RoundToInt(value01 * 100f).ToString());

            int step = StepAt(stepEnds, value01);
            if (step != _step || done)
            {
                _step = step;
                if (stepText)
                    stepText.text = done ? doneWord : step < stepNames.Length ? stepNames[step] : string.Empty;
            }

            OnShown(value01, step, done);
        }

        /// <summary>The look of the steps and the done state: markers, label emphasis, the fill's done colour.</summary>
        protected virtual void OnShown(float value01, int step, bool done) { }

        void Update()
        {
            if (!glint || !fill || _done) return;
            _glintClock = Mathf.Repeat(_glintClock + Time.unscaledDeltaTime / glintSeconds, 1f);
            float half = 0.06f;
            float centre = Mathf.Lerp(-half, 1f + half, _glintClock);
            glint.anchorMin = new Vector2(centre - half, 0f);
            glint.anchorMax = new Vector2(centre + half, 1f);
            glint.offsetMin = Vector2.zero;
            glint.offsetMax = Vector2.zero;
        }
    }
}
