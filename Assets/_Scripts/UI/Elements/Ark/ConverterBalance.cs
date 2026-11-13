using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// One element's balance on the Ark converter (Rev 104 A, "the ledger on the deck", Docs/MENU_REDESIGN_B.md section
    /// 196): its crystal, how many of them the player holds, and its name, standing on the pier with no panel behind it.
    /// The count turns the CTA colour while that element's crystal flies into the omni, and dims at 0.
    ///
    /// <para><see cref="ElementalConverterView"/> draws it every frame; text and colours are written only on a change.</para>
    /// </summary>
    [AddComponentMenu("Cosmic Shore/UI/Converter Balance")]
    public class ConverterBalance : MonoBehaviour
    {
        const float EmptyArtAlpha = 0.35f;

        [Tooltip("The crystal render: dimmed while the player holds none.")]
        [SerializeField] private Image art;
        [Tooltip("How many the player holds (DataLarge role).")]
        [SerializeField] private KitText countText;
        [Tooltip("The element's name (Label role). Its words are authored on this text.")]
        [SerializeField] private KitText nameText;

        int _held = -1;
        bool _spending, _drawn;

        /// <summary>Draw <paramref name="held"/>; <paramref name="spending"/> while this element's crystal flies in.</summary>
        public void Draw(int held, bool spending)
        {
            if (_drawn && held == _held && spending == _spending) return;
            bool countChanged = !_drawn || held != _held;
            _drawn = true;
            _held = held;
            _spending = spending;
            bool empty = held <= 0;

            if (countText)
            {
                if (countChanged) countText.SetText(held.ToString());
                countText.SetColorToken(spending ? UIColorToken.Cta : empty ? UIColorToken.NeutralLightest : UIColorToken.TextLight);
            }
            if (nameText) nameText.SetColorToken(empty && !spending ? UIColorToken.TextLight : UIColorToken.NeutralLightest);
            if (art) art.color = new Color(1f, 1f, 1f, empty && !spending ? EmptyArtAlpha : 1f);
        }
    }
}
