using System;
using System.Collections;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// Navigate's dive into the live preview (Rev 142 A "Portal", kept through Rev 145 H,
    /// Docs/MENU_REDESIGN_B.md section 235): the preview panel grows to fill its window while
    /// everything else in the window fades, and only then does the caller hand the player to the
    /// world. The player reads the press as going INTO the picture they were looking at.
    ///
    /// <para>Runs on unscaled time (<see cref="KitMotion"/>), blocks the window's input while it
    /// plays, and puts every rect and alpha back the next time the window is enabled, so a modal
    /// that hides by deactivating opens as it was authored.</para>
    /// </summary>
    [AddComponentMenu("Cosmic Shore/UI Kit/Kit Dive")]
    public class KitDive : MonoBehaviour
    {
        [Tooltip("The panel that grows to fill the window (the live preview).")]
        [SerializeField] RectTransform target;

        [Tooltip("What fades out while the target grows (the other panels and the header).")]
        [SerializeField] CanvasGroup[] fadeOut = Array.Empty<CanvasGroup>();

        [Tooltip("The whole window's group: input is blocked on it while the dive plays.")]
        [SerializeField] CanvasGroup window;

        [Tooltip("How long the dive takes, in seconds (the ceremony token is 0.5).")]
        [SerializeField] float duration = KitMotion.Panel + 0.08f;

        [Tooltip("The easing of the dive.")]
        [SerializeField] KitEase ease = KitEase.InOutSine;

        Vector2 _min, _max, _offMin, _offMax;
        float[] _alphas = Array.Empty<float>();
        bool _captured;
        Coroutine _running;

        /// <summary>True while a dive is playing.</summary>
        public bool IsDiving => _running != null;

        void OnEnable() => Restore();

        void OnDisable() => Restore();

        /// <summary>
        /// Play the dive and call <paramref name="done"/> when it lands. With no target, or while the
        /// object is inactive, <paramref name="done"/> runs at once, so a missing wire never eats a
        /// press.
        /// </summary>
        public void Play(Action done)
        {
            if (!target || !isActiveAndEnabled || _running != null)
            {
                done?.Invoke();
                return;
            }
            Capture();
            _running = StartCoroutine(Run(done));
        }

        void Capture()
        {
            _min = target.anchorMin;
            _max = target.anchorMax;
            _offMin = target.offsetMin;
            _offMax = target.offsetMax;
            _alphas = new float[fadeOut.Length];
            for (int i = 0; i < fadeOut.Length; i++)
                _alphas[i] = fadeOut[i] ? fadeOut[i].alpha : 1f;
            _captured = true;
        }

        /// <summary>
        /// Put the target and the faded groups back as authored. A window that hides by alpha never
        /// re-enables, so its owner calls this when it opens again.
        /// </summary>
        public void Restore()
        {
            if (_running != null)
            {
                StopCoroutine(_running);
                _running = null;
            }
            if (!_captured) return;
            _captured = false;
            if (target)
            {
                target.anchorMin = _min;
                target.anchorMax = _max;
                target.offsetMin = _offMin;
                target.offsetMax = _offMax;
            }
            for (int i = 0; i < fadeOut.Length && i < _alphas.Length; i++)
                if (fadeOut[i]) fadeOut[i].alpha = _alphas[i];
            if (window)
            {
                window.interactable = true;
                window.blocksRaycasts = true;
            }
        }

        IEnumerator Run(Action done)
        {
            if (window)
            {
                window.interactable = false;
                window.blocksRaycasts = false;
            }

            // Work in the parent's space: where the target's corners sit now, eased to the parent's
            // full rect, written back as stretch anchors with offsets.
            var parent = target.parent as RectTransform;
            Rect pr = parent ? parent.rect : target.rect;
            Vector3[] corners = new Vector3[4];
            target.GetLocalCorners(corners);
            Vector2 from0 = (Vector2)(target.localPosition + corners[0]) - pr.min;
            Vector2 from1 = (Vector2)(target.localPosition + corners[2]) - pr.max;
            target.anchorMin = Vector2.zero;
            target.anchorMax = Vector2.one;

            float t = 0f;
            float length = Mathf.Max(0.01f, duration);
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / length;
                float e = KitMotion.Ease(ease, t);
                target.offsetMin = Vector2.Lerp(from0, Vector2.zero, e);
                target.offsetMax = Vector2.Lerp(from1, Vector2.zero, e);
                for (int i = 0; i < fadeOut.Length; i++)
                    if (fadeOut[i]) fadeOut[i].alpha = Mathf.Lerp(_alphas[i], 0f, e);
                yield return null;
            }

            _running = null;
            done?.Invoke();
        }
    }
}
