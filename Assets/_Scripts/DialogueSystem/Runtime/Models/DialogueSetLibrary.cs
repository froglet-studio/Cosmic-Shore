using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.DialogueSystem.Runtime.Models
{
    [CreateAssetMenu(menuName = "CosmicShore/Dialogue/Dialogue Library")]
    public class DialogueSetLibrary : ScriptableObject
    {
        public List<DialogueSet> allDialogueSets;

        public DialogueSet GetSetById(string id)
        {
            return allDialogueSets.Find(set => set.setId == id);
        }
    }
}
