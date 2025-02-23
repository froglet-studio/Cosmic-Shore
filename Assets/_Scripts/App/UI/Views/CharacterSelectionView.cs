using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.App.UI.Views
{
    public class CharacterSelectionView : View
    {
        [SerializeField] Transform ShipSelectionGrid;

        public override void UpdateView()
        {
            for (int i = 0; i < ShipSelectionGrid.childCount; i++)
            {
                var shipSelection = ShipSelectionGrid.GetChild(i).gameObject;

                if (i < Models.Count)
                {
                    var ship = Models[i] as SO_Ship;
                    shipSelection.SetActive(true);

                    if (SelectedModel as SO_Ship == ship)
                    {
                        shipSelection.GetComponent<Image>().sprite = ship.IconActive;
                        shipSelection.transform.GetChild(0).gameObject.SetActive(true);
                    }
                    else
                    {
                        shipSelection.GetComponent<Image>().sprite = ship.IconInactive;
                        shipSelection.transform.GetChild(0).gameObject.SetActive(false);
                    }

                    shipSelection.transform.GetChild(0).GetComponent<TMP_Text>().text = ship.Name.ToUpper();
                    var button = shipSelection.GetComponent<Button>();
                    button.onClick.RemoveAllListeners();
                    int index = i; // Capture the current index for the lambda
                    button.onClick.AddListener(() => Select(index));
                    button.onClick.AddListener(() => ShipSelectionGrid.GetComponent<MenuAudio>().PlayAudio());
                }
                else
                {
                    shipSelection.SetActive(false);
                }
            }
        }

    }
}