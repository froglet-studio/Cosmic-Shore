using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CosmicShore.Utility;

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
            // Captain system removed from vessels — Port screen is inactive.
            sortedCaptains.Clear();
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
                Rows[row].transform.GetChild(1).GetComponent<Image>().sprite = captain.IconActive;

                // flavor text
                if (IsPlayer)
                    Rows[row].transform.GetChild(2).GetComponent<TMP_Text>().text = captain.Description;
                else
                    Rows[row].transform.GetChild(2).GetComponent<TMP_Text>().text = captain.Flavor;

                if (row == shipClassTypeVariable.Value)
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
            if (sortedCaptains == null || index < 0 || index >= sortedCaptains.Count)
                return;

            CSDebug.Log($"Selected {index}");
            shipClassTypeVariable.Value = index;
            OnSelect?.Invoke(sortedCaptains[index]);

            UpdateView();
        }
    }   
}