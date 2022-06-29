using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StarWriter.Core.Input
{
    public class FlipUI : MonoBehaviour
    {
        private void OnEnable()
        {
            GameManager.onPhoneFlip += OnPhoneFlip;
        }

        private void OnDisable()
        {
            GameManager.onPhoneFlip -= OnPhoneFlip;
        }
        // Start is called before the first frame update
        private void OnPhoneFlip(bool state)
        {
            if (state) transform.rotation = Quaternion.identity;
            else transform.rotation = Quaternion.Euler(0,0,180);
        }
    }

}
