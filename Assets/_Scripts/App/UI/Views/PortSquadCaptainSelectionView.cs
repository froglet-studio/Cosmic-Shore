using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore.App.UI.Views
{
    public class PortSquadCaptainSelectionView : View
    {
        [SerializeField] List<GameObject> Rows;
        [SerializeField] Color32 SelectedRowColor = Color.gray;
        [SerializeField] Color32 UnselectedRowColor = Color.black;
        List<SO_Captain> sortedCaptains = new List<SO_Captain>();

        public delegate void SelectionCallback(SO_Captain captain);
        public SelectionCallback OnSelect;

        public bool IsPlayer = false;

        public override void AssignModel(ScriptableObject Model)
        {
            // cast
            var ship = Model as SO_Ship;

            // sort
            sortedCaptains = ship.Captains;
            sortedCaptains.Sort((x, y) => { return x.PrimaryElement < y.PrimaryElement ? 1 : -1; });
            
            base.AssignModel(Model);
        }

        public override void UpdateView()
        {
            // populate rows
            int row = 0;
            foreach (var captain in sortedCaptains) 
            {
                // captain image : element level : flavor text : selection color

                // Captain Image
                Rows[row].transform.GetChild(0).GetComponent<Image>().sprite = captain.Image;

                // Elemental image
                Rows[row].transform.GetChild(1).GetComponent<Image>().sprite = captain.SelectedIcon;

                // flavor text
                if (IsPlayer)
                    Rows[row].transform.GetChild(2).GetComponent<TMP_Text>().text = captain.Description;
                else
                    Rows[row].transform.GetChild(2).GetComponent<TMP_Text>().text = captain.Flavor;

                if (row == SelectedIndex)
                    Rows[row].GetComponent<Image>().color = SelectedRowColor;
                else
                    Rows[row].GetComponent<Image>().color = UnselectedRowColor;

                row++;
            }
        }

        public void SetSelectedCaptain(SO_Captain selectedCaptain)
        {
            int row = 0;
            foreach (var captain in sortedCaptains)
            {
                if (captain.Name == selectedCaptain.Name)
                {
                    Select(row);
                    return;
                }
                row++;
            }
        }

        public override void Select(int index)
        {
            Debug.Log($"Selected {index}");
            SelectedIndex = index;
            OnSelect?.Invoke(sortedCaptains[index]);

            UpdateView();
        }
    }   
}