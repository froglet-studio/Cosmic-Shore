using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    /// <summary>
    /// A phone-only button that opens the friends drawer (Docs/MENU_REDESIGN_B.md section 129, R55Arcade: the Arcade's
    /// Party button). On PC the drawer's own presence orb opens it and this button stays hidden; on a phone (its canvas's
    /// <see cref="AdaptiveCanvasScaler"/> at the phone layout) it shows. The drawer is the scene's one
    /// <see cref="FriendsDrawer"/>, found when pressed (the button lives in a prefab that cannot reference the scene).
    /// </summary>
    [AddComponentMenu("Cosmic Shore/UI/Friends Drawer Opener")]
    public class FriendsDrawerOpener : MonoBehaviour
    {
        [Tooltip("The button that opens the drawer.")]
        [SerializeField] private Button button;
        [Tooltip("The button's CanvasGroup: shown on a phone, hidden on PC.")]
        [SerializeField] private CanvasGroup group;

        private AdaptiveCanvasScaler _scaler;

        private void Awake()
        {
            if (button) button.onClick.AddListener(OpenDrawer);
        }

        private void OnDestroy()
        {
            if (button) button.onClick.RemoveListener(OpenDrawer);
        }

        private void OnEnable()
        {
            _scaler = GetComponentInParent<AdaptiveCanvasScaler>(true);
            if (_scaler) _scaler.PhoneLayoutChanged += Show;
            Show(_scaler && _scaler.UsesPhoneLayout);
        }

        private void OnDisable()
        {
            if (_scaler) _scaler.PhoneLayoutChanged -= Show;
        }

        private void Show(bool phone)
        {
            if (!group) return;
            group.alpha = phone ? 1f : 0f;
            group.interactable = phone;
            group.blocksRaycasts = phone;
        }

        private void OpenDrawer()
        {
            var drawer = FindAnyObjectByType<FriendsDrawer>(FindObjectsInactive.Exclude);
            if (drawer) drawer.Open();
        }
    }
}
