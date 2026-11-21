using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using DG.Tweening.Core.Easing;
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
    /// <see cref="ModalWindowManager.OnModalOpened"/>.</para>
    ///
    /// <para><b>It cannot leave a card invisible.</b> The cascade is ONE coroutine on the grid's
    /// own host, stepped on unscaled time (the menu sits at timeScale 0 on every non-HOME
    /// screen), and its last act - reached on completion, and by <see cref="Snap"/> on any
    /// interruption (a re-open, the host disabling, a second Play) - writes every card back to
    /// alpha 1 / scale 1. A per-card tween can be killed, paused or never ticked by something
    /// the grid cannot see, and a card that stays at alpha 0 reads as a mode that has vanished
    /// from the arcade; a single routine whose exit IS the rest state has no such failure. The
    /// card's own CanvasGroup is used (added when missing) - never the modal's, which its
    /// animator writes every frame.</para>
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
        const float BackOvershoot = 1.70158f;

        /// <summary>
        /// Start the cascade over <paramref name="cards"/> in list order on <paramref name="host"/>.
        /// Inactive cards are skipped. Any run already held in <paramref name="running"/> is
        /// stopped and snapped to rest first. Timing comes from <paramref name="settings"/>'s
        /// card-entrance block when one is wired, else the fleet defaults above. Returns the
        /// routine to keep in <paramref name="running"/>; null when nothing had to play.
        /// </summary>
        public static Coroutine Play(MonoBehaviour host, IReadOnlyList<GameObject> cards,
                                     HUDAnimationSettingsSO settings, Coroutine running,
                                     float delayAfterOpen = DefaultDelayAfterOpen)
        {
            Snap(host, cards, running);
            if (!host || !host.isActiveAndEnabled || cards == null) return null;

            var live = new List<GameObject>(cards.Count);
            for (int i = 0; i < cards.Count; i++)
                if (cards[i] && cards[i].activeInHierarchy) live.Add(cards[i]);
            if (live.Count == 0) return null;

            return host.StartCoroutine(Cascade(live, settings, delayAfterOpen));
        }

        /// <summary>
        /// Stop a running reveal and leave every card at rest (alpha 1, scale 1). Safe with a
        /// null routine, a null host and cards that were destroyed since.
        /// </summary>
        public static void Snap(MonoBehaviour host, IReadOnlyList<GameObject> cards, Coroutine running)
        {
            if (running != null && host) host.StopCoroutine(running);
            if (cards == null) return;
            for (int i = 0; i < cards.Count; i++)
                if (cards[i]) Rest(cards[i]);
        }

        static IEnumerator Cascade(List<GameObject> cards, HUDAnimationSettingsSO settings, float delayAfterOpen)
        {
            float duration   = settings ? Mathf.Max(0.01f, settings.cardEntranceDuration)   : DefaultDuration;
            float startScale = settings ? settings.cardEntranceStartScale : DefaultStartScale;
            float stagger    = settings ? settings.cardEntranceStagger    : DefaultStagger;
            var   ease       = settings ? settings.cardEntranceEase       : Ease.OutBack;

            // Divide the stagger down rather than truncating the tail: every card still gets
            // its own beat, the last one just arrives sooner on a big grid.
            if (cards.Count > 1) stagger = Mathf.Min(stagger, MaxTotalStagger / (cards.Count - 1));

            var groups = new CanvasGroup[cards.Count];
            for (int i = 0; i < cards.Count; i++)
            {
                groups[i] = EnsureGroup(cards[i]);
                groups[i].alpha = 0f;
                cards[i].transform.localScale = Vector3.one * startScale;
            }

            float total = delayAfterOpen + stagger * (cards.Count - 1) + duration;
            float elapsed = 0f;
            while (elapsed < total)
            {
                elapsed += Time.unscaledDeltaTime;
                for (int i = 0; i < cards.Count; i++)
                {
                    var card = cards[i];
                    if (!card) continue;
                    float t = Mathf.Clamp01((elapsed - delayAfterOpen - stagger * i) / duration);
                    float eased = EaseManager.Evaluate(ease, null, t, 1f, BackOvershoot, 0f);
                    card.transform.localScale = Vector3.one * Mathf.LerpUnclamped(startScale, 1f, eased);
                    if (groups[i]) groups[i].alpha = Mathf.Clamp01(EaseManager.Evaluate(Ease.OutQuad, null, t, 1f, 0f, 0f));
                }
                yield return null;
            }

            // The exit IS the rest state, whatever the numbers above did.
            for (int i = 0; i < cards.Count; i++)
                if (cards[i]) Rest(cards[i]);
        }

        static CanvasGroup EnsureGroup(GameObject card)
        {
            if (!card.TryGetComponent<CanvasGroup>(out var group))
                group = card.AddComponent<CanvasGroup>();
            return group;
        }

        static void Rest(GameObject card)
        {
            card.transform.localScale = Vector3.one;
            if (card.TryGetComponent<CanvasGroup>(out var group)) group.alpha = 1f;
        }
    }
}
