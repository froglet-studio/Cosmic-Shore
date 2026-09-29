using UnityEngine;
using TMPro;
using CosmicShore.Utility;

namespace CosmicShore.UI
{
    public class VersionDisplay : MonoBehaviour
    {
        [SerializeField] TMP_Text tmpText;
        [SerializeField] string prefix;
        void Start()
        {
            tmpText.text = prefix + " " + Application.version;
        }
    }
}