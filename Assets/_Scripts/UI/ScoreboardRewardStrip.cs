using System.Globalization;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace CosmicShore.UI
{
    /// <summary>
    /// The reward strip on the Rev 8 scoreboard (Docs/MENU_REDESIGN_B.md §49): what the local pilot earned this game and
    /// the wallet counting from its old balance to the new one. Hidden when nothing was earned. The words ("You earned",
    /// "Balance") are authored on the prefab; <see cref="amountTemplate"/> shapes the amount.
    /// </summary>
    public class ScoreboardRewardStrip : MonoBehaviour
    {
        [SerializeField] TMP_Text amountText;
        [SerializeField] TMP_Text fromText;
        [SerializeField] TMP_Text toText;
        [Tooltip("The amount line. {amount} is replaced by the crystals earned. Empty: no amount line.")]
        [SerializeField] string amountTemplate = "+{amount}";
        [Tooltip("Seconds the new balance takes to count up from the old one (unscaled).")]
        [SerializeField, Min(0f)] float countSeconds = 0.8f;

        Tween _count;

        public void Show(int amount, int before, int after)
        {
            if (amount <= 0) { Hide(); return; }
            gameObject.SetActive(true);
            if (amountText)
                amountText.text = string.IsNullOrEmpty(amountTemplate) ? string.Empty
                    : amountTemplate.Replace("{amount}", amount.ToString("N0", CultureInfo.InvariantCulture));
            if (fromText) fromText.text = before.ToString("N0", CultureInfo.InvariantCulture);
            _count?.Kill();
            if (!toText) return;
            if (countSeconds <= 0f) { toText.text = after.ToString("N0", CultureInfo.InvariantCulture); return; }
            toText.text = before.ToString("N0", CultureInfo.InvariantCulture);
            _count = DOVirtual.Int(before, after, countSeconds,
                    v => toText.text = v.ToString("N0", CultureInfo.InvariantCulture))
                .SetEase(Ease.OutCubic).SetUpdate(true).SetDelay(0.4f);
        }

        public void Hide()
        {
            _count?.Kill();
            gameObject.SetActive(false);
        }

        void OnDestroy() => _count?.Kill();
    }
}
