using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Gameplay
{
    /// <summary>
    /// The Maelstrom hub's motion vocabulary, in one place so the three screens it drives (hub,
    /// stats, summary) move in the same language rather than each inventing a flourish.
    ///
    /// <para><b>Everything here is unscaled.</b> The hub runs across scene loads and countdowns
    /// where <c>Time.timeScale</c> is not something this screen controls, and an animation that
    /// stops because something else paused the game reads as a hang.</para>
    ///
    /// <para><b>Every tween kills its own target first.</b> A round card cascade, a countdown tick
    /// and a panel entrance can all land on the same transform within a frame of each other (the
    /// countdown ticks while the cards are still arriving), and two live tweens on one transform
    /// fight over <c>localScale</c> and leave it wherever the loser stopped.</para>
    /// </summary>
    public static class MaelstromTransitions
    {
        /// <summary>A panel arriving: falls in from slightly above, overshoots, settles.</summary>
        public static void PanelIn(CanvasGroup group, RectTransform rect, float duration = 0.45f)
        {
            if (group)
            {
                group.DOKill();
                group.alpha = 0f;
                group.DOFade(1f, duration * 0.8f).SetEase(Ease.OutQuad).SetUpdate(true);
            }

            if (!rect) return;
            rect.DOKill();
            var home = rect.anchoredPosition;
            rect.anchoredPosition = home + new Vector2(0f, 42f);
            rect.localScale = Vector3.one * 0.94f;
            rect.DOAnchorPos(home, duration).SetEase(Ease.OutBack).SetUpdate(true);
            rect.DOScale(1f, duration).SetEase(Ease.OutBack).SetUpdate(true);
        }

        /// <summary>
        /// A panel leaving, on the way to something else. Deliberately quicker than the entrance:
        /// an exit the player has already committed to should not make them wait for it.
        /// </summary>
        public static void PanelOut(CanvasGroup group, RectTransform rect, float duration = 0.22f)
        {
            if (group)
            {
                group.DOKill();
                group.DOFade(0f, duration).SetEase(Ease.InQuad).SetUpdate(true);
            }

            if (!rect) return;
            rect.DOKill();
            rect.DOScale(0.96f, duration).SetEase(Ease.InQuad).SetUpdate(true);
        }

        /// <summary>
        /// One round card arriving, <paramref name="index"/> places down the stack. The stagger is
        /// what makes a scroll of results read as a tally being counted out rather than a list
        /// appearing.
        /// </summary>
        public static void CardIn(Transform card, int index, float step = 0.07f)
        {
            if (!card) return;
            card.DOKill();

            var group = card.GetComponent<CanvasGroup>();
            if (!group) group = card.gameObject.AddComponent<CanvasGroup>();
            group.DOKill();
            group.alpha = 0f;

            card.localScale = Vector3.one * 0.88f;

            float delay = index * step;
            group.DOFade(1f, 0.28f).SetDelay(delay).SetEase(Ease.OutQuad).SetUpdate(true);
            card.DOScale(1f, 0.38f).SetDelay(delay).SetEase(Ease.OutBack).SetUpdate(true);
        }

        /// <summary>
        /// One tick of the 3-2-1. Punchy and colour-flashed, and it grows as the number falls -
        /// the last second is the loudest thing on the screen because it is the last chance to
        /// press READY.
        /// </summary>
        public static void CountdownTick(TMP_Text text, int secondsRemaining, Color flashColor, Color restColor)
        {
            if (!text) return;

            var t = text.transform;
            t.DOKill(true);
            text.DOKill(true);

            // 3 → 1 maps to a rising punch, so urgency is legible without reading the digit.
            float weight = Mathf.InverseLerp(3f, 1f, Mathf.Clamp(secondsRemaining, 1, 3));
            float punch = Mathf.Lerp(0.22f, 0.5f, weight);

            t.localScale = Vector3.one;
            t.DOPunchScale(Vector3.one * punch, 0.36f, 8, 0.7f).SetUpdate(true);

            // Driven through DOTween.To rather than text.DOColor: the TMP shortcut lives in
            // DOTween's TextMeshPro MODULE, and this project does not have it - the Modules folder
            // carries Audio / EPOOutline / Physics / Physics2D / Sprite / UI / UnityVersion / Utils
            // and nothing else, which is why no other file in the codebase tweens a TMP colour.
            // DOTween.To's Color plugin is core, so this needs no module and no setup pass.
            text.color = flashColor;
            DOTween.To(() => text.color, c => text.color = c, restColor, 0.5f)
                .SetEase(Ease.OutQuad).SetUpdate(true);
        }

        /// <summary>A soft pulse for anything that merely changed - a tally, a leading domain.</summary>
        public static void PulseScale(Transform t, float strength = 0.12f)
        {
            if (!t) return;
            t.DOKill(true);
            t.localScale = Vector3.one;
            t.DOPunchScale(Vector3.one * strength, 0.25f, 6, 0.6f).SetUpdate(true);
        }

        /// <summary>
        /// The mode reveal: the drawn round's name lands. Used once per hub visit, when the draw
        /// resolves - the one moment in the hub where something genuinely new appears.
        /// </summary>
        public static void Reveal(TMP_Text text)
        {
            if (!text) return;

            text.DOKill();
            text.transform.DOKill();
            text.ForceMeshUpdate();
            int total = Mathf.Max(1, text.textInfo.characterCount);

            text.maxVisibleCharacters = 0;
            text.transform.localScale = Vector3.one * 1.18f;
            text.transform.DOScale(1f, 0.45f).SetEase(Ease.OutBack).SetUpdate(true);
            DOTween.To(() => text.maxVisibleCharacters, x => text.maxVisibleCharacters = x, total, 0.4f)
                .SetEase(Ease.Linear).SetUpdate(true);
        }

        /// <summary>
        /// The launch: everything on screen leans in and a white sheet takes over, so the round
        /// begins on a cut rather than on a panel that merely disappeared. The sheet is left OPAQUE
        /// - the scene load is what clears it - which is exactly why it must never be started for
        /// anything that is not actually loading.
        /// </summary>
        public static void LaunchFlourish(CanvasGroup content, Image flashSheet)
        {
            if (content)
            {
                content.DOKill();
                var rect = content.transform as RectTransform;
                if (rect)
                {
                    rect.DOKill();
                    rect.DOScale(1.08f, 0.55f).SetEase(Ease.InBack).SetUpdate(true);
                }
                content.DOFade(0f, 0.5f).SetEase(Ease.InQuad).SetUpdate(true);
            }

            if (!flashSheet) return;
            flashSheet.gameObject.SetActive(true);
            flashSheet.DOKill();
            var c = flashSheet.color;
            c.a = 0f;
            flashSheet.color = c;
            flashSheet.DOFade(1f, 0.45f).SetEase(Ease.InQuad).SetUpdate(true);
        }
    }
}
