using System;
using System.Collections.Generic;
using System.Linq;
using CosmicShore.Core;
using CosmicShore.Data;
using CosmicShore.Gameplay;
using CosmicShore.ScriptableObjects;
using CosmicShore.UI;
using CosmicShore.Utility;
using Obvious.Soap;
using Reflex.Attributes;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace CosmicShore.UI
{
    // Part of ArcadeGameConfigureModal (Docs/REFACTOR_BACKLOG.md M13): this region moved here verbatim from ArcadeGameConfigureModal.cs.
    public partial class ArcadeGameConfigureModal
    {
        #region Vessel selection

        /// <summary>
        /// Commit the hull this card locks to. There is no picker any more - every arcade mode
        /// names one vessel, which is what collapsed the two-screen flow into one panel - so this
        /// runs once per card open, from <see cref="InitializeDefaultShipFromAvailable"/>.
        /// </summary>
        /// <param name="commit">True where the pilot commits to the hull (SELECT VESSEL, Start): the
        /// owner write must land. False for the provisional writes (the default on card open, the
        /// carousel), which may run before this machine's Player exists; Start re-asserts them.</param>
        void SetSelectedShipInternal(SO_Vessel ship, bool commit = false)
        {
            if (config)
                config.SelectedShip = ship;

            SyncGameDataShip(ship);

            // Write the selected vessel class to the local player's NetworkVariable
            // so the server spawns the correct vessel for this client.
            SyncLocalPlayerVesselType(ship, commit);
        }

        #endregion
    }
}
