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
            if (Model == null)
            {
                Debug.LogWarning($"{nameof(View)} on {name} received a null model.");
                Models = null;
                SelectedModel = null;
                return;
            }

            Models = new List<ScriptableObject> { Model };

            if (shipClassTypeVariable != null)
            {
                Select(shipClassTypeVariable.Value);
            }
            else
            {
                // Fall back to the single provided model when no selector variable is configured.
                SelectedModel = Model;
                UpdateView();
            }
        }

        public virtual void AssignModels(List<ScriptableObject> Models)
        {
            if (Models == null || Models.Count == 0)
            {
                Debug.LogWarning($"{nameof(View)} on {name} received an empty model list.");
                this.Models = null;
                SelectedModel = null;
                return;
            }

            this.Models = Models;

            if (shipClassTypeVariable != null)
            {
                Select(shipClassTypeVariable.Value);
            }
            else
            {
                SelectedModel = Models[0];
                UpdateView();
            }
        }

        public virtual void Select(int index)
        {
            if (shipClassTypeVariable == null)
            {
                Debug.LogWarning($"{nameof(View)} on {name} cannot select a model because shipClassTypeVariable is not set.");
                return;
            }

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
