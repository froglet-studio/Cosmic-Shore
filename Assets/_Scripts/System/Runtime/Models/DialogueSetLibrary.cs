using System.Collections.Generic;
using UnityEngine;

namespace CosmicShore.Core
{
    [CreateAssetMenu(menuName = "ScriptableObjects/Dialogue/Dialogue Library")]
    public class DialogueSetLibrary : ScriptableObject
    {
        [SerializeField] List<DialogueSet> allDialogueSets;
        public List<DialogueSet> AllDialogueSets => allDialogueSets;

        public DialogueSet GetSetById(string id)
        {
            return allDialogueSets.Find(set => set.setId == id);
        }
    }
}
