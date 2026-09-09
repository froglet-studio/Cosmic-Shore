using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// The staggered reveal every card grid on the home hub plays when its window opens -
    /// Arcade, Arena and Mission (one <c>ArcadeExploreView</c> each) and the Toy Box. Cards pop
    /// in one after another: scale from <c>startScale</c> and fade from 0, each a beat later
    /// than the last, the whole cascade held under <see cref="MaxTotalStagger"/> so a
    /// thirteen-card grid finishes about when a six-card one does.
    ///
    /// <para><b>Scale and alpha only - never a rect.</b> Both grids are laid out by layout
    /// groups that own every card's anchored position, so a positional slide would be undone
    /// on the next layout pass and read as a jitter (Docs/ArcadeLaunch/ARCHITECTURE.md §4.2).
    /// The pop's <c>OutBack</c> overshoot is what makes a scale-in read as "sliding out" of the
    /// plate.</para>
    ///
    /// <para><b>Played on OPEN, never on a redraw.</b> The grids repopulate while a window is
    /// up (a favourite toggled, progression changed, a toy registered) and a settled card must
    /// not flicker (Docs/HomeHub/ARCHITECTURE.md §4.1). So the trigger is
    /// <see cref="ModalWindowManager.OnModalOpened"/>, and a second <see cref="Play"/> on the
    /// same cards kills and snaps the previous run first. Unscaled time: the menu sits at
    /// timeScale 0 on every non-HOME screen. The card's own CanvasGroup is used (added when
    /// missing) - never the modal's, which its animator writes every frame.</para>
    /// </summary>
    public static class CardGridReveal
    {
        /// <summary>Beat after the window starts opening before the first card pops (its plate is up by then).</summary>
        public const float DefaultDelayAfterOpen = 0.15f;
        /// <summary>The whole cascade, first card to last, is compressed under this many seconds.</summary>
        public const float MaxTotalStagger = 0.55f;

        const float DefaultDuration = 0.3f;
        const float DefaultStartScale = 0.6f;
        const float DefaultStagger = 0.08f;

        /// <summary>
        /// Play the cascade over <paramref name="cards"/> in list order. Inactive cards are
        /// skipped. Timing comes from <paramref name="settings"/>'s card-entrance block when one
        /// is wired, else the fleet defaults above.
        /// </summary>
        public static void Play(IReadOnlyList<GameObject> cards, HUDAnimationSettingsSO settings,
                                float delayAfterOpen = DefaultDelayAfterOpen)
        {
            if (cards == null) return;

            float duration   = settings ? settings.cardEntranceDuration   : DefaultDuration;
            float startScale = settings ? settings.cardEntranceStartScale : DefaultStartScale;
            float stagger    = settings ? settings.cardEntranceStagger    : DefaultStagger;
            var   ease       = settings ? settings.cardEntranceEase       : Ease.OutBack;

            int live = 0;
            for (int i = 0; i < cards.Count; i++)
                if (cards[i] && cards[i].activeInHierarchy) live++;
            if (live == 0) return;

            // Divide the stagger down rather than truncating the tail: every card still gets
            // its own beat, the last one just arrives sooner on a big grid.
            if (live > 1) stagger = Mathf.Min(stagger, MaxTotalStagger / (live - 1));

            int index = 0;
            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                if (!card || !card.activeInHierarchy) continue;

                var group = card.GetComponent<CanvasGroup>();
                if (!group) group = card.AddComponent<CanvasGroup>();

                // Any earlier run on this card ends here, at rest, before the new one starts.
                Kill(card.transform, group);

                group.alpha = 0f;
                card.transform.localScale = Vector3.one * startScale;

                float delay = delayAfterOpen + stagger * index++;
                card.transform.DOScale(1f, duration)
                    .SetDelay(delay).SetEase(ease).SetUpdate(true).SetLink(card);
                group.DOFade(1f, duration)
                    .SetDelay(delay).SetEase(Ease.OutQuad).SetUpdate(true).SetLink(card);
            }
        }

        /// <summary>Stop any running reveal on <paramref name="cards"/> and leave them at rest.</summary>
        public static void Snap(IReadOnlyList<GameObject> cards)
        {
            if (cards == null) return;
            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                if (!card) continue;
                Kill(card.transform, card.GetComponent<CanvasGroup>());
            }
        }

        static void Kill(Transform t, CanvasGroup group)
        {
            t.DOKill();
            t.localScale = Vector3.one;
            if (!group) return;
            group.DOKill();
            group.alpha = 1f;
        }
    }
}
