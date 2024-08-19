using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.App.UI.Views
{
    public abstract class View : MonoBehaviour
    {
        [SerializeField] NavGroup navGroup;
        public List<ScriptableObject> Models { get; set; }
        protected ScriptableObject SelectedModel { get; set; }
        protected int SelectedIndex;

        public virtual void AssignModel(ScriptableObject Model)
        {
            Models = new List<ScriptableObject> { Model };
            Select(SelectedIndex);
        }

        public virtual void AssignModels(List<ScriptableObject> Models)
        {
            this.Models = Models;
            Select(SelectedIndex);
        }

        public virtual void Select(int index)
        {
            SelectedIndex = index;
            if (Models != null && SelectedIndex < Models.Count)
            {
                SelectedModel = Models[index];
                UpdateView();
            }
        }

        /// <summary>
        /// Updates the UI to reflect the values contained in Models[modelIndex]
        /// </summary>
        public abstract void UpdateView();
    }
}