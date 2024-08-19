using System.Collections;
using UnityEngine;

namespace CosmicShore.App.UI.Modals
{
    public class ModalWindowManager : MonoBehaviour
    {
        [Header("Settings")]
        public bool sharpAnimations = false;

        Animator mWindowAnimator;
        bool isOn = false;

        void Start()
        {
            mWindowAnimator = gameObject.GetComponent<Animator>();

            gameObject.SetActive(false);
        }

        public void ModalWindowIn()
        {
            StopCoroutine("DisableWindow");
            gameObject.SetActive(true);

            if (isOn == false)
            {
                if (sharpAnimations == false)
                    mWindowAnimator.CrossFade("Window In", 0.1f);
                else
                    mWindowAnimator.Play("Window In");

                isOn = true;
            }
        }

        public void ModalWindowOut()
        {
            if (isOn == true)
            {
                if (sharpAnimations == false)
                    mWindowAnimator.CrossFade("Window Out", 0.1f);
                else
                    mWindowAnimator.Play("Window Out");

                isOn = false;
            }

            StartCoroutine("DisableWindow");
        }

        IEnumerator DisableWindow()
        {
            yield return new WaitForSeconds(0.5f);
            gameObject.SetActive(false);
        }
    }
}