using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// One line of a Codex page - a fact (label and value), an element cell (its ethirion, name and stats) or a row (an
    /// ability, a vessel, a painting) - copied from its hidden template by <see cref="CodexPageView"/>. It draws what it
    /// is handed; an empty part hides.
    /// </summary>
    [AddComponentMenu("Cosmic Shore/UI/Codex Line")]
    public class CodexLine : MonoBehaviour
    {
        [Tooltip("The line's group: shown and hidden through it (KitVisibility), never SetActive.")]
        [SerializeField] private CanvasGroup group;
        [Tooltip("The icon on the left. Optional; hidden while the line has none.")]
        [SerializeField] private Image icon;
        [Tooltip("A fact's label, a cell's or a row's name.")]
        [SerializeField] private TMP_Text title;
        [Tooltip("A fact's value, a cell's stats, a row's line under its name.")]
        [SerializeField] private TMP_Text line;

        public CanvasGroup Group => group;

        public void Set(Sprite sprite, string titleText, string lineText)
        {
            if (icon)
            {
                icon.sprite = sprite;
                icon.enabled = sprite;
            }
            if (title) title.text = titleText ?? string.Empty;
            if (line) line.text = lineText ?? string.Empty;
        }
    }
}
