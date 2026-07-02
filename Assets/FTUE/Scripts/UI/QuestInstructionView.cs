using System.Collections;
using CosmicShore.Gameplay;
using TMPro;
using UnityEngine;

namespace CosmicShore.Core
{
    /// <summary>
    /// Lightweight in-game instruction overlay for control teaching: plain text + an
    /// optional haptic pulse, no captain, no buttons. Driven by ShowInstruction nodes;
    /// the prompt stays on screen while a gate node (e.g. WaitForInput) holds the flow,
    /// and is replaced by the next instruction or hidden explicitly.
    ///
    /// UI-side: drop this on a panel under Menu_Main's Game UI with a CanvasGroup and a
    /// TMP text child — style freely; this script only sets text and fades the group.
    /// </summary>
    public class QuestInstructionView : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Root CanvasGroup faded in/out. Auto-resolved from this GameObject if unset.")]
        [SerializeField] CanvasGroup panel;

        [Tooltip("The instruction text. Auto-resolved from children if unset.")]
        [SerializeField] TMP_Text instructionText;

        [Header("Timing")]
        [Tooltip("Fade duration (unscaled seconds) for show/hide.")]
        [Min(0f)] [SerializeField] float fadeDuration = 0.25f;

        Coroutine _fade;

        void Awake()
        {
            if (panel == null && !TryGetComponent(out panel))
                panel = gameObject.AddComponent<CanvasGroup>();
            if (instructionText == null)
                instructionText = GetComponentInChildren<TMP_Text>(true);

            panel.alpha = 0f;
            panel.blocksRaycasts = false;
            panel.interactable = false;
        }

        /// <summary>Show an instruction and optionally pulse haptics.</summary>
        public void Show(string text, HapticType haptic = HapticType.None)
        {
            if (instructionText != null)
                instructionText.text = text;

            if (haptic != HapticType.None)
                HapticController.PlayHaptic(haptic);

            FadeTo(1f);
        }

        /// <summary>Hide the instruction panel.</summary>
        public void Hide() => FadeTo(0f);

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
