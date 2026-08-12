using CosmicShore.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using CosmicShore.Gameplay;
using CosmicShore.Data;
using CosmicShore.ScriptableObjects;
using CosmicShore.Utility;

namespace CosmicShore.UI
{
    public class SquadMemberCard : MonoBehaviour
    {
        [SerializeField] bool ShowShipName = false;
        [SerializeField] TMP_Text CaptainName;
        [SerializeField] TMP_Text ShipName;
        [SerializeField] Image CaptainImage;
        [SerializeField] Image ShipImage;

        SO_Captain captain;
        public SO_Captain Captain
        {
            get { return captain; }
            set
            {
                captain = value;
                UpdateView();
            }
        }

        public void SetShip(SO_Vessel ship)
        {
            // Captain system removed from vessels - Port/squad screen is inactive.
        }

        /// <summary>
        /// This exists in addition to the Property for Captain so that it can be invoked as a delegate
        /// </summary>
        /// <param name="captain"></param>
        public void SetCaptain(SO_Captain captain)
        {
            CSDebug.Log($"SetCaptain:{(captain != null ? captain.Name : "<none>")}");
            Captain = captain;
        }

        /// <summary>
        /// A null captain is the NORMAL state here, not an error: captains were removed from
        /// vessels, so <see cref="PortSquadView"/> seeds an empty roster and then pushes
        /// SquadSystem's (null) leader and rogues straight into these cards on Start. The
        /// unguarded dereference that used to sit on the first line threw a
        /// NullReferenceException out of Start on every entry to the menu.
        ///
        /// Draw the empty card instead, so the Port screen degrades to blank slots until the
        /// squad system is refactored.
        /// </summary>
        void UpdateView()
        {
            if (captain == null)
            {
                if (CaptainName != null) CaptainName.text = string.Empty;
                if (ShipName != null) ShipName.text = string.Empty;
                if (CaptainImage != null) CaptainImage.sprite = null;
                if (ShipImage != null) ShipImage.sprite = null;
                return;
            }

            if (CaptainName != null) CaptainName.text = captain.Name;
            if (CaptainImage != null) CaptainImage.sprite = captain.Image;

            var vessel = captain.Vessel;
            if (ShipImage != null) ShipImage.sprite = vessel != null ? vessel.SquadImage : null;
            if (ShowShipName && ShipName != null) ShipName.text = vessel != null ? vessel.Name : string.Empty;
        }
    }
}