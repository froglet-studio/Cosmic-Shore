using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The whole interface of a Test Flight: what you are doing, how far in you are, how long is
    /// left, and the way out.
    ///
    /// <para>Deliberately not the mode's real HUD. A preview has no rounds, no turns, no
    /// countdown, no ready button and no scoreboard, and standing those up would mean
    /// instantiating the mode's controller — the exact coupling the preview exists to avoid. The
    /// VESSEL's own HUD is still there underneath (it comes with freestyle), so the pilot still
    /// reads their own ship; this only adds the objective.</para>
    ///
    /// <para>Event-driven: it repaints on <see cref="ModePreviewRunner.OnProgressChanged"/> and
    /// ticks only the clock, so it costs nothing while a player is just flying.</para>
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class ModePreviewHUD : MonoBehaviour
    {
        [Header("Readouts")]
        [SerializeField, Tooltip("The mode's name, so the player always knows what they are trying.")]
        TMP_Text modeLabel;

        [SerializeField, Tooltip("One line of instruction, shown for the whole flight.")]
        TMP_Text objectiveLabel;

        [SerializeField, Tooltip("Progress toward the objective, e.g. \"2 / 3\". Hidden when the " +
                                 "preview has no target.")]
        TMP_Text progressLabel;

        [SerializeField, Tooltip("Seconds left. Hidden when the preview has no time limit.")]
        TMP_Text timerLabel;

        [SerializeField, Tooltip("Optional fill image driven 0..1 by objective progress.")]
        Image progressFill;

        [Header("Feel")]
        [SerializeField, Tooltip("Seconds for the HUD to fade in and out, so it never pops.")]
        float fadeSeconds = 0.35f;

        CanvasGroup _canvasGroup;
        ModePreviewRunner _runner;
        bool _hasTimer;
        float _fadeTarget;

        void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            ApplyAlpha(0f);
        }

        void OnDestroy()
        {
            Unbind();
        }

        /// <summary>Bind to a running flight and fade in.</summary>
        public void Show(ModePreviewRunner runner, ModePreviewDefinitionSO definition)
        {
            Unbind();

            _runner = runner;
            _hasTimer = definition && definition.DurationSeconds > 0f;

            if (modeLabel) modeLabel.text = definition ? definition.Mode.ToString() : string.Empty;
            if (objectiveLabel) objectiveLabel.text = definition ? definition.ObjectiveText : string.Empty;

            bool hasTarget = runner != null && runner.HasTarget;
            if (progressLabel) progressLabel.gameObject.SetActive(hasTarget);
            if (progressFill) progressFill.gameObject.SetActive(hasTarget);
            if (timerLabel) timerLabel.gameObject.SetActive(_hasTimer);

            if (_runner != null)
            {
                _runner.OnProgressChanged += Repaint;
                Repaint();
            }

            _fadeTarget = 1f;
        }

        /// <summary>Unbind and fade out. Safe to call when nothing is bound.</summary>
        public void Hide()
        {
            Unbind();
            _fadeTarget = 0f;
        }

        void Unbind()
        {
            if (_runner != null) _runner.OnProgressChanged -= Repaint;
            _runner = null;
        }

        void Update()
        {
            // Fade toward the target. Unscaled, because the menu is free to touch timeScale.
            if (!Mathf.Approximately(_canvasGroup.alpha, _fadeTarget))
            {
                float step = fadeSeconds > 0.001f
                    ? Time.unscaledDeltaTime / fadeSeconds
                    : 1f;
                ApplyAlpha(Mathf.MoveTowards(_canvasGroup.alpha, _fadeTarget, step));
            }

            if (_runner == null || !_hasTimer || !timerLabel) return;

            float remaining = _runner.SecondsRemaining;
            if (remaining < 0f) return;
            timerLabel.text = Mathf.CeilToInt(remaining).ToString();
        }

        void Repaint()
        {
            if (_runner == null) return;

            if (progressLabel && _runner.HasTarget)
                progressLabel.text = $"{_runner.Progress} / {_runner.Target}";

            if (progressFill && _runner.HasTarget)
                progressFill.fillAmount = _runner.Target > 0
                    ? Mathf.Clamp01((float)_runner.Progress / _runner.Target)
                    : 0f;
        }

        void ApplyAlpha(float alpha)
        {
            _canvasGroup.alpha = alpha;
            // Only take raycasts while actually visible, so a faded-out HUD can never eat a tap
            // meant for the vessel or the menu underneath it.
            bool visible = alpha > 0.01f;
            _canvasGroup.blocksRaycasts = visible;
            _canvasGroup.interactable = visible;
        }
    }
}
