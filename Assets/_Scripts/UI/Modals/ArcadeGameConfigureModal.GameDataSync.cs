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
        #region GameData sync helpers

        void SyncGameDataConfig()
        {
            if (!gameData) return;

            if (gameData.SelectedIntensity)
                gameData.SelectedIntensity.Value = config.Intensity;

            if (gameData.SelectedPlayerCount)
                gameData.SelectedPlayerCount.Value = config.PlayerCount;

            SyncSwitchbackSeedMode();
        }

        void SyncGameDataShip(SO_Vessel ship)
        {
            if (!gameData || !gameData.selectedVesselClass)
                return;

            VesselClassType targetClass = ship ? ship.Class : VesselClassType.Dolphin;

            gameData.selectedVesselClass.Value = targetClass;

            if (gameData.VesselClassSelectedIndex)
                gameData.VesselClassSelectedIndex.Value = (int)targetClass;
        }

        /// <summary>
        /// Writes the selected vessel class directly to the local Player's
        /// NetDefaultVesselType NetworkVariable (owner-writable - legal from the
        /// owning client, unlike NetDomain). This ensures each client's vessel
        /// choice is propagated to the server independently of
        /// gameData.selectedVesselClass (which carries the host's choice).
        /// </summary>
        void SyncLocalPlayerVesselType(SO_Vessel ship, bool commit)
        {
            var localPlayer = commit ? ResolveLocalOwnedPlayer() : LocalOwnedPlayerQuiet();
            if (localPlayer == null)
            {
                // A provisional write (card open, carousel) can run before this machine's Player
                // exists: the default hull was dropped with a red error on every such open (Bug
                // Ledger E-908f18b36f, Andrew, Garrett, Shombith). It is re-asserted at the commit,
                // which is where a missing Player really means a wrong hull.
                if (!commit) return;
                CSDebug.LogError("[ArcadeConfigModal] Vessel selection DROPPED - no owned local Player " +
                               "resolved. NetDefaultVesselType not updated; spawn would use a stale class.");
                return;
            }

            localPlayer.NetDefaultVesselType.Value = ship ? ship.Class : VesselClassType.Dolphin;
        }

        #endregion
    }
}
