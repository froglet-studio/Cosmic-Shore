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
        #region Domain count stepper

        void HandleDomainCountChanged(int newDomainCount)
        {
            if (config == null) return;
            if (IsClientMode) return;

            // Pre-commit, host-local DC change. No snap-back: nobody has picked
            // a domain yet (CommitConfiguration resets all humans to Jade), so
            // there's nothing to protect against. No client broadcast either -
            // clients don't open the modal until commit.
            int proposed = Mathf.Clamp(newDomainCount, MinDomainsForGame, ComputeMaxDomainCount());

            if (proposed == config.DomainCount)
            {
                if (dcStepper) dcStepper.SetValue(config.DomainCount);
                return;
            }

            config.DomainCount = proposed;
            RefreshTileVisibility();
            SyncGameDataConfig();
        }

        #endregion
    }
}
