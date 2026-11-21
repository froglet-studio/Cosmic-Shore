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

        [Header("Words (empty shows nothing)")]
        [Tooltip("The eyebrow. {pilots} is the player count or range ('2 - 6').")]
        [SerializeField] private string eyebrowLine = "Arena - {pilots} pilots";
        [Tooltip("The domains line for one domain. {domains} is the count.")]
        [SerializeField] private string oneDomainLine = "{domains} domain";
        [Tooltip("The domains line for more than one. {domains} is the count or range.")]
        [SerializeField] private string manyDomainsLine = "{domains} domains";

        private readonly List<GameObject> _chips = new();

        protected override void DrawDetails(SO_ArcadeGame game)
        {
            if (eyebrowText) eyebrowText.SetText(TemplateText.Fill(eyebrowLine, "pilots", Range(game.MinPlayersAllowed, game.MaxPlayersAllowed)));
            if (taglineText) taglineText.text = game.Description;
            if (domainsText)
            {
                domainsText.SetText(TemplateText.Fill(game.MaxDomainsAllowed == 1 ? oneDomainLine : manyDomainsLine,
                    "domains", Range(game.MinDomainsAllowed, game.MaxDomainsAllowed)));
            }
            DrawHulls(game);
        }

        /// <summary>"2" when the bounds agree, "2 - 6" otherwise.</summary>
        public static string Range(int min, int max) => min == max ? min.ToString() : $"{min} - {max}";

        const string IconChildName = "Icon";

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
                    // By name, not index: the chip's frame (Fill, Border) comes before its Icon, so child 0 put the
                    // vessel art on the fill and left the sprite-less Icon drawing a white square on top (2026-10-19).
                    var iconNode = chip.transform.Find(IconChildName);
                    if (iconNode && iconNode.TryGetComponent(out Image icon))
                    {
                        icon.sprite = sprite;
                        icon.enabled = true;
                    }
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
