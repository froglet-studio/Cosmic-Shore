using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.Game.UI
{
    public class StatRowUI : MonoBehaviour
    {
        [SerializeField] Image iconImage;
        [SerializeField] TMP_Text labelText;
        [SerializeField] TMP_Text valueText;

        public void Initialize(string label, string value, Sprite icon)
        {
            if (labelText) labelText.text = label;
            if (valueText) valueText.text = value;

            if (!iconImage) return;
            if (icon)
            {
                iconImage.sprite = icon;
                iconImage.gameObject.SetActive(true);
            }
            else
            {
                iconImage.gameObject.SetActive(false); 
            }
        }
    }
}