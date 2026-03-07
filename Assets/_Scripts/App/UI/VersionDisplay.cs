using UnityEngine;
using TMPro;
using CosmicShore.Utility;

namespace CosmicShore.App.UI
{
    public class VersionDisplay : MonoBehaviour
    {
        [SerializeField] TMP_Text tmpText;
        [SerializeField] string prefix;
        void Start()
        {
            //CSDebug.Log("Application Version : " + Application.version);
            tmpText.text = prefix + " " + Application.version;
        }
    }
}