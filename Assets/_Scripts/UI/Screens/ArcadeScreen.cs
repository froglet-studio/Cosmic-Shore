using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.UI;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using System.Linq;

namespace CosmicShore.UI
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