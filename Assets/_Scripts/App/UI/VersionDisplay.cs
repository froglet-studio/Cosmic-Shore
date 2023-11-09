using UnityEngine;
using TMPro;

namespace StarWriter.UI
{
    public class VersionDisplay : MonoBehaviour
    {
        [SerializeField] TMP_Text tmpText;
        [SerializeField] string prefix;
        void Start()
        {
            //Debug.Log("Application Version : " + Application.version);
            tmpText.text = prefix + " " + Application.version;
        }
    }
}