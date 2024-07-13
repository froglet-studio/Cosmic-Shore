using CosmicShore.App.UI;
using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore
{
    public abstract class View : MonoBehaviour
    {
        [SerializeField] NavGroup navGroup;
        public List<ScriptableObject> Models { get; set; }
        protected ScriptableObject SelectedModel { get; set; }

        public void AssignModel(ScriptableObject Model)
        {
            this.Models = new List<ScriptableObject>() { Model };
            Select(0);
        }

        public void AssignModels(List<ScriptableObject> Models)
        {
            this.Models = Models;
            Select(0);
        }

        public virtual void Select(int index)
        {
            SelectedModel = Models[index];
            UpdateView();
        }

        /// <summary>
        /// Updates the UI to reflect the values contained in Models[modelIndex]
        /// </summary>
        /// <param name="modelIndex"></param>
        public abstract void UpdateView();
    }
}