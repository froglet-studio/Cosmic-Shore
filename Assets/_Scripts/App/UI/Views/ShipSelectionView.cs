using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.App.UI.Views
{
    public class ShipSelectionView : View
    {
        [SerializeField] Transform ShipSelectionGrid;
        [SerializeField] bool VerboseLogging;

        public delegate void SelectionCallback(SO_Ship ship);
        public SelectionCallback OnSelect;

        public override void Select(int index)
        {
            base.Select(index);

            OnSelect?.Invoke(SelectedModel as SO_Ship);
        }

        public override void UpdateView()
        {
            for (var i = 0; i < ShipSelectionGrid.childCount; i++)
            {
                if (VerboseLogging) Debug.Log($"MiniGamesMenu - Populating Ship Select List: {i}");
                var shipSelectionRow = ShipSelectionGrid.transform.GetChild(i);
                for (var j = 0; j < shipSelectionRow.transform.childCount; j++)
                {
                    if (VerboseLogging) Debug.Log($"MiniGamesMenu - Populating Ship Select List: {i},{j}");
                    var selectionIndex = (i * 3) + j;

                    // TODO: convert this to take a CaptainCard prefab and instantiate one rather than using the placeholder objects
                    var shipSelection = shipSelectionRow.transform.GetChild(j).gameObject;
                    if (selectionIndex < Models.Count)
                    {
                        var ship = Models[selectionIndex] as SO_Ship;

                        if (VerboseLogging) Debug.Log($"MiniGamesMenu - Populating Ship Select List: {ship.Name}");

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
                        shipSelection.GetComponent<Button>().onClick.RemoveAllListeners();
                        shipSelection.GetComponent<Button>().onClick.AddListener(() => Select(selectionIndex));
                        shipSelection.GetComponent<Button>().onClick.AddListener(() => ShipSelectionGrid.GetComponent<MenuAudio>().PlayAudio());
                    }
                    else
                    {
                        // Deactive remaining
                        shipSelection.SetActive(false);
                    }
                }
            }
        }
    }
}