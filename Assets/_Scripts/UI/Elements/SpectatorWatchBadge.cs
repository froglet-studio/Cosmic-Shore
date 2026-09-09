using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The small EYE that appears under the goal stack while somebody is spectating this pilot -
    /// the one thing a player has no other way to learn, since a viewer has no vessel, no name
    /// plate and no presence in the arena at all.
    ///
    /// <para><b>Built at runtime, never authored.</b> The goal stack lives in TWO forked
    /// GameCanvas prefabs across a dozen scenes (Docs/GAME_MODE_TOPBAR.md), so an authored badge
    /// would be a hand-edit in both and absent from whichever one a future scene copies. It is
    /// ensured by <see cref="MiniGameHUD"/> the way the connecting panel's roster is, and draws
    /// the same eye sprite the friends row's Spectate button uses - one asset, so the button a
    /// viewer pressed and the mark their target sees are visibly the same act.</para>
    ///
    /// <para>The count comes from <c>Player.NetSpectatorCount</c>, which the server derives from
    /// <see cref="SpectatorSession"/>'s watch book. It is polled rather than evented because the
    /// LOCAL player arrives late (the pair resolves after this HUD exists) and a viewer can join,
    /// switch pilots or drop at any moment; one small read a few times a second is cheaper than
    /// the bookkeeping that would keep a subscription pointed at the right Player.</para>
    /// </summary>
    public class SpectatorWatchBadge : MonoBehaviour
    {
        /// <summary>Sprite path under Resources - the same asset the Spectate button draws.</summary>
        public const string EyeSpritePath = "UI/icon_Spectate";

        const float PollSeconds = 0.25f;
        const float IconSize = 26f;

        [SerializeField] GameDataSO gameData;

        Image _eye;
        TMP_Text _count;
        CanvasGroup _group;
        float _nextPoll;
        int _shown = -1;

        /// <summary>
        /// Create (or find) the badge under <paramref name="parent"/>. Idempotent: a second call
        /// returns the existing one, so a HUD that re-runs its ensure pass cannot stack badges.
        /// </summary>
        public static SpectatorWatchBadge Ensure(Transform parent, GameDataSO data)
        {
            if (!parent) return null;

            var existing = parent.GetComponentInChildren<SpectatorWatchBadge>(true);
            if (existing)
            {
                if (!existing.gameData) existing.gameData = data;
                return existing;
            }

            var go = new GameObject("SpectatorWatchBadge", typeof(RectTransform), typeof(CanvasGroup));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            // Under the stack, hugging its left edge - the stack itself is top-left anchored.
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(0f, -6f);
            rect.sizeDelta = new Vector2(96f, IconSize);

            var badge = go.AddComponent<SpectatorWatchBadge>();
            badge.gameData = data;
            badge.Build(rect);
            return badge;
        }

        void Build(RectTransform rect)
        {
            _group = GetComponent<CanvasGroup>();
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;

            var iconGo = new GameObject("Eye", typeof(RectTransform), typeof(Image));
            var iconRect = (RectTransform)iconGo.transform;
            iconRect.SetParent(rect, false);
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.sizeDelta = new Vector2(IconSize, IconSize);

            _eye = iconGo.GetComponent<Image>();
            _eye.raycastTarget = false;
            _eye.preserveAspect = true;
            _eye.sprite = Resources.Load<Sprite>(EyeSpritePath);
            // A missing sprite would draw a SOLID QUAD in the tint rather than nothing, which is
            // how a retired texture once painted a navy panel over half the screen. Draw nothing.
            if (!_eye.sprite) _eye.enabled = false;
            _eye.color = new Color(1f, 0.85f, 0.25f, 0.95f);

            var countGo = new GameObject("Count", typeof(RectTransform));
            var countRect = (RectTransform)countGo.transform;
            countRect.SetParent(rect, false);
            countRect.anchorMin = countRect.anchorMax = new Vector2(0f, 0.5f);
            countRect.pivot = new Vector2(0f, 0.5f);
            countRect.anchoredPosition = new Vector2(IconSize + 4f, 0f);
            countRect.sizeDelta = new Vector2(60f, IconSize);

            _count = countGo.AddComponent<TextMeshProUGUI>();
            _count.fontSize = 16f;
            _count.alignment = TextAlignmentOptions.MidlineLeft;
            _count.raycastTarget = false;
            _count.color = new Color(1f, 0.85f, 0.25f, 0.95f);
            _count.text = string.Empty;
        }

        void Update()
        {
            if (Time.unscaledTime < _nextPoll) return;
            _nextPoll = Time.unscaledTime + PollSeconds;

            int watchers = ResolveWatchers();
            if (watchers == _shown) return;
            _shown = watchers;

            if (_group) _group.alpha = watchers > 0 ? 1f : 0f;
            // A count is only worth drawing when it is ambiguous: one viewer is what the eye
            // already says.
            if (_count) _count.text = watchers > 1 ? watchers.ToString() : string.Empty;
        }

        int ResolveWatchers()
        {
            var local = gameData ? gameData.LocalPlayer : null;
            return local is Player p ? p.SpectatorCount : 0;
        }
    }
}
