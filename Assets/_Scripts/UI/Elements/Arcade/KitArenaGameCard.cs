using CosmicShore.ScriptableObjects;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// The style-B ARENA card (Docs/MENU_REDESIGN_B.md section 17): the arcade card's mode, favourite,
    /// party-pick and lock behaviour (<see cref="KitGameCard"/>), on a larger card that also says who
    /// plays it - "Arena - 2 - 6 pilots", the mode's tagline, "2 - 3 domains" and one chip per hull the
    /// card seats (an arena card seats several vessel classes).
    ///
    /// <para>All of it is the card's own data (<see cref="SO_ArcadeGame"/>): Min/MaxPlayersAllowed,
    /// Description, Min/MaxDomainsAllowed and Vessels. The hull chips are a pool cloned from the first
    /// child of <see cref="hullRow"/> and shown / hidden through their CanvasGroups.</para>
    /// </summary>
    public class KitArenaGameCard : KitGameCard
    {
        [Header("Arena card")]
        [Tooltip("\"Arena - 2 - 6 pilots\" (Label role).")]
        [SerializeField] private KitText eyebrowText;
        [Tooltip("The mode's one-line description.")]
        [SerializeField] private TMP_Text taglineText;
        [Tooltip("\"2 domains\" / \"2 - 3 domains\" (Label role).")]
        [SerializeField] private KitText domainsText;
        [Tooltip("The hull chips' parent; its first child is the chip template (an Image with a CanvasGroup and a LayoutElement, the icon a child Image).")]
        [SerializeField] private Transform hullRow;

        private readonly List<GameObject> _chips = new();

        protected override void DrawDetails(SO_ArcadeGame game)
        {
            if (eyebrowText) eyebrowText.SetText($"Arena - {Range(game.MinPlayersAllowed, game.MaxPlayersAllowed)} pilots");
            if (taglineText) taglineText.text = game.Description;
            if (domainsText)
            {
                string noun = game.MaxDomainsAllowed == 1 ? "domain" : "domains";
                domainsText.SetText($"{Range(game.MinDomainsAllowed, game.MaxDomainsAllowed)} {noun}");
            }
            DrawHulls(game);
        }

        /// <summary>"2" when the bounds agree, "2 - 6" otherwise.</summary>
        public static string Range(int min, int max) => min == max ? min.ToString() : $"{min} - {max}";

        private void DrawHulls(SO_ArcadeGame game)
        {
            if (!hullRow || hullRow.childCount == 0) return;
            if (_chips.Count == 0) _chips.Add(hullRow.GetChild(0).gameObject);

            int shown = 0;
            if (game.Vessels != null)
            {
                foreach (var vessel in game.Vessels)
                {
                    if (!vessel) continue;
                    var sprite = vessel.IconActive ? vessel.IconActive : vessel.IconInactive;
                    if (!sprite) continue;
                    if (shown == _chips.Count)
                        _chips.Add(Instantiate(_chips[0], hullRow));
                    var chip = _chips[shown++];
                    var icon = chip.transform.childCount > 0 ? chip.transform.GetChild(0).GetComponent<Image>() : null;
                    if (icon) icon.sprite = sprite;
                    ShowHull(chip, true);
                }
            }
            for (int i = shown; i < _chips.Count; i++)
                ShowHull(_chips[i], false);
        }

        // A hidden chip leaves the row's layout too, or the right-aligned row would keep its gap.
        private static void ShowHull(GameObject chip, bool shown)
        {
            if (chip.TryGetComponent(out CanvasGroup group)) KitVisibility.SetShown(group, shown);
            if (chip.TryGetComponent(out LayoutElement element)) element.ignoreLayout = !shown;
        }
    }
}
