using System;
using System.Collections;
using System.Collections.Generic;
using CosmicShore.Gameplay;
using TMPro;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Lightweight in-game instruction overlay for control teaching: plain text + an
    /// optional haptic pulse, no captain, no buttons. Driven by ShowInstruction nodes;
    /// the prompt stays on screen while a gate node (e.g. WaitForInput) holds the flow.
    ///
    /// Two authoring modes, mixable:
    ///  • Text mode — this script sets <see cref="instructionText"/> directly.
    ///  • Panel mode — you build one CanvasGroup per instruction (text + thumbstick/trigger
    ///    icons, animatable) and register it in <see cref="panels"/> with a key; nodes
    ///    reference the key and this script cross-toggles the groups. The node's authored
    ///    text OVERWRITES the panel's TMP text — the scene text is only a design-time
    ///    placeholder, the graph is the source of truth.
    ///
    /// Also hosts the optional progress counter ("3/10") used by counting gates
    /// (e.g. WaitForSkim).
    /// </summary>
    public class QuestInstructionView : MonoBehaviour
    {
        [Serializable]
        public class InstructionPanel
        {
            [Tooltip("Key referenced by ShowInstruction nodes (e.g. 'speed_up').")]
            public string key;
            [Tooltip("The hand-built panel for this instruction (icons + text).")]
            public CanvasGroup group;
        }

        [Header("References")]
        [Tooltip("Root CanvasGroup faded in/out. Auto-resolved from this GameObject if unset.")]
        [SerializeField] CanvasGroup panel;

        [Tooltip("Fallback/simple text element. Optional when using keyed panels.")]
        [SerializeField] TMP_Text instructionText;

        [Tooltip("Counting gates (e.g. skim) append this to the ACTIVE instruction's own text: {0}=current, {1}=target.")]
        [SerializeField] string progressFormat = "  {0} / {1}";

        [Header("Keyed Panels (hand-built instruction sets)")]
        [SerializeField] List<InstructionPanel> panels = new();

        [Header("Timing")]
        [Tooltip("Fade duration (unscaled seconds) for show/hide.")]
        [Min(0f)] [SerializeField] float fadeDuration = 0.25f;

        Coroutine _fade;
        TMP_Text _activeText;
        string _activeBaseText;

        void Awake()
        {
            if (panel == null && !TryGetComponent(out panel))
                panel = gameObject.AddComponent<CanvasGroup>();
            // Only auto-grab a text element in plain-text mode — with keyed panels, the first
            // TMP found would be inside a panel and Show() would wrongly toggle it.
            if (instructionText == null && panels.Count == 0)
                instructionText = GetComponentInChildren<TMP_Text>(true);

            panel.alpha = 0f;
            panel.blocksRaycasts = false;
            panel.interactable = false;
        }

        /// <summary>
        /// Show an instruction. When <paramref name="panelKey"/> matches a registered panel,
        /// that panel is enabled (others disabled) and the node's authored text OVERWRITES the
        /// panel's own TMP text (the scene text is just a placeholder — the graph is the source
        /// of truth); otherwise the plain text element is used.
        /// Optionally pulses haptics (respects the player's haptics setting).
        /// </summary>
        public void Show(string text, HapticType haptic = HapticType.None, string panelKey = null)
        {
            bool usedPanel = ActivatePanel(panelKey, text);

            if (instructionText != null)
            {
                if (!usedPanel && !string.IsNullOrEmpty(text))
                    instructionText.text = text;
                instructionText.gameObject.SetActive(!usedPanel);
            }

            if (!usedPanel)
            {
                _activeText = instructionText;
                _activeBaseText = _activeText != null ? _activeText.text : null;
            }

            if (haptic != HapticType.None)
                HapticController.PlayHaptic(haptic);

            FadeTo(1f);
        }

        /// <summary>
        /// Update a counting gate's progress ("3 / 10") — appended to the ACTIVE
        /// instruction's own text (base text restored when target &lt;= 0).
        /// </summary>
        public void SetProgress(int current, int target)
        {
            if (_activeText == null || _activeBaseText == null) return;

            _activeText.text = target > 0
                ? _activeBaseText + string.Format(progressFormat, current, target)
                : _activeBaseText;
        }

        /// <summary>Hide the instruction overlay (restoring any counter-modified text).</summary>
        public void Hide()
        {
            SetProgress(0, 0);
            FadeTo(0f);
        }

        bool ActivatePanel(string key, string text)
        {
            bool found = false;
            foreach (var entry in panels)
            {
                if (entry?.group == null) continue;
                bool match = !string.IsNullOrEmpty(key) && entry.key == key;
                entry.group.gameObject.SetActive(match);
                entry.group.alpha = match ? 1f : 0f;
                if (match)
                {
                    found = true;
                    // The graph node's text is authoritative: overwrite whatever placeholder
                    // the hand-built panel carries in the scene. Counting gates then append
                    // to this base. Empty node text keeps the scene text.
                    _activeText = entry.group.GetComponentInChildren<TMP_Text>(true);
                    if (_activeText != null && !string.IsNullOrEmpty(text))
                        _activeText.text = text;
                    _activeBaseText = _activeText != null ? _activeText.text : null;
                }
            }

            if (!string.IsNullOrEmpty(key) && !found)
                Debug.LogWarning($"[Quest] QuestInstructionView: no panel registered for key '{key}' — falling back to text.");

            return found;
        }

        void FadeTo(float target)
        {
            if (!isActiveAndEnabled)
            {
                panel.alpha = target;
                return;
            }

            if (_fade != null) StopCoroutine(_fade);
            _fade = StartCoroutine(FadeRoutine(target));
        }

        IEnumerator FadeRoutine(float target)
        {
            float start = panel.alpha;
            if (fadeDuration <= 0f)
            {
                panel.alpha = target;
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                panel.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / fadeDuration));
                yield return null;
            }
            panel.alpha = target;
        }
    }
}
