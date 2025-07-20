using System.Collections.Generic;
using Obvious.Soap;
using UnityEngine;

namespace CosmicShore.App.UI.Views
{
    public abstract class View : MonoBehaviour
    {
        [SerializeField] NavGroup navGroup;
        public List<ScriptableObject> Models { get; set; }
        protected ScriptableObject SelectedModel { get; set; }

        [SerializeField]
        protected ScriptableVariable<int> shipClassTypeVariable;

        public virtual void AssignModel(ScriptableObject Model)
        {
            Models = new List<ScriptableObject> { Model };
            Select(shipClassTypeVariable.Value);
        }

        public virtual void AssignModels(List<ScriptableObject> Models)
        {
            this.Models = Models;
            Select(shipClassTypeVariable.Value);
        }

        public virtual void Select(int index)
        {
            shipClassTypeVariable.Value = index;
            if (Models != null && shipClassTypeVariable.Value < Models.Count)
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