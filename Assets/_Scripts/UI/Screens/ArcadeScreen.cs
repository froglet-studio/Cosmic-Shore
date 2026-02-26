using CosmicShore.Systems.UserAction;
using CosmicShore.Models.Enums;
using CosmicShore.UI.Views;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using System.Linq;

namespace CosmicShore.UI.Screens
{
    public class ArcadeScreen : MonoBehaviour
    {
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

            LoadoutView.gameObject.SetActive(loadout);
            ExploreView.gameObject.SetActive(!loadout);
        }
    }
}