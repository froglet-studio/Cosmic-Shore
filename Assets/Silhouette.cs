using CosmicShore.Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace CosmicShore
{
    public class Silhouette : MonoBehaviour
    {
        [SerializeField] List<GameObject> silhouetteParts = new List<GameObject>();

        #region Optional configuration 
        [SerializeField] DriftTrailAction driftTrailAction;
        private void OnEnable()
        {
            driftTrailAction.OnChangeDriftAltitude += calculateDriftAngle;
            ship.ResourceSystem.OnAmmoChange += calculateBlastAngle;
        }
        private void OnDisable()
        {
            driftTrailAction.OnChangeDriftAltitude -= calculateDriftAngle;
            ship.ResourceSystem.OnAmmoChange -= calculateBlastAngle;
        }

        [SerializeField] GameObject TopJaw;
        [SerializeField] GameObject BottomJaw;
        [SerializeField] GameObject block;
  
        #endregion


        [SerializeField] Ship ship;
        Game.UI.MiniGameHUD hud;
        GameObject hudContainer;

        // Start is called before the first frame update
        void Start()
        {
            if (ship.Player.GameCanvas != null)
            {
                hud = ship.Player.GameCanvas.MiniGameHUD;
                hudContainer = hud.SetSilhouetteActive(!ship.AutoPilot.AutoPilotEnabled);
                foreach (var part in silhouetteParts)
                {
                    part.transform.SetParent(hudContainer.transform, false);
                    part.SetActive(true);
                }
            }
        }

        private void calculateDriftAngle(float dotProduct)
        {
            hudContainer.transform.rotation = Quaternion.Euler(0, 0, Mathf.Acos(dotProduct-.0001f) * Mathf.Rad2Deg); // Acos hates 1
        }

        private void calculateBlastAngle(float currentAmmo)
        {
            TopJaw.transform.localRotation = Quaternion.Euler(0, 0, 21 * currentAmmo);
            BottomJaw.transform.localRotation = Quaternion.Euler(0, 0, -21 * currentAmmo);

            TopJaw.GetComponent<Image>().color = currentAmmo > .98 ? Color.green : Color.white ;
            BottomJaw.GetComponent<Image>().color = currentAmmo > .98 ? Color.green : Color.white ;
        }


    }
}
