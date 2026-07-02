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
    ///    reference the key and this script cross-toggles the groups.
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

        [Tooltip("Optional counter text for counting gates (e.g. skim 3/10).")]
        [SerializeField] TMP_Text progressText;

        [Tooltip("Progress format: {0}=current, {1}=target.")]
        [SerializeField] string progressFormat = "{0} / {1}";

        [Header("Keyed Panels (hand-built instruction sets)")]
        [SerializeField] List<InstructionPanel> panels = new();

        [Header("Timing")]
        [Tooltip("Fade duration (unscaled seconds) for show/hide.")]
        [Min(0f)] [SerializeField] float fadeDuration = 0.25f;

        Coroutine _fade;

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
            SetProgress(0, 0);
        }

        /// <summary>
        /// Show an instruction. When <paramref name="panelKey"/> matches a registered panel,
        /// that panel is enabled (others disabled); otherwise the plain text element is used.
        /// Optionally pulses haptics (respects the player's haptics setting).
        /// </summary>
        public void Show(string text, HapticType haptic = HapticType.None, string panelKey = null)
        {
            bool usedPanel = ActivatePanel(panelKey);

            if (instructionText != null)
            {
                if (!usedPanel && !string.IsNullOrEmpty(text))
                    instructionText.text = text;
                instructionText.gameObject.SetActive(!usedPanel);
            }

            if (haptic != HapticType.None)
                HapticController.PlayHaptic(haptic);

            SetProgress(0, 0);
            FadeTo(1f);
        }

        /// <summary>Update the counter ("3 / 10"). target &lt;= 0 hides it.</summary>
        public void SetProgress(int current, int target)
        {
            if (progressText == null) return;

            bool visible = target > 0;
            progressText.gameObject.SetActive(visible);
            if (visible)
                progressText.text = string.Format(progressFormat, current, target);
        }

        /// <summary>Hide the instruction overlay (and counter).</summary>
        public void Hide()
        {
            SetProgress(0, 0);
            FadeTo(0f);
        }

        bool ActivatePanel(string key)
        {
            bool found = false;
            foreach (var entry in panels)
            {
                if (entry?.group == null) continue;
                bool match = !string.IsNullOrEmpty(key) && entry.key == key;
                entry.group.gameObject.SetActive(match);
                entry.group.alpha = match ? 1f : 0f;
                if (match) found = true;
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
