using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.App.UI.Views
{
    public class CharacterSelectClassComponentReference : MonoBehaviour
    {
        [SerializeField] Button _selectClassButton;
        [SerializeField] GameObject _classSelectedImage;
        [SerializeField] Image _classIcon;

        public Button SelectClassButton => _selectClassButton;
        public GameObject ClassSelectedImage => _classSelectedImage;
        public Image ClassIcon => _classIcon;
    }
}
