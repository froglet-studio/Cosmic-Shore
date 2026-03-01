using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Utility;
using Reflex.Attributes;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    public class ArcadeScreen : MonoBehaviour
    {
        [Inject] AudioSystem audioSystem;

        [FormerlySerializedAs("exploreMenu")]
        [SerializeField] ArcadeExploreView ExploreView;
        [FormerlySerializedAs("loadoutMenu")]
        [SerializeField] ArcadeLoadoutView LoadoutView;
        [SerializeField] Toggle LoadoutButton;
        [SerializeField] Toggle ExploreButton;

        void Start()
        {
            LoadoutButton.Select();
        }

        public void ToggleView(bool loadout)
        {
            if (loadout)
                UserActionSystem.Instance.CompleteAction(UserActionType.ViewArcadeLoadoutMenu);
            else
                UserActionSystem.Instance.CompleteAction(UserActionType.ViewArcadeExploreMenu);

            audioSystem.PlayMenuAudio(MenuAudioCategory.SwitchView);
            LoadoutView.gameObject.SetVisible(loadout);
            ExploreView.gameObject.SetVisible(!loadout);
        }
    }
}